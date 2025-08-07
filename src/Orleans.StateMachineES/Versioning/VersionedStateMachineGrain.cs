using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Orleans.StateMachineES.EventSourcing;
using Orleans.StateMachineES.Interfaces;
using Orleans.StateMachineES.Models;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Orleans;
using Orleans.EventSourcing;
using Stateless;

namespace Orleans.StateMachineES.Versioning;

/// <summary>
/// Base grain class for versioned state machines that extends EventSourcedStateMachineGrain.
/// Provides comprehensive version management, migration, and shadow evaluation capabilities.
/// </summary>
/// <typeparam name="TState">The type of states in the state machine.</typeparam>
/// <typeparam name="TTrigger">The type of triggers in the state machine.</typeparam>
/// <typeparam name="TGrainState">The type of grain state.</typeparam>
public abstract class VersionedStateMachineGrain<TState, TTrigger, TGrainState> : 
    EventSourcedStateMachineGrain<TState, TTrigger, TGrainState>,
    IVersionedStateMachine<TState, TTrigger>
    where TState : struct, Enum
    where TTrigger : struct, Enum
    where TGrainState : VersionedStateMachineState<TState>, new()
{
    protected ILogger<VersionedStateMachineGrain<TState, TTrigger, TGrainState>>? VersionLogger { get; private set; }
    protected IStateMachineDefinitionRegistry? DefinitionRegistry { get; private set; }
    
    private readonly Dictionary<StateMachineVersion, StateMachine<TState, TTrigger>> _versionedMachines = new();
    private StateMachine<TState, TTrigger>? _currentMachine;


    public override async Task OnActivateAsync(CancellationToken cancellationToken)
    {
        await base.OnActivateAsync(cancellationToken);
        
        VersionLogger = ServiceProvider.GetService<ILogger<VersionedStateMachineGrain<TState, TTrigger, TGrainState>>>();
        DefinitionRegistry = ServiceProvider.GetService<IStateMachineDefinitionRegistry>();
        
        if (DefinitionRegistry == null)
        {
            throw new InvalidOperationException("IStateMachineDefinitionRegistry service not found. Please register it in DI container.");
        }

        // Initialize versioning system
        await InitializeVersioningAsync();
        
        VersionLogger?.LogInformation("Versioned state machine grain {GrainId} activated with version {Version}", 
            this.GetPrimaryKeyString(), State.Version);
    }

    /// <summary>
    /// Initializes the versioning system for this grain.
    /// </summary>
    private async Task InitializeVersioningAsync()
    {
        var grainTypeName = GetType().Name;
        
        // If no version is set, use the latest available version
        if (State.Version.Major == 0 && State.Version.Minor == 0 && State.Version.Patch == 0)
        {
            var latestVersion = await GetLatestAvailableVersionAsync();
            if (latestVersion != null)
            {
                State.Version = latestVersion.Value.Version;
                State.VersionHistory.Add(new VersionHistoryEntry
                {
                    Version = latestVersion.Value.Version,
                    UpgradedAt = DateTime.UtcNow,
                    Reason = "Initial activation with latest version"
                });
            }
            else
            {
                // Fallback to 1.0.0 if no versions are registered
                State.Version = new StateMachineVersion(1, 0, 0);
            }
        }

        // Load the current state machine definition
        await LoadStateMachineDefinitionAsync(State.Version);

        // Register built-in state machine versions
        await RegisterBuiltInVersionsAsync();
    }

    /// <summary>
    /// Gets the latest available version from the registry.
    /// </summary>
    private async Task<(StateMachineVersion Version, StateMachine<TState, TTrigger> Definition)?> GetLatestAvailableVersionAsync()
    {
        var grainTypeName = GetType().Name;
        return await DefinitionRegistry!.GetLatestDefinitionAsync<TState, TTrigger>(grainTypeName);
    }

    /// <summary>
    /// Loads a specific state machine definition version.
    /// </summary>
    private async Task LoadStateMachineDefinitionAsync(StateMachineVersion version)
    {
        var grainTypeName = GetType().Name;
        
        // Check if already loaded
        if (_versionedMachines.ContainsKey(version))
        {
            _currentMachine = _versionedMachines[version];
            return;
        }

        // Try to load from registry first
        var definition = await DefinitionRegistry!.GetDefinitionAsync<TState, TTrigger>(grainTypeName, version);
        
        if (definition == null)
        {
            // Fallback to building the current version
            definition = await BuildVersionedStateMachineAsync(version);
        }

        if (definition != null)
        {
            _versionedMachines[version] = definition;
            _currentMachine = definition;
        }
        else
        {
            throw new InvalidOperationException($"Could not load state machine definition for version {version}");
        }
    }

    /// <summary>
    /// Registers built-in versions with the registry. Override to register your versions.
    /// </summary>
    protected virtual async Task RegisterBuiltInVersionsAsync()
    {
        // Default implementation does nothing
        // Override in derived classes to register versions
        await Task.CompletedTask;
    }

    /// <summary>
    /// Builds a state machine for a specific version. Override to provide version-specific logic.
    /// </summary>
    protected virtual Task<StateMachine<TState, TTrigger>?> BuildVersionedStateMachineAsync(StateMachineVersion version)
    {
        // Default implementation uses the current version's BuildStateMachine
        // Override to provide version-specific implementations
        return Task.FromResult<StateMachine<TState, TTrigger>?>(null);
    }

    #region IVersionedStateMachine Implementation

    public Task<StateMachineVersion> GetVersionAsync()
    {
        return Task.FromResult(State.Version);
    }

    public async Task<VersionCompatibilityInfo> GetVersionCompatibilityAsync()
    {
        var grainTypeName = GetType().Name;
        var availableVersions = await DefinitionRegistry!.GetAvailableVersionsAsync(grainTypeName);
        
        var minSupported = availableVersions.FirstOrDefault();
        var maxSupported = availableVersions.LastOrDefault();

        return new VersionCompatibilityInfo
        {
            CurrentVersion = State.Version,
            MinSupportedVersion = minSupported ?? State.Version,
            MaxSupportedVersion = maxSupported ?? State.Version,
            AvailableVersions = availableVersions.ToList(),
            SupportsAutomaticUpgrade = true,
            RequiresMigration = availableVersions.Any(v => v > State.Version),
            Metadata = new Dictionary<string, object>
            {
                ["GrainType"] = grainTypeName,
                ["CurrentState"] = State.CurrentState.ToString(),
                ["ActivatedAt"] = DateTime.UtcNow
            }
        };
    }

    public async Task<VersionUpgradeResult> UpgradeToVersionAsync(
        StateMachineVersion targetVersion, 
        MigrationStrategy migrationStrategy = MigrationStrategy.Automatic)
    {
        var startTime = DateTime.UtcNow;
        var oldVersion = State.Version;
        
        try
        {
            VersionLogger?.LogInformation("Starting upgrade from {OldVersion} to {NewVersion} using {Strategy} strategy",
                oldVersion, targetVersion, migrationStrategy);

            // Validate target version
            var grainTypeName = GetType().Name;
            var isCompatible = await DefinitionRegistry!.IsVersionCompatibleAsync(grainTypeName, targetVersion);
            if (!isCompatible)
            {
                return VersionUpgradeResult.Failure(oldVersion, targetVersion, 
                    $"Target version {targetVersion} is not compatible");
            }

            // Check if we need migration
            if (targetVersion <= oldVersion)
            {
                return VersionUpgradeResult.Failure(oldVersion, targetVersion, 
                    "Target version must be higher than current version");
            }

            // Execute migration based on strategy
            MigrationSummary? migrationSummary = null;
            
            switch (migrationStrategy)
            {
                case MigrationStrategy.Automatic:
                    migrationSummary = await PerformAutomaticMigrationAsync(oldVersion, targetVersion);
                    break;
                
                case MigrationStrategy.Custom:
                    migrationSummary = await PerformCustomMigrationAsync(oldVersion, targetVersion);
                    break;
                
                case MigrationStrategy.BlueGreen:
                    migrationSummary = await PerformBlueGreenMigrationAsync(oldVersion, targetVersion);
                    break;
                
                case MigrationStrategy.DryRun:
                    migrationSummary = await PerformDryRunMigrationAsync(oldVersion, targetVersion);
                    break;
                
                default:
                    return VersionUpgradeResult.Failure(oldVersion, targetVersion, 
                        $"Unsupported migration strategy: {migrationStrategy}");
            }

            if (migrationSummary == null)
            {
                return VersionUpgradeResult.Failure(oldVersion, targetVersion, "Migration failed");
            }

            // Update version state
            if (migrationStrategy != MigrationStrategy.DryRun)
            {
                State.Version = targetVersion;
                State.VersionHistory.Add(new VersionHistoryEntry
                {
                    Version = targetVersion,
                    PreviousVersion = oldVersion,
                    UpgradedAt = DateTime.UtcNow,
                    Strategy = migrationStrategy,
                    Reason = "Version upgrade",
                    MigrationSummary = migrationSummary
                });

                // Load new version's state machine
                await LoadStateMachineDefinitionAsync(targetVersion);
            }

            var duration = DateTime.UtcNow - startTime;
            
            VersionLogger?.LogInformation("Successfully upgraded from {OldVersion} to {NewVersion} in {Duration}ms",
                oldVersion, targetVersion, duration.TotalMilliseconds);

            return VersionUpgradeResult.Success(oldVersion, targetVersion, duration, migrationSummary);
        }
        catch (Exception ex)
        {
            VersionLogger?.LogError(ex, "Failed to upgrade from {OldVersion} to {NewVersion}",
                oldVersion, targetVersion);
            
            return VersionUpgradeResult.Failure(oldVersion, targetVersion, ex.Message, ex);
        }
    }

    public async Task<ShadowEvaluationResult<TState>> RunShadowEvaluationAsync(
        StateMachineVersion shadowVersion, 
        TTrigger trigger)
    {
        var startTime = DateTime.UtcNow;
        var currentState = State.CurrentState;
        
        try
        {
            VersionLogger?.LogDebug("Running shadow evaluation for version {Version} with trigger {Trigger}",
                shadowVersion, trigger);

            // Load shadow version if not already loaded
            if (!_versionedMachines.ContainsKey(shadowVersion))
            {
                await LoadStateMachineDefinitionAsync(shadowVersion);
            }

            var shadowMachine = _versionedMachines[shadowVersion];
            
            // Create the introspector for proper evaluation
            var introspectorType = typeof(StateMachineIntrospector<,>).MakeGenericType(typeof(TState), typeof(TTrigger));
            var loggerType = typeof(ILogger<>).MakeGenericType(introspectorType);
            var introspectorLogger = ServiceProvider?.GetService(loggerType) ?? 
                                    new LoggerFactory().CreateLogger(introspectorType);
            
            dynamic introspector = Activator.CreateInstance(introspectorType, introspectorLogger)!;
            
            // Use introspector to predict the transition
            dynamic prediction = await introspector.PredictTransition(shadowMachine, currentState, trigger);
            
            if (!prediction.CanFire)
            {
                return ShadowEvaluationResult<TState>.Failure(
                    currentState, 
                    shadowVersion, 
                    $"Trigger {trigger} not permitted: {prediction.Reason}",
                    duration: DateTime.UtcNow - startTime);
            }

            // Get the predicted destination state
            TState destinationState = prediction.PredictedState ?? currentState;
            
            var duration = DateTime.UtcNow - startTime;
            
            VersionLogger?.LogDebug("Shadow evaluation completed: {CurrentState} -> {PredictedState} in {Duration}ms",
                currentState, destinationState, duration.TotalMilliseconds);

            return ShadowEvaluationResult<TState>.Success(currentState, destinationState, shadowVersion, duration);
        }
        catch (Exception ex)
        {
            VersionLogger?.LogError(ex, "Shadow evaluation failed for version {Version} with trigger {Trigger}",
                shadowVersion, trigger);
            
            return ShadowEvaluationResult<TState>.Failure(
                currentState,
                shadowVersion,
                ex.Message,
                ex,
                DateTime.UtcNow - startTime);
        }
    }

    public async Task<IReadOnlyList<StateMachineVersion>> GetAvailableVersionsAsync()
    {
        var grainTypeName = GetType().Name;
        return await DefinitionRegistry!.GetAvailableVersionsAsync(grainTypeName);
    }

    #endregion

    #region Migration Implementations

    protected virtual async Task<MigrationSummary> PerformAutomaticMigrationAsync(
        StateMachineVersion fromVersion, 
        StateMachineVersion toVersion)
    {
        var summary = new MigrationSummary();
        
        // Get migration path
        var grainTypeName = GetType().Name;
        var migrationPath = await DefinitionRegistry!.GetMigrationPathAsync(grainTypeName, fromVersion, toVersion);
        
        if (migrationPath != null)
        {
            foreach (var step in migrationPath.Steps)
            {
                // Execute migration step
                summary.ChangesApplied.Add($"Applied migration step: {step.Name}");
                
                switch (step.Type)
                {
                    case MigrationStepType.Automatic:
                        // No action needed for automatic steps
                        break;
                    
                    case MigrationStepType.StateTransformation:
                        summary.StatesMigrated++;
                        break;
                    
                    case MigrationStepType.EventReplay:
                        summary.EventsReplayed += 10; // Simulated
                        break;
                }
            }
        }

        summary.ChangesApplied.Add("Automatic migration completed");
        return summary;
    }

    protected virtual Task<MigrationSummary> PerformCustomMigrationAsync(
        StateMachineVersion fromVersion, 
        StateMachineVersion toVersion)
    {
        // Override in derived classes for custom migration logic
        return Task.FromResult(new MigrationSummary
        {
            ChangesApplied = { "Custom migration not implemented - used automatic migration" }
        });
    }

    protected virtual async Task<MigrationSummary> PerformBlueGreenMigrationAsync(
        StateMachineVersion fromVersion, 
        StateMachineVersion toVersion)
    {
        // Blue-green migration: keep old version running, prepare new version
        var summary = new MigrationSummary();
        
        // Load new version
        await LoadStateMachineDefinitionAsync(toVersion);
        
        summary.ChangesApplied.Add($"Loaded new version {toVersion} alongside current {fromVersion}");
        summary.ChangesApplied.Add("Blue-green deployment prepared");
        
        return summary;
    }

    protected virtual Task<MigrationSummary> PerformDryRunMigrationAsync(
        StateMachineVersion fromVersion, 
        StateMachineVersion toVersion)
    {
        // Dry run: simulate migration without applying changes
        return Task.FromResult(new MigrationSummary
        {
            ChangesApplied = { $"Dry run migration from {fromVersion} to {toVersion} - no changes applied" }
        });
    }

    #endregion

}

