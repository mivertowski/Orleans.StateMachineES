using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Orleans;
using Stateless;
using Stateless.Graph;
using Stateless.Reflection;

namespace ivlt.Orleans.StateMachineES.Versioning;

/// <summary>
/// Enhanced introspector for analyzing and comparing state machine configurations.
/// Provides advanced inspection capabilities using reflection and DOT graph parsing.
/// </summary>
public class ImprovedStateMachineIntrospector<TState, TTrigger>
    where TState : struct, Enum
    where TTrigger : struct, Enum
{
    private readonly ILogger<ImprovedStateMachineIntrospector<TState, TTrigger>> _logger;
    private readonly Dictionary<TState, HashSet<TTrigger>> _permittedTriggersCache = new();
    private readonly Dictionary<(TState, TTrigger), TransitionInfo<TState, TTrigger>> _transitionCache = new();

    public ImprovedStateMachineIntrospector(ILogger<ImprovedStateMachineIntrospector<TState, TTrigger>> logger)
    {
        _logger = logger;
    }

    /// <summary>
    /// Extracts complete configuration from a state machine using multiple techniques.
    /// </summary>
    public async Task<EnhancedStateMachineConfiguration<TState, TTrigger>> ExtractEnhancedConfigurationAsync(
        StateMachine<TState, TTrigger> machine)
    {
        var config = new EnhancedStateMachineConfiguration<TState, TTrigger>
        {
            InitialState = machine.State,
            IsValid = true,
            ExtractionTimestamp = DateTime.UtcNow
        };

        try
        {
            // 1. Extract basic structure from GetInfo()
            var info = machine.GetInfo();
            ExtractBasicStructure(info, config);

            // 2. Use reflection to get internal state machine details
            await ExtractInternalDetailsAsync(machine, config);

            // 3. Probe the state machine to discover transitions
            await ProbeTransitionsAsync(machine, config);

            // 4. Generate and parse DOT graph for additional insights
            ExtractFromDotGraph(machine, config);

            // 5. Analyze guard conditions and actions
            AnalyzeGuardsAndActions(machine, config);

            // 6. Calculate metrics
            CalculateMetrics(config);

            return config;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to extract enhanced state machine configuration");
            config.IsValid = false;
            config.ExtractionErrors.Add(ex.Message);
            return config;
        }
    }

    private void ExtractBasicStructure(StateMachineInfo info, EnhancedStateMachineConfiguration<TState, TTrigger> config)
    {
        foreach (var state in info.States)
        {
            var stateConfig = new EnhancedStateConfiguration<TState, TTrigger>
            {
                State = ParseStateValue(state.UnderlyingState),
                IsInitialState = state.UnderlyingState.ToString() == config.InitialState.ToString()
            };

            // Extract hierarchy
            if (state.Superstate != null)
            {
                stateConfig.Superstate = ParseStateValue(state.Superstate.UnderlyingState);
            }

            foreach (var substate in state.Substates)
            {
                stateConfig.Substates.Add(ParseStateValue(substate.UnderlyingState));
            }

            config.States[stateConfig.State] = stateConfig;
        }
    }

    private async Task ExtractInternalDetailsAsync(
        StateMachine<TState, TTrigger> machine, 
        EnhancedStateMachineConfiguration<TState, TTrigger> config)
    {
        await Task.Run(() =>
        {
            var machineType = machine.GetType();
            
            // Try to access internal state configuration via reflection
            var stateConfigField = machineType.GetField("_stateConfiguration", 
                BindingFlags.NonPublic | BindingFlags.Instance);
                
            if (stateConfigField != null)
            {
                var stateConfig = stateConfigField.GetValue(machine);
                if (stateConfig != null)
                {
                    ExtractStateConfigurationDetails(stateConfig, config);
                }
            }

            // Try to get trigger configuration
            var triggerConfigField = machineType.GetField("_triggerConfiguration",
                BindingFlags.NonPublic | BindingFlags.Instance);
                
            if (triggerConfigField != null)
            {
                var triggerConfig = triggerConfigField.GetValue(machine);
                if (triggerConfig != null)
                {
                    ExtractTriggerConfigurationDetails(triggerConfig, config);
                }
            }
        });
    }

    private void ExtractStateConfigurationDetails(object stateConfig, EnhancedStateMachineConfiguration<TState, TTrigger> config)
    {
        try
        {
            var configType = stateConfig.GetType();
            
            // Try to enumerate state representations
            if (configType.IsGenericType)
            {
                var enumerableMethod = configType.GetMethod("GetEnumerator");
                if (enumerableMethod != null)
                {
                    var enumerator = enumerableMethod.Invoke(stateConfig, null);
                    if (enumerator != null)
                    {
                        var moveNextMethod = enumerator.GetType().GetMethod("MoveNext");
                        var currentProp = enumerator.GetType().GetProperty("Current");
                        
                        while ((bool)(moveNextMethod?.Invoke(enumerator, null) ?? false))
                        {
                            var current = currentProp?.GetValue(enumerator);
                            if (current != null)
                            {
                                ExtractStateRepresentation(current, config);
                            }
                        }
                    }
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogDebug(ex, "Could not extract state configuration details");
        }
    }

    private void ExtractStateRepresentation(object stateRep, EnhancedStateMachineConfiguration<TState, TTrigger> config)
    {
        try
        {
            var repType = stateRep.GetType();
            
            // Get the state value
            var stateProp = repType.GetProperty("State", BindingFlags.Public | BindingFlags.Instance);
            if (stateProp != null)
            {
                var stateValue = stateProp.GetValue(stateRep);
                if (stateValue != null && Enum.TryParse<TState>(stateValue.ToString(), out var state))
                {
                    if (config.States.TryGetValue(state, out var stateConfig))
                    {
                        // Extract permitted triggers
                        var permittedTriggersProp = repType.GetProperty("PermittedTriggers", 
                            BindingFlags.Public | BindingFlags.Instance);
                        if (permittedTriggersProp != null)
                        {
                            var triggers = permittedTriggersProp.GetValue(stateRep) as IEnumerable<object>;
                            if (triggers != null)
                            {
                                foreach (var trigger in triggers)
                                {
                                    if (Enum.TryParse<TTrigger>(trigger.ToString(), out var t))
                                    {
                                        stateConfig.PermittedTriggers.Add(t);
                                    }
                                }
                            }
                        }
                    }
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogDebug(ex, "Could not extract state representation details");
        }
    }

    private void ExtractTriggerConfigurationDetails(object triggerConfig, EnhancedStateMachineConfiguration<TState, TTrigger> config)
    {
        // Similar extraction for trigger configuration
        _logger.LogDebug("Extracting trigger configuration details");
    }

    private Task ProbeTransitionsAsync(
        StateMachine<TState, TTrigger> machine,
        EnhancedStateMachineConfiguration<TState, TTrigger> config)
    {
        // Save current state
        var originalState = machine.State;
        
        // For each state, try to discover permitted triggers and transitions
        foreach (var state in Enum.GetValues<TState>())
        {
            if (!config.States.ContainsKey(state))
            {
                config.States[state] = new EnhancedStateConfiguration<TState, TTrigger> { State = state };
            }

            var stateConfig = config.States[state];
            
            // Try to probe this state (this is limited without actually changing state)
            if (machine.State.Equals(state))
            {
                // We can check permitted triggers for current state
                try
                {
                    var permittedTriggers = machine.GetPermittedTriggers();
                    foreach (var trigger in permittedTriggers)
                    {
                        stateConfig.PermittedTriggers.Add(trigger);
                        
                        // Try to determine if trigger would succeed
                        if (machine.CanFire(trigger))
                        {
                            stateConfig.ActivableTriggers.Add(trigger);
                        }
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogDebug(ex, "Could not probe state {State}", state);
                }
            }
        }
        
        return Task.CompletedTask;
    }

    private void ExtractFromDotGraph(
        StateMachine<TState, TTrigger> machine,
        EnhancedStateMachineConfiguration<TState, TTrigger> config)
    {
        try
        {
            // Generate DOT graph
            var dotGraph = GenerateDotGraph(machine);
            if (!string.IsNullOrEmpty(dotGraph))
            {
                config.DotGraphRepresentation = dotGraph;
                ParseDotGraph(dotGraph, config);
            }
        }
        catch (Exception ex)
        {
            _logger.LogDebug(ex, "Could not extract from DOT graph");
        }
    }

    private string GenerateDotGraph(StateMachine<TState, TTrigger> machine)
    {
        try
        {
            // Try to use UmlDotGraph.Format if available
            var info = machine.GetInfo();
            return UmlDotGraph.Format(info);
        }
        catch
        {
            // Fallback to manual generation
            return string.Empty;
        }
    }

    private void ParseDotGraph(string dotGraph, EnhancedStateMachineConfiguration<TState, TTrigger> config)
    {
        // Parse DOT graph to extract transitions
        var lines = dotGraph.Split('\n');
        foreach (var line in lines)
        {
            // Look for transition lines (e.g., "StateA -> StateB [label="Trigger"]")
            if (line.Contains("->") && line.Contains("[label="))
            {
                try
                {
                    var parts = line.Split(new[] { "->", "[label=" }, StringSplitOptions.None);
                    if (parts.Length >= 3)
                    {
                        var fromState = parts[0].Trim().Trim('"');
                        var toState = parts[1].Trim().Split('[')[0].Trim().Trim('"');
                        var trigger = parts[2].Split('"')[1];

                        if (Enum.TryParse<TState>(fromState, out var from) &&
                            Enum.TryParse<TState>(toState, out var to) &&
                            Enum.TryParse<TTrigger>(trigger, out var trig))
                        {
                            if (config.States.TryGetValue(from, out var stateConfig))
                            {
                                var transition = new EnhancedTransitionConfiguration<TState, TTrigger>
                                {
                                    SourceState = from,
                                    DestinationState = to,
                                    Trigger = trig,
                                    HasGuard = line.Contains("guard") || line.Contains("?")
                                };
                                stateConfig.Transitions.Add(transition);
                                
                                // Update transition map
                                var key = (from, trig);
                                if (!config.TransitionMap.ContainsKey(key))
                                {
                                    config.TransitionMap[key] = new List<EnhancedTransitionConfiguration<TState, TTrigger>>();
                                }
                                config.TransitionMap[key].Add(transition);
                            }
                        }
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogDebug(ex, "Could not parse DOT graph line: {Line}", line);
                }
            }
        }
    }

    private void AnalyzeGuardsAndActions(
        StateMachine<TState, TTrigger> machine,
        EnhancedStateMachineConfiguration<TState, TTrigger> config)
    {
        // Analyze guards by checking which triggers have conditional behavior
        foreach (var stateConfig in config.States.Values)
        {
            foreach (var trigger in stateConfig.PermittedTriggers)
            {
                // Check if this trigger has guards by seeing if it's conditionally permitted
                var hasGuard = stateConfig.ActivableTriggers.Contains(trigger) != 
                              stateConfig.PermittedTriggers.Contains(trigger);
                
                if (hasGuard)
                {
                    config.GuardedTriggers.Add((stateConfig.State, trigger));
                }
            }
        }
    }

    private void CalculateMetrics(EnhancedStateMachineConfiguration<TState, TTrigger> config)
    {
        config.Metrics = new StateMachineMetrics
        {
            TotalStates = config.States.Count,
            TotalTransitions = config.TransitionMap.Sum(kvp => kvp.Value.Count),
            TotalGuardedTransitions = config.GuardedTriggers.Count,
            MaxStateDepth = CalculateMaxDepth(config),
            AverageTransitionsPerState = config.States.Count > 0 
                ? (double)config.TransitionMap.Count / config.States.Count 
                : 0,
            CyclomaticComplexity = CalculateCyclomaticComplexity(config)
        };
    }

    private int CalculateMaxDepth(EnhancedStateMachineConfiguration<TState, TTrigger> config)
    {
        int maxDepth = 0;
        foreach (var state in config.States.Values)
        {
            int depth = 0;
            var current = state.Superstate;
            while (current.HasValue)
            {
                depth++;
                current = config.States.TryGetValue(current.Value, out var parent) 
                    ? parent.Superstate 
                    : null;
            }
            maxDepth = Math.Max(maxDepth, depth);
        }
        return maxDepth;
    }

    private int CalculateCyclomaticComplexity(EnhancedStateMachineConfiguration<TState, TTrigger> config)
    {
        // Cyclomatic complexity = E - N + 2P
        // E = edges (transitions), N = nodes (states), P = connected components (usually 1)
        var edges = config.TransitionMap.Sum(kvp => kvp.Value.Count);
        var nodes = config.States.Count;
        var components = 1; // Assuming single connected graph
        
        return edges - nodes + (2 * components);
    }

    /// <summary>
    /// Compares two configurations and provides detailed analysis.
    /// </summary>
    public StateMachineComparison<TState, TTrigger> CompareConfigurations(
        EnhancedStateMachineConfiguration<TState, TTrigger> config1,
        EnhancedStateMachineConfiguration<TState, TTrigger> config2)
    {
        var comparison = new StateMachineComparison<TState, TTrigger>
        {
            Config1 = config1,
            Config2 = config2,
            ComparisonTimestamp = DateTime.UtcNow
        };

        // Compare states
        var states1 = new HashSet<TState>(config1.States.Keys);
        var states2 = new HashSet<TState>(config2.States.Keys);

        comparison.AddedStates = states2.Except(states1).ToList();
        comparison.RemovedStates = states1.Except(states2).ToList();
        comparison.CommonStates = states1.Intersect(states2).ToList();

        // Compare transitions
        foreach (var state in comparison.CommonStates)
        {
            var stateConfig1 = config1.States[state];
            var stateConfig2 = config2.States[state];

            var triggers1 = new HashSet<TTrigger>(stateConfig1.PermittedTriggers);
            var triggers2 = new HashSet<TTrigger>(stateConfig2.PermittedTriggers);

            var addedTriggers = triggers2.Except(triggers1);
            var removedTriggers = triggers1.Except(triggers2);

            foreach (var trigger in addedTriggers)
            {
                comparison.AddedTransitions.Add((state, trigger));
            }

            foreach (var trigger in removedTriggers)
            {
                comparison.RemovedTransitions.Add((state, trigger));
            }

            // Check for modified transitions
            foreach (var trigger in triggers1.Intersect(triggers2))
            {
                var trans1 = stateConfig1.Transitions.Where(t => t.Trigger.Equals(trigger)).ToList();
                var trans2 = stateConfig2.Transitions.Where(t => t.Trigger.Equals(trigger)).ToList();

                if (trans1.Count > 0 && trans2.Count > 0)
                {
                    if (!trans1.First().DestinationState.Equals(trans2.First().DestinationState))
                    {
                        comparison.ModifiedTransitions.Add((state, trigger, 
                            trans1.First().DestinationState, 
                            trans2.First().DestinationState));
                    }
                }
            }
        }

        // Calculate similarity score
        comparison.CalculateSimilarityScore();

        return comparison;
    }

    private TState ParseStateValue(object stateValue)
    {
        if (stateValue is TState state)
            return state;
        
        return (TState)Enum.Parse(typeof(TState), stateValue.ToString() ?? string.Empty);
    }

    private TTrigger ParseTriggerValue(object triggerValue)
    {
        if (triggerValue is TTrigger trigger)
            return trigger;
        
        return (TTrigger)Enum.Parse(typeof(TTrigger), triggerValue.ToString() ?? string.Empty);
    }
}

/// <summary>
/// Enhanced state machine configuration with additional metadata.
/// </summary>
[GenerateSerializer]
public class EnhancedStateMachineConfiguration<TState, TTrigger>
    where TState : struct, Enum
    where TTrigger : struct, Enum
{
    [Id(0)] public Dictionary<TState, EnhancedStateConfiguration<TState, TTrigger>> States { get; set; } = new();
    [Id(1)] public Dictionary<(TState Source, TTrigger Trigger), List<EnhancedTransitionConfiguration<TState, TTrigger>>> TransitionMap { get; set; } = new();
    [Id(2)] public TState InitialState { get; set; }
    [Id(3)] public bool IsValid { get; set; }
    [Id(4)] public DateTime ExtractionTimestamp { get; set; }
    [Id(5)] public List<string> ExtractionErrors { get; set; } = new();
    [Id(6)] public HashSet<(TState State, TTrigger Trigger)> GuardedTriggers { get; set; } = new();
    [Id(7)] public string? DotGraphRepresentation { get; set; }
    [Id(8)] public StateMachineMetrics Metrics { get; set; } = new();
}

/// <summary>
/// Enhanced state configuration with additional details.
/// </summary>
[GenerateSerializer]
public class EnhancedStateConfiguration<TState, TTrigger>
    where TState : struct, Enum
    where TTrigger : struct, Enum
{
    [Id(0)] public TState State { get; set; }
    [Id(1)] public TState? Superstate { get; set; }
    [Id(2)] public List<TState> Substates { get; set; } = new();
    [Id(3)] public HashSet<TTrigger> PermittedTriggers { get; set; } = new();
    [Id(4)] public HashSet<TTrigger> ActivableTriggers { get; set; } = new();
    [Id(5)] public HashSet<TTrigger> IgnoredTriggers { get; set; } = new();
    [Id(6)] public List<EnhancedTransitionConfiguration<TState, TTrigger>> Transitions { get; set; } = new();
    [Id(7)] public bool IsInitialState { get; set; }
    [Id(8)] public Dictionary<string, object> Metadata { get; set; } = new();
}

/// <summary>
/// Enhanced transition configuration with additional metadata.
/// </summary>
[GenerateSerializer]
public class EnhancedTransitionConfiguration<TState, TTrigger>
    where TState : struct, Enum
    where TTrigger : struct, Enum
{
    [Id(0)] public TState SourceState { get; set; }
    [Id(1)] public TState DestinationState { get; set; }
    [Id(2)] public TTrigger Trigger { get; set; }
    [Id(3)] public bool HasGuard { get; set; }
    [Id(4)] public string? GuardDescription { get; set; }
    [Id(5)] public bool IsInternal { get; set; }
    [Id(6)] public bool IsReentrant { get; set; }
}

/// <summary>
/// Metrics about the state machine structure.
/// </summary>
[GenerateSerializer]
public class StateMachineMetrics
{
    [Id(0)] public int TotalStates { get; set; }
    [Id(1)] public int TotalTransitions { get; set; }
    [Id(2)] public int TotalGuardedTransitions { get; set; }
    [Id(3)] public int MaxStateDepth { get; set; }
    [Id(4)] public double AverageTransitionsPerState { get; set; }
    [Id(5)] public int CyclomaticComplexity { get; set; }
}

/// <summary>
/// Result of comparing two state machine configurations.
/// </summary>
[GenerateSerializer]
public class StateMachineComparison<TState, TTrigger>
    where TState : struct, Enum
    where TTrigger : struct, Enum
{
    [Id(0)] public EnhancedStateMachineConfiguration<TState, TTrigger> Config1 { get; set; } = new();
    [Id(1)] public EnhancedStateMachineConfiguration<TState, TTrigger> Config2 { get; set; } = new();
    [Id(2)] public List<TState> AddedStates { get; set; } = new();
    [Id(3)] public List<TState> RemovedStates { get; set; } = new();
    [Id(4)] public List<TState> CommonStates { get; set; } = new();
    [Id(5)] public List<(TState State, TTrigger Trigger)> AddedTransitions { get; set; } = new();
    [Id(6)] public List<(TState State, TTrigger Trigger)> RemovedTransitions { get; set; } = new();
    [Id(7)] public List<(TState State, TTrigger Trigger, TState OldDest, TState NewDest)> ModifiedTransitions { get; set; } = new();
    [Id(8)] public double SimilarityScore { get; set; }
    [Id(9)] public DateTime ComparisonTimestamp { get; set; }

    public void CalculateSimilarityScore()
    {
        var totalElements = Config1.States.Count + Config2.States.Count + 
                          Config1.TransitionMap.Count + Config2.TransitionMap.Count;
        
        if (totalElements == 0)
        {
            SimilarityScore = 1.0;
            return;
        }

        var differences = AddedStates.Count + RemovedStates.Count +
                         AddedTransitions.Count + RemovedTransitions.Count +
                         ModifiedTransitions.Count;

        SimilarityScore = 1.0 - ((double)differences / totalElements);
    }
}

/// <summary>
/// Represents information about a transition.
/// </summary>
public class TransitionInfo<TState, TTrigger>
    where TState : struct, Enum
    where TTrigger : struct, Enum
{
    public TState SourceState { get; set; }
    public TState DestinationState { get; set; }
    public TTrigger Trigger { get; set; }
    public bool HasGuard { get; set; }
    public string? GuardDescription { get; set; }
}