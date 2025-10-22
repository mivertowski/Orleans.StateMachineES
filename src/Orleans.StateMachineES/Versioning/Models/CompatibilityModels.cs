using StateMachineVersion = Orleans.StateMachineES.Abstractions.Models.StateMachineVersion;
using RiskLevel = Orleans.StateMachineES.Abstractions.Models.RiskLevel;

namespace Orleans.StateMachineES.Versioning;

/// <summary>
/// Context for compatibility evaluation.
/// </summary>
public sealed class CompatibilityContext
{
    /// <summary>
    /// Gets or sets the source version.
    /// </summary>
    public StateMachineVersion FromVersion { get; set; } = null!;

    /// <summary>
    /// Gets or sets the target version.
    /// </summary>
    public StateMachineVersion ToVersion { get; set; } = null!;

    /// <summary>
    /// Gets or sets the grain type name.
    /// </summary>
    public string GrainTypeName { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets state changes between versions.
    /// </summary>
    public StateChanges? StateChanges { get; set; }

    /// <summary>
    /// Gets or sets trigger changes between versions.
    /// </summary>
    public TriggerChanges? TriggerChanges { get; set; }

    /// <summary>
    /// Gets or sets guard condition changes.
    /// </summary>
    public List<GuardChange>? GuardChanges { get; set; }

    /// <summary>
    /// Gets or sets transition changes.
    /// </summary>
    public List<TransitionChange>? TransitionChanges { get; set; }

    /// <summary>
    /// Gets or sets whether data format has changed.
    /// </summary>
    public bool DataFormatChanged { get; set; }

    /// <summary>
    /// Gets or sets whether data migration is required.
    /// </summary>
    public bool RequiresDataMigration { get; set; }

    /// <summary>
    /// Gets or sets the migration complexity.
    /// </summary>
    public MigrationComplexity MigrationComplexity { get; set; }

    /// <summary>
    /// Gets or sets additional metadata.
    /// </summary>
    public Dictionary<string, object>? Metadata { get; set; }
}

/// <summary>
/// Result of compatibility evaluation.
/// </summary>
public sealed class CompatibilityResult
{
    /// <summary>
    /// Gets or sets the source version.
    /// </summary>
    public StateMachineVersion FromVersion { get; set; } = null!;

    /// <summary>
    /// Gets or sets the target version.
    /// </summary>
    public StateMachineVersion ToVersion { get; set; } = null!;

    /// <summary>
    /// Gets or sets whether versions are compatible.
    /// </summary>
    public bool IsCompatible { get; set; }

    /// <summary>
    /// Gets or sets the compatibility level.
    /// </summary>
    public VersionCompatibilityLevel CompatibilityLevel { get; set; }

    /// <summary>
    /// Gets the list of rule results.
    /// </summary>
    public List<RuleResult> RuleResults { get; } = [];

    /// <summary>
    /// Gets the list of breaking changes.
    /// </summary>
    public List<BreakingChange> BreakingChanges { get; } = [];

    /// <summary>
    /// Gets the list of warnings.
    /// </summary>
    public List<string> Warnings { get; } = [];

    /// <summary>
    /// Gets or sets required migration steps.
    /// </summary>
    public List<MigrationStep>? RequiredMigrationSteps { get; set; }

    /// <summary>
    /// Gets or sets the evaluation time.
    /// </summary>
    public DateTime EvaluationTime { get; set; }
}

/// <summary>
/// Represents state changes between versions.
/// </summary>
public sealed class StateChanges
{
    /// <summary>
    /// Gets the list of added states.
    /// </summary>
    public List<string> AddedStates { get; } = [];

    /// <summary>
    /// Gets the list of removed states.
    /// </summary>
    public List<string> RemovedStates { get; } = [];

    /// <summary>
    /// Gets the list of modified states.
    /// </summary>
    public List<string> ModifiedStates { get; } = [];

    /// <summary>
    /// Gets whether there are any changes.
    /// </summary>
    public bool HasChanges => AddedStates.Any() || RemovedStates.Any() || ModifiedStates.Any();
}

/// <summary>
/// Represents trigger changes between versions.
/// </summary>
public sealed class TriggerChanges
{
    /// <summary>
    /// Gets the list of added triggers.
    /// </summary>
    public List<string> AddedTriggers { get; } = [];

    /// <summary>
    /// Gets the list of removed triggers.
    /// </summary>
    public List<string> RemovedTriggers { get; } = [];

    /// <summary>
    /// Gets the list of modified triggers.
    /// </summary>
    public List<string> ModifiedTriggers { get; } = [];

