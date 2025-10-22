using Orleans.StateMachineES.Interfaces;
using StateMachineVersion = Orleans.StateMachineES.Abstractions.Models.StateMachineVersion;

namespace Orleans.StateMachineES.Versioning;

/// <summary>
/// Interface for versioned state machines that can evolve and be upgraded safely.
/// </summary>
/// <typeparam name="TState">The type of states in the state machine.</typeparam>
/// <typeparam name="TTrigger">The type of triggers in the state machine.</typeparam>
public interface IVersionedStateMachine<TState, TTrigger> : IStateMachineGrain<TState, TTrigger>
    where TState : struct, Enum
    where TTrigger : struct, Enum
{
    /// <summary>
    /// Gets the current version of the state machine definition.
    /// </summary>
    Task<StateMachineVersion> GetVersionAsync();
    
    /// <summary>
    /// Gets the version compatibility information for this grain.
    /// </summary>
    Task<VersionCompatibilityInfo> GetVersionCompatibilityAsync();
    
    /// <summary>
    /// Attempts to upgrade the state machine to a newer version.
    /// </summary>
    /// <param name="targetVersion">The version to upgrade to.</param>
    /// <param name="migrationStrategy">The migration strategy to use.</param>
    /// <returns>The result of the upgrade operation.</returns>
    Task<VersionUpgradeResult> UpgradeToVersionAsync(StateMachineVersion targetVersion, MigrationStrategy migrationStrategy = MigrationStrategy.Automatic);
    
    /// <summary>
    /// Runs a shadow evaluation of a new state machine version without committing state changes.
    /// </summary>
    /// <param name="shadowVersion">The version to evaluate in shadow mode.</param>
    /// <param name="trigger">The trigger to evaluate.</param>
    /// <returns>The result of the shadow evaluation.</returns>
    Task<ShadowEvaluationResult<TState>> RunShadowEvaluationAsync(StateMachineVersion shadowVersion, TTrigger trigger);
    
    /// <summary>
    /// Gets the available state machine versions for this grain type.
    /// </summary>
    Task<IReadOnlyList<StateMachineVersion>> GetAvailableVersionsAsync();
}

/// <summary>
/// Information about version compatibility for a state machine grain.
/// </summary>
[GenerateSerializer]
[Alias("Orleans.StateMachineES.Versioning.VersionCompatibilityInfo")]
public class VersionCompatibilityInfo
{
    [Id(0)] public StateMachineVersion CurrentVersion { get; set; } = new(1, 0, 0);
    [Id(1)] public StateMachineVersion MinSupportedVersion { get; set; } = new(1, 0, 0);
    [Id(2)] public StateMachineVersion MaxSupportedVersion { get; set; } = new(1, 0, 0);
    [Id(3)] public List<StateMachineVersion> AvailableVersions { get; set; } = [];
    [Id(4)] public bool SupportsAutomaticUpgrade { get; set; }
    [Id(5)] public bool RequiresMigration { get; set; }
    [Id(6)] public Dictionary<string, object> Metadata { get; set; } = [];
}

/// <summary>
/// Result of a version upgrade operation.
/// </summary>
[GenerateSerializer]
[Alias("Orleans.StateMachineES.Versioning.VersionUpgradeResult")]
public class VersionUpgradeResult
{
    [Id(0)] public bool IsSuccess { get; set; }
    [Id(1)] public StateMachineVersion? OldVersion { get; set; }
    [Id(2)] public StateMachineVersion? NewVersion { get; set; }
    [Id(3)] public string? ErrorMessage { get; set; }
    [Id(4)] public Exception? Exception { get; set; }
    [Id(5)] public DateTime UpgradeTime { get; set; } = DateTime.UtcNow;
    [Id(6)] public TimeSpan UpgradeDuration { get; set; }
    [Id(7)] public MigrationSummary? MigrationSummary { get; set; }
    [Id(8)] public Dictionary<string, object> Metadata { get; set; } = [];

    public static VersionUpgradeResult Success(StateMachineVersion oldVersion, StateMachineVersion newVersion, TimeSpan duration, MigrationSummary? summary = null)
    {
        return new VersionUpgradeResult
        {
            IsSuccess = true,
            OldVersion = oldVersion,
            NewVersion = newVersion,
            UpgradeDuration = duration,
            MigrationSummary = summary
        };
    }

    public static VersionUpgradeResult Failure(StateMachineVersion? oldVersion, StateMachineVersion? targetVersion, string errorMessage, Exception? exception = null)
    {
        return new VersionUpgradeResult
        {
            IsSuccess = false,
            OldVersion = oldVersion,
            NewVersion = targetVersion,
            ErrorMessage = errorMessage,
            Exception = exception
        };
    }
}

/// <summary>
/// Summary of what was migrated during a version upgrade.
/// </summary>
[GenerateSerializer]
[Alias("Orleans.StateMachineES.Versioning.MigrationSummary")]
public class MigrationSummary
{
    [Id(0)] public int StatesMigrated { get; set; }
    [Id(1)] public int TransitionsUpdated { get; set; }
    [Id(2)] public int GuardsModified { get; set; }
    [Id(3)] public int EventsReplayed { get; set; }
    [Id(4)] public List<string> ChangesApplied { get; set; } = [];
    [Id(5)] public Dictionary<string, object> Statistics { get; set; } = [];
}

/// <summary>
/// Strategy for migrating state machines during version upgrades.
/// </summary>
public enum MigrationStrategy
{
    /// <summary>
    /// Attempt automatic migration using built-in rules.
    /// </summary>
    Automatic,
    
    /// <summary>
    /// Use custom migration hooks defined by the implementer.
    /// </summary>
    Custom,
    
    /// <summary>
    /// Create a new instance and deprecate the old one.
    /// </summary>
    BlueGreen,
    
    /// <summary>
    /// Perform a dry-run migration to validate compatibility.
    /// </summary>
    DryRun
}

/// <summary>
/// Result of evaluating a state machine trigger in shadow mode.
/// </summary>
[GenerateSerializer]
[Alias("Orleans.StateMachineES.Versioning.ShadowEvaluationResult`1")]
public class ShadowEvaluationResult<TState>
    where TState : struct, Enum
{
    [Id(0)] public bool WouldSucceed { get; set; }
    [Id(1)] public TState? PredictedState { get; set; }
    [Id(2)] public TState CurrentState { get; set; }
    [Id(3)] public string? ErrorMessage { get; set; }
    [Id(4)] public Exception? Exception { get; set; }
    [Id(5)] public StateMachineVersion EvaluatedVersion { get; set; } = new(1, 0, 0);
    [Id(6)] public TimeSpan EvaluationDuration { get; set; }
    [Id(7)] public Dictionary<string, object> Metadata { get; set; } = [];

    public static ShadowEvaluationResult<TState> Success(TState currentState, TState predictedState, StateMachineVersion version, TimeSpan duration)
    {
        return new ShadowEvaluationResult<TState>
        {
            WouldSucceed = true,
            CurrentState = currentState,
            PredictedState = predictedState,
            EvaluatedVersion = version,
            EvaluationDuration = duration
        };
    }

    public static ShadowEvaluationResult<TState> Failure(TState currentState, StateMachineVersion version, string errorMessage, Exception? exception = null, TimeSpan duration = default)
    {
        return new ShadowEvaluationResult<TState>
        {
            WouldSucceed = false,
            CurrentState = currentState,
            EvaluatedVersion = version,
            ErrorMessage = errorMessage,
            Exception = exception,
            EvaluationDuration = duration
        };
    }
}