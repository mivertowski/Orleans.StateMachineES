using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Orleans;
using Orleans.StateMachineES.Interfaces;

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
/// Represents a state machine version with semantic versioning.
/// </summary>
[GenerateSerializer]
public class StateMachineVersion : IComparable<StateMachineVersion>
{
    [Id(0)] public int Major { get; set; }
    [Id(1)] public int Minor { get; set; }
    [Id(2)] public int Patch { get; set; }
    [Id(3)] public string? PreRelease { get; set; }
    [Id(4)] public string? BuildMetadata { get; set; }
    [Id(5)] public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    [Id(6)] public string Description { get; set; } = "";
    [Id(7)] public Dictionary<string, object> Metadata { get; set; } = new();

    public StateMachineVersion() { }

    public StateMachineVersion(int major, int minor, int patch, string? preRelease = null, string? buildMetadata = null)
    {
        Major = major;
        Minor = minor;
        Patch = patch;
        PreRelease = preRelease;
        BuildMetadata = buildMetadata;
    }

    /// <summary>
    /// Creates a version from a semantic version string (e.g., "1.2.3-beta.1+build.123").
    /// </summary>
    public static StateMachineVersion Parse(string version)
    {
        var parts = version.Split('-', '+');
        var versionParts = parts[0].Split('.');
        
        if (versionParts.Length != 3)
            throw new ArgumentException("Version must be in format 'major.minor.patch'", nameof(version));
        
        var major = int.Parse(versionParts[0]);
        var minor = int.Parse(versionParts[1]);
        var patch = int.Parse(versionParts[2]);
        
        string? preRelease = null;
        string? buildMetadata = null;
        
        if (parts.Length > 1 && version.Contains('-'))
        {
            var preReleaseIndex = version.IndexOf('-') + 1;
            var buildIndex = version.IndexOf('+');
            
            if (buildIndex > 0)
            {
                preRelease = version.Substring(preReleaseIndex, buildIndex - preReleaseIndex);
                buildMetadata = version.Substring(buildIndex + 1);
            }
            else
            {
                preRelease = version.Substring(preReleaseIndex);
            }
        }
        else if (parts.Length > 1 && version.Contains('+'))
        {
            var buildIndex = version.IndexOf('+') + 1;
            buildMetadata = version.Substring(buildIndex);
        }
        
        return new StateMachineVersion(major, minor, patch, preRelease, buildMetadata);
    }

    /// <summary>
    /// Determines if this version is compatible with another version for upgrades.
    /// </summary>
    public bool IsCompatibleWith(StateMachineVersion other)
    {
        // Major version changes are breaking
        if (Major != other.Major)
            return false;
        
        // Minor version increases are backward compatible
        // Patch version increases are always compatible
        return other.Minor >= Minor;
    }

    /// <summary>
    /// Determines if this is a breaking change compared to another version.
    /// </summary>
    public bool IsBreakingChangeFrom(StateMachineVersion other)
    {
        return Major > other.Major;
    }

    public int CompareTo(StateMachineVersion? other)
    {
        if (other is null) return 1;
        
        var majorComparison = Major.CompareTo(other.Major);
        if (majorComparison != 0) return majorComparison;
        
        var minorComparison = Minor.CompareTo(other.Minor);
        if (minorComparison != 0) return minorComparison;
        
        return Patch.CompareTo(other.Patch);
    }

    public override string ToString()
    {
        var version = $"{Major}.{Minor}.{Patch}";
        if (!string.IsNullOrEmpty(PreRelease))
            version += $"-{PreRelease}";
        if (!string.IsNullOrEmpty(BuildMetadata))
            version += $"+{BuildMetadata}";
        return version;
    }

    public override bool Equals(object? obj)
    {
        return obj is StateMachineVersion version && CompareTo(version) == 0;
    }

    public override int GetHashCode()
    {
        return HashCode.Combine(Major, Minor, Patch, PreRelease);
    }

    public static bool operator >(StateMachineVersion left, StateMachineVersion right) => left.CompareTo(right) > 0;
    public static bool operator <(StateMachineVersion left, StateMachineVersion right) => left.CompareTo(right) < 0;
    public static bool operator >=(StateMachineVersion left, StateMachineVersion right) => left.CompareTo(right) >= 0;
    public static bool operator <=(StateMachineVersion left, StateMachineVersion right) => left.CompareTo(right) <= 0;
    public static bool operator ==(StateMachineVersion left, StateMachineVersion right) => left.CompareTo(right) == 0;
    public static bool operator !=(StateMachineVersion left, StateMachineVersion right) => left.CompareTo(right) != 0;
}

/// <summary>
/// Information about version compatibility for a state machine grain.
/// </summary>
[GenerateSerializer]
public class VersionCompatibilityInfo
{
    [Id(0)] public StateMachineVersion CurrentVersion { get; set; } = new();
    [Id(1)] public StateMachineVersion MinSupportedVersion { get; set; } = new();
    [Id(2)] public StateMachineVersion MaxSupportedVersion { get; set; } = new();
    [Id(3)] public List<StateMachineVersion> AvailableVersions { get; set; } = new();
    [Id(4)] public bool SupportsAutomaticUpgrade { get; set; }
    [Id(5)] public bool RequiresMigration { get; set; }
    [Id(6)] public Dictionary<string, object> Metadata { get; set; } = new();
}

/// <summary>
/// Result of a version upgrade operation.
/// </summary>
[GenerateSerializer]
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
    [Id(8)] public Dictionary<string, object> Metadata { get; set; } = new();

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
public class MigrationSummary
{
    [Id(0)] public int StatesMigrated { get; set; }
    [Id(1)] public int TransitionsUpdated { get; set; }
    [Id(2)] public int GuardsModified { get; set; }
    [Id(3)] public int EventsReplayed { get; set; }
    [Id(4)] public List<string> ChangesApplied { get; set; } = new();
    [Id(5)] public Dictionary<string, object> Statistics { get; set; } = new();
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
public class ShadowEvaluationResult<TState>
    where TState : struct, Enum
{
    [Id(0)] public bool WouldSucceed { get; set; }
    [Id(1)] public TState? PredictedState { get; set; }
    [Id(2)] public TState CurrentState { get; set; }
    [Id(3)] public string? ErrorMessage { get; set; }
    [Id(4)] public Exception? Exception { get; set; }
    [Id(5)] public StateMachineVersion EvaluatedVersion { get; set; } = new();
    [Id(6)] public TimeSpan EvaluationDuration { get; set; }
    [Id(7)] public Dictionary<string, object> Metadata { get; set; } = new();

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