    /// <summary>
    /// Gets whether there are any changes.
    /// </summary>
    public bool HasChanges => AddedTriggers.Any() || RemovedTriggers.Any() || ModifiedTriggers.Any();
}

/// <summary>
/// Represents a guard condition change.
/// </summary>
public sealed class GuardChange
{
    /// <summary>
    /// Gets or sets the state where the guard changed.
    /// </summary>
    public string State { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the trigger associated with the guard.
    /// </summary>
    public string Trigger { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the type of change.
    /// </summary>
    public GuardChangeType ChangeType { get; set; }

    /// <summary>
    /// Gets or sets the description of the change.
    /// </summary>
    public string Description { get; set; } = string.Empty;
}

/// <summary>
/// Type of guard condition change.
/// </summary>
public enum GuardChangeType
{
    /// <summary>
    /// Guard was added.
    /// </summary>
    Added,
    
    /// <summary>
    /// Guard was removed.
    /// </summary>
    Removed,
    
    /// <summary>
    /// Guard condition was modified.
    /// </summary>
    Modified
}

/// <summary>
/// Represents a transition change.
/// </summary>
public sealed class TransitionChange
{
    /// <summary>
    /// Gets or sets the source state.
    /// </summary>
    public string FromState { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the trigger.
    /// </summary>
    public string Trigger { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the original target state.
    /// </summary>
    public string? OriginalToState { get; set; }

    /// <summary>
    /// Gets or sets the new target state.
    /// </summary>
    public string? NewToState { get; set; }

    /// <summary>
    /// Gets or sets whether this is a removal.
    /// </summary>
    public bool IsRemoval { get; set; }

    /// <summary>
    /// Gets or sets whether this is an addition.
    /// </summary>
    public bool IsAddition { get; set; }

    /// <summary>
    /// Gets whether the target state changed.
    /// </summary>
    public bool IsTargetStateChange => 
        !IsRemoval && !IsAddition && OriginalToState != NewToState;

    /// <summary>
    /// Gets or sets the description.
    /// </summary>
    public string Description { get; set; } = string.Empty;
}

/// <summary>
/// Represents a breaking change in compatibility.
/// </summary>
public sealed class BreakingChange
{
    /// <summary>
    /// Gets or sets the type of breaking change.
    /// </summary>
    public BreakingChangeType ChangeType { get; set; }

    /// <summary>
    /// Gets or sets the description of the change.
    /// </summary>
    public string Description { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the impact level.
    /// </summary>
    public BreakingChangeImpact Impact { get; set; }

    /// <summary>
    /// Gets or sets the mitigation strategy.
    /// </summary>
    public string Mitigation { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets additional details.
    /// </summary>
    public Dictionary<string, object>? Details { get; set; }
}

/// <summary>
/// Type of breaking change.
/// </summary>
public enum BreakingChangeType
{
    /// <summary>
    /// State was removed.
    /// </summary>
    StateRemoved,
    
    /// <summary>
    /// State was added.
    /// </summary>
    StateAdded,
    
    /// <summary>
    /// Trigger was removed.
    /// </summary>
    TriggerRemoved,
    
    /// <summary>
    /// Trigger was added.
    /// </summary>
    TriggerAdded,
    
    /// <summary>
    /// Transition was changed.
    /// </summary>
    TransitionChanged,
    
    /// <summary>
    /// Guard condition was changed.
    /// </summary>
    GuardChanged,
    
    /// <summary>
    /// Data format was changed.
    /// </summary>
    DataFormatChanged,
    
    /// <summary>
    /// Major version increase.
    /// </summary>
    MajorVersionIncrease,
    
    /// <summary>
    /// Downgrade attempt.
    /// </summary>
    DowngradeAttempt,
    
    /// <summary>
    /// Complex migration required.
    /// </summary>
    ComplexMigration
}

/// <summary>
/// Impact level of a breaking change.
/// </summary>
public enum BreakingChangeImpact
{
    /// <summary>
    /// Low impact change.
    /// </summary>
    Low,
    
    /// <summary>
    /// Medium impact change.
    /// </summary>
    Medium,
    
    /// <summary>
    /// High impact change.
    /// </summary>
    High,
    
    /// <summary>
    /// Critical impact change.
    /// </summary>
    Critical
}

/// <summary>
/// Represents a migration path between versions.
/// </summary>
public sealed class MigrationPath
{
    /// <summary>
    /// Gets or sets the starting version.
    /// </summary>
    public StateMachineVersion FromVersion { get; set; } = null!;

    /// <summary>
    /// Gets or sets the target version.
    /// </summary>
    public StateMachineVersion ToVersion { get; set; } = null!;

    /// <summary>
    /// Gets the list of migration steps.
    /// </summary>
    public List<MigrationStep> Steps { get; set; } = [];

    /// <summary>
    /// Gets or sets the total cost of the migration.
    /// </summary>
    public double TotalCost { get; set; }

    /// <summary>
    /// Gets or sets the total risk level.
    /// </summary>
    public RiskLevel TotalRisk { get; set; }

    /// <summary>
    /// Gets or sets whether this is a direct path.
    /// </summary>
    public bool IsDirectPath { get; set; }

    /// <summary>
    /// Gets or sets the estimated duration.
    /// </summary>
    public TimeSpan EstimatedDuration { get; set; }

    /// <summary>
    /// Gets or sets the reason if no path is available.
    /// </summary>
    public string? NoPathReason { get; set; }

    /// <summary>
    /// Creates a "no path" result.
    /// </summary>
    public static MigrationPath NoPath(StateMachineVersion from, StateMachineVersion to, string reason)
    {
        return new MigrationPath
        {
            FromVersion = from,
            ToVersion = to,
            NoPathReason = reason
        };
    }

    /// <summary>
    /// Updates the estimated duration based on the migration steps.
    /// </summary>
    public void UpdateEstimatedDuration()
    {
        var totalMinutes = 0;
        
        foreach (var step in Steps)
        {
            totalMinutes += step.EstimatedEffort switch
            {
                MigrationEffort.Low => 30,
                MigrationEffort.Medium => 120,
                MigrationEffort.High => 480,
                _ => 60
            };
        }
        
        EstimatedDuration = TimeSpan.FromMinutes(totalMinutes);
    }
}

/// <summary>
/// Represents a step in a migration path.
/// </summary>
public sealed class MigrationStep
{
    /// <summary>
    /// Gets or sets the step order.
    /// </summary>
    public int Order { get; set; }

    /// <summary>
    /// Gets or sets the source version for this step.
    /// </summary>
    public StateMachineVersion? FromVersion { get; set; }

    /// <summary>
    /// Gets or sets the target version for this step.
    /// </summary>
    public StateMachineVersion? ToVersion { get; set; }

    /// <summary>
    /// Gets or sets the migration type.
    /// </summary>
    public MigrationType Type { get; set; }

    /// <summary>
    /// Gets or sets the step description.
    /// </summary>
    public string Description { get; set; } = string.Empty;

    /// <summary>
    /// Gets the list of required actions.
    /// </summary>
    public List<string> RequiredActions { get; set; } = [];

    /// <summary>
    /// Gets the list of validation steps.
    /// </summary>
    public List<string> ValidationSteps { get; set; } = [];

    /// <summary>
    /// Gets or sets the estimated effort.
    /// </summary>
    public MigrationEffort EstimatedEffort { get; set; }

    /// <summary>
    /// Gets or sets the risk level.
    /// </summary>
    public RiskLevel RiskLevel { get; set; }

    /// <summary>
    /// Gets or sets whether automation is available.
    /// </summary>
    public bool AutomationAvailable { get; set; }
}

/// <summary>
/// Type of migration.
/// </summary>
public enum MigrationType
{
    /// <summary>
    /// State removal migration.
    /// </summary>
    StateRemoval,
    
    /// <summary>
    /// State addition migration.
    /// </summary>
    StateAddition,
    
    /// <summary>
    /// Trigger removal migration.
    /// </summary>
    TriggerRemoval,
    
    /// <summary>
    /// Trigger addition migration.
    /// </summary>
    TriggerAddition,
    
    /// <summary>
    /// Transition modification migration.
    /// </summary>
    TransitionModification,
    
    /// <summary>
    /// Guard modification migration.
    /// </summary>
    GuardModification,
    
    /// <summary>
    /// Data migration.
    /// </summary>
    DataMigration,
    
    /// <summary>
    /// Major version upgrade.
    /// </summary>
    MajorUpgrade,
    
    /// <summary>
    /// Minor version upgrade.
    /// </summary>
    MinorUpgrade,
    
    /// <summary>
    /// Patch update.
    /// </summary>
    PatchUpdate,
    
    /// <summary>
    /// Custom migration.
    /// </summary>
    Custom
}

/// <summary>
/// Migration effort level.
/// </summary>
public enum MigrationEffort
{
    /// <summary>
    /// Low effort required.
    /// </summary>
    Low,
    
    /// <summary>
    /// Medium effort required.
    /// </summary>
    Medium,
    
    /// <summary>
    /// High effort required.
    /// </summary>
    High
}

/// <summary>
/// Migration complexity level.
/// </summary>
public enum MigrationComplexity
{
    /// <summary>
    /// Simple migration.
    /// </summary>
    Simple,
    
    /// <summary>
    /// Moderate migration.
    /// </summary>
    Moderate,
    
    /// <summary>
    /// Complex migration.
    /// </summary>
    Complex,
    
    /// <summary>
    /// High complexity migration.
    /// </summary>
    High
}

/// <summary>
/// Represents a migration rule between two versions.
/// </summary>
public sealed class MigrationRule
{
    /// <summary>
    /// Gets or sets the source version.
    /// </summary>
    public StateMachineVersion FromVersion { get; set; } = null!;

    /// <summary>
    /// Gets or sets the target version.
    /// </summary>
    public StateMachineVersion ToVersion { get; set; } = null!;

    /// <summary>
    /// Gets or sets the migration step.
    /// </summary>
    public MigrationStep Step { get; set; } = null!;

    /// <summary>
    /// Gets or sets the rule priority.
    /// </summary>
    public int Priority { get; set; }

    /// <summary>
    /// Gets or sets whether the rule is enabled.
    /// </summary>
    public bool IsEnabled { get; set; } = true;

    /// <summary>
    /// Gets or sets the rule description.
    /// </summary>
    public string Description { get; set; } = string.Empty;
}