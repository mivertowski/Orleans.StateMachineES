using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Orleans;
using Stateless;

namespace ivlt.Orleans.StateMachineES.Versioning;

/// <summary>
/// Service for evaluating state machine transitions in shadow mode without affecting live state.
/// Enables safe testing of new state machine versions before committing to upgrades.
/// </summary>
/// <typeparam name="TState">The type of states in the state machine.</typeparam>
/// <typeparam name="TTrigger">The type of triggers in the state machine.</typeparam>
public class ShadowEvaluator<TState, TTrigger>
    where TState : struct, Enum
    where TTrigger : struct, Enum
{
    private readonly ILogger<ShadowEvaluator<TState, TTrigger>> _logger;
    private readonly StateMachineIntrospector<TState, TTrigger> _introspector;

    public ShadowEvaluator(ILogger<ShadowEvaluator<TState, TTrigger>> logger, IServiceProvider? serviceProvider = null)
    {
        _logger = logger;
        _introspector = serviceProvider?.GetService<StateMachineIntrospector<TState, TTrigger>>() ??
                       new StateMachineIntrospector<TState, TTrigger>(
                           serviceProvider?.GetService<ILogger<StateMachineIntrospector<TState, TTrigger>>>() ?? 
                           new LoggerFactory().CreateLogger<StateMachineIntrospector<TState, TTrigger>>());
    }

    /// <summary>
    /// Evaluates a trigger against multiple state machine versions and compares results.
    /// </summary>
    /// <param name="currentState">The current state of the machine.</param>
    /// <param name="trigger">The trigger to evaluate.</param>
    /// <param name="versionedMachines">Dictionary of versioned state machines.</param>
    /// <param name="currentVersion">The current version being used.</param>
    /// <returns>Comparison results across all versions.</returns>
    public async Task<ShadowComparisonResult<TState>> EvaluateAcrossVersionsAsync(
        TState currentState,
        TTrigger trigger,
        Dictionary<StateMachineVersion, StateMachine<TState, TTrigger>> versionedMachines,
        StateMachineVersion currentVersion)
    {
        var startTime = DateTime.UtcNow;
        var results = new List<ShadowEvaluationResult<TState>>();

        _logger.LogDebug("Starting shadow evaluation across {VersionCount} versions for trigger {Trigger} in state {State}",
            versionedMachines.Count, trigger, currentState);

        foreach (var kvp in versionedMachines)
        {
            var version = kvp.Key;
            var machine = kvp.Value;

            try
            {
                var result = await EvaluateSingleVersionAsync(currentState, trigger, machine, version);
                results.Add(result);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error evaluating version {Version}", version);
                results.Add(ShadowEvaluationResult<TState>.Failure(
                    currentState, version, ex.Message, ex));
            }
        }

        var duration = DateTime.UtcNow - startTime;
        
        return new ShadowComparisonResult<TState>
        {
            CurrentState = currentState,
            Trigger = trigger,
            CurrentVersion = currentVersion,
            EvaluationResults = results,
            TotalDuration = duration,
            HasDivergentBehavior = DetectDivergentBehavior(results),
            ConsensusResult = DetermineConsensus(results),
            Metadata = new Dictionary<string, object>
            {
                ["EvaluatedVersions"] = versionedMachines.Count,
                ["SuccessfulEvaluations"] = results.Count(r => r.WouldSucceed),
                ["FailedEvaluations"] = results.Count(r => !r.WouldSucceed)
            }
        };
    }

    /// <summary>
    /// Evaluates a trigger against a single version of the state machine.
    /// </summary>
    private async Task<ShadowEvaluationResult<TState>> EvaluateSingleVersionAsync(
        TState currentState,
        TTrigger trigger,
        StateMachine<TState, TTrigger> machine,
        StateMachineVersion version)
    {
        var startTime = DateTime.UtcNow;

        try
        {
            // Create a copy of the machine with the current state
            var shadowMachine = CreateShadowMachine(machine, currentState);
            
            // Check if trigger is permitted
            var permittedTriggers = shadowMachine.GetPermittedTriggers();
            
            if (!permittedTriggers.Contains(trigger))
            {
                return ShadowEvaluationResult<TState>.Failure(
                    currentState,
                    version,
                    $"Trigger {trigger} is not permitted in state {currentState}",
                    duration: DateTime.UtcNow - startTime);
            }

            // Simulate firing the trigger to get destination state
            var destinationState = await SimulateTriggerAsync(shadowMachine, trigger);
            
            var duration = DateTime.UtcNow - startTime;
            
            return ShadowEvaluationResult<TState>.Success(
                currentState, 
                destinationState, 
                version, 
                duration);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to evaluate trigger {Trigger} for version {Version}", trigger, version);
            
            return ShadowEvaluationResult<TState>.Failure(
                currentState,
                version,
                ex.Message,
                ex,
                DateTime.UtcNow - startTime);
        }
    }

    /// <summary>
    /// Creates a shadow copy of the state machine for safe evaluation.
    /// </summary>
    private StateMachine<TState, TTrigger> CreateShadowMachine(
        StateMachine<TState, TTrigger> originalMachine, 
        TState currentState)
    {
        // Use the introspector to create a proper deep copy
        return _introspector.CloneStateMachine(originalMachine, currentState);
    }

    /// <summary>
    /// Simulates firing a trigger and returns the destination state.
    /// </summary>
    private async Task<TState> SimulateTriggerAsync(
        StateMachine<TState, TTrigger> machine, 
        TTrigger trigger)
    {
        var currentState = machine.State;
        
        // Use the introspector to predict the transition
        var prediction = await _introspector.PredictTransition(machine, currentState, trigger);
        
        if (!prediction.CanFire)
        {
            throw new InvalidOperationException($"Trigger {trigger} is not permitted: {prediction.Reason}");
        }

        if (prediction.IsIgnored)
        {
            // Trigger is ignored, state remains the same
            return currentState;
        }

        if (prediction.PredictedState.HasValue)
        {
            return prediction.PredictedState.Value;
        }

        if (prediction.HasGuard && prediction.PossibleDestinations.Count > 0)
        {
            // For guarded transitions, return the first possible destination
            // In production, you might want to evaluate the actual guard condition
            _logger.LogDebug("Guarded transition has {Count} possible destinations, returning first",
                prediction.PossibleDestinations.Count);
            return prediction.PossibleDestinations.First();
        }

        // Fallback to current state if no destination found
        _logger.LogWarning("Could not determine destination state for trigger {Trigger} in state {State}",
            trigger, currentState);
        return currentState;
    }

    /// <summary>
    /// Detects if different versions would produce different behaviors.
    /// </summary>
    private bool DetectDivergentBehavior(List<ShadowEvaluationResult<TState>> results)
    {
        if (results.Count <= 1) return false;

        var successfulResults = results.Where(r => r.WouldSucceed).ToList();
        
        // Check if success/failure differs
        if (successfulResults.Count != results.Count && successfulResults.Count > 0)
            return true;

        // Check if predicted states differ among successful results
        if (successfulResults.Count > 1)
        {
            var firstPredictedState = successfulResults.First().PredictedState;
            return successfulResults.Any(r => !EqualityComparer<TState?>.Default.Equals(r.PredictedState, firstPredictedState));
        }

        return false;
    }

    /// <summary>
    /// Determines consensus result from multiple evaluations.
    /// </summary>
    private ShadowConsensusResult<TState> DetermineConsensus(List<ShadowEvaluationResult<TState>> results)
    {
        if (results.Count == 0)
        {
            return new ShadowConsensusResult<TState>
            {
                HasConsensus = false,
                ConsensusType = ConsensusType.NoResults
            };
        }

        var successfulResults = results.Where(r => r.WouldSucceed).ToList();
        var failureResults = results.Where(r => !r.WouldSucceed).ToList();

        if (successfulResults.Count == results.Count)
        {
            // All succeeded - check if they agree on destination
            var firstPredictedState = successfulResults.First().PredictedState;
            var allAgree = successfulResults.All(r => 
                EqualityComparer<TState?>.Default.Equals(r.PredictedState, firstPredictedState));

            return new ShadowConsensusResult<TState>
            {
                HasConsensus = allAgree,
                ConsensusType = allAgree ? ConsensusType.AllSuccess : ConsensusType.SuccessWithDivergence,
                ConsensusPrediction = allAgree ? firstPredictedState : null
            };
        }

        if (failureResults.Count == results.Count)
        {
            return new ShadowConsensusResult<TState>
            {
                HasConsensus = true,
                ConsensusType = ConsensusType.AllFailure
            };
        }

        return new ShadowConsensusResult<TState>
        {
            HasConsensus = false,
            ConsensusType = ConsensusType.Mixed,
            SuccessfulCount = successfulResults.Count,
            FailureCount = failureResults.Count
        };
    }
}