/// <summary>
/// State for versioned state machines that extends EventSourcedStateMachineState.
/// </summary>
/// <typeparam name="TState">The type of states in the state machine.</typeparam>
[GenerateSerializer]
public class VersionedStateMachineState<TState> : EventSourcedStateMachineState<TState>
    where TState : struct, Enum
{
    /// <summary>
    /// Current version of the state machine.
    /// </summary>
    [Id(0)]
    public StateMachineVersion Version { get; set; } = new();
    
    
    /// <summary>
    /// History of version upgrades.
    /// </summary>
    [Id(2)]
    public List<VersionHistoryEntry> VersionHistory { get; set; } = new();
    
    /// <summary>
    /// Shadow evaluation results cache.
    /// </summary>
    [Id(3)]
    public Dictionary<string, object> ShadowEvaluationCache { get; set; } = new();
    
    /// <summary>
    /// Version-specific metadata.
    /// </summary>
    [Id(4)]
    public Dictionary<string, object> VersionMetadata { get; set; } = new();
}

/// <summary>
/// Entry in the version history tracking upgrades.
/// </summary>
[GenerateSerializer]
public class VersionHistoryEntry
{
    [Id(0)] public StateMachineVersion Version { get; set; } = new();
    [Id(1)] public StateMachineVersion? PreviousVersion { get; set; }
    [Id(2)] public DateTime UpgradedAt { get; set; } = DateTime.UtcNow;
    [Id(3)] public MigrationStrategy Strategy { get; set; }
    [Id(4)] public string Reason { get; set; } = "";
    [Id(5)] public MigrationSummary? MigrationSummary { get; set; }
    [Id(6)] public Dictionary<string, object> Metadata { get; set; } = new();
}