/// <summary>
/// Result of comparing shadow evaluations across multiple versions.
/// </summary>
[GenerateSerializer]
public class ShadowComparisonResult<TState>
    where TState : struct, Enum
{
    [Id(0)] public TState CurrentState { get; set; }
    [Id(1)] public object Trigger { get; set; } = new();
    [Id(2)] public StateMachineVersion CurrentVersion { get; set; } = new();
    [Id(3)] public List<ShadowEvaluationResult<TState>> EvaluationResults { get; set; } = new();
    [Id(4)] public TimeSpan TotalDuration { get; set; }
    [Id(5)] public bool HasDivergentBehavior { get; set; }
    [Id(6)] public ShadowConsensusResult<TState> ConsensusResult { get; set; } = new();
    [Id(7)] public Dictionary<string, object> Metadata { get; set; } = new();

    /// <summary>
    /// Gets the results grouped by predicted outcome.
    /// </summary>
    public Dictionary<TState?, List<StateMachineVersion>> GetResultsByOutcome()
    {
        var grouped = new Dictionary<TState?, List<StateMachineVersion>>();
        
        foreach (var result in EvaluationResults.Where(r => r.WouldSucceed))
        {
            if (!grouped.ContainsKey(result.PredictedState))
                grouped[result.PredictedState] = new List<StateMachineVersion>();
            
            grouped[result.PredictedState].Add(result.EvaluatedVersion);
        }

        return grouped;
    }
}

/// <summary>
/// Consensus result from shadow evaluations.
/// </summary>
[GenerateSerializer]
public class ShadowConsensusResult<TState>
    where TState : struct, Enum
{
    [Id(0)] public bool HasConsensus { get; set; }
    [Id(1)] public ConsensusType ConsensusType { get; set; }
    [Id(2)] public TState? ConsensusPrediction { get; set; }
    [Id(3)] public int SuccessfulCount { get; set; }
    [Id(4)] public int FailureCount { get; set; }
    [Id(5)] public Dictionary<string, object> Metadata { get; set; } = new();
}

/// <summary>
/// Types of consensus from shadow evaluations.
/// </summary>
public enum ConsensusType
{
    /// <summary>
    /// No evaluation results available.
    /// </summary>
    NoResults,
    
    /// <summary>
    /// All versions succeeded with the same prediction.
    /// </summary>
    AllSuccess,
    
    /// <summary>
    /// All versions succeeded but with different predictions.
    /// </summary>
    SuccessWithDivergence,
    
    /// <summary>
    /// All versions failed.
    /// </summary>
    AllFailure,
    
    /// <summary>
    /// Mixed results - some succeeded, some failed.
    /// </summary>
    Mixed
}