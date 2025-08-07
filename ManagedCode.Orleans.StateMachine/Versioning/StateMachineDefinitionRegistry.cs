using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Orleans;
using Stateless;

namespace ivlt.Orleans.StateMachineES.Versioning;

/// <summary>
/// Registry for state machine definitions across different versions.
/// Enables versioned deployments and gradual migrations.
/// </summary>
public interface IStateMachineDefinitionRegistry
{
    /// <summary>
    /// Registers a state machine definition for a specific version.
    /// </summary>
    Task RegisterDefinitionAsync<TState, TTrigger>(
        string grainTypeName,
        StateMachineVersion version,
        Func<StateMachine<TState, TTrigger>> definitionFactory,
        StateMachineDefinitionMetadata? metadata = null)
        where TState : struct, Enum
        where TTrigger : struct, Enum;

    /// <summary>
    /// Gets a state machine definition for a specific version.
    /// </summary>
    Task<StateMachine<TState, TTrigger>?> GetDefinitionAsync<TState, TTrigger>(
        string grainTypeName,
        StateMachineVersion version)
        where TState : struct, Enum
        where TTrigger : struct, Enum;

    /// <summary>
    /// Gets the latest version of a state machine definition.
    /// </summary>
    Task<(StateMachineVersion Version, StateMachine<TState, TTrigger> Definition)?> GetLatestDefinitionAsync<TState, TTrigger>(
        string grainTypeName)
        where TState : struct, Enum
        where TTrigger : struct, Enum;

    /// <summary>
    /// Gets all available versions for a grain type.
    /// </summary>
    Task<IReadOnlyList<StateMachineVersion>> GetAvailableVersionsAsync(string grainTypeName);

    /// <summary>
    /// Checks if a version is compatible with the current deployment.
    /// </summary>
    Task<bool> IsVersionCompatibleAsync(string grainTypeName, StateMachineVersion version);

    /// <summary>
    /// Gets migration path from one version to another.
    /// </summary>
    Task<MigrationPath?> GetMigrationPathAsync(
        string grainTypeName,
        StateMachineVersion fromVersion,
        StateMachineVersion toVersion);
}

/// <summary>
/// Default implementation of the state machine definition registry.
/// </summary>
public class StateMachineDefinitionRegistry : IStateMachineDefinitionRegistry
{
    private readonly ConcurrentDictionary<string, ConcurrentDictionary<StateMachineVersion, StateMachineDefinitionEntry>> _definitions = new();
    private readonly ConcurrentDictionary<string, List<MigrationRule>> _migrationRules = new();
    private readonly ILogger<StateMachineDefinitionRegistry> _logger;

    public StateMachineDefinitionRegistry(ILogger<StateMachineDefinitionRegistry> logger)
    {
        _logger = logger;
    }

    public Task RegisterDefinitionAsync<TState, TTrigger>(
        string grainTypeName,
        StateMachineVersion version,
        Func<StateMachine<TState, TTrigger>> definitionFactory,
        StateMachineDefinitionMetadata? metadata = null)
        where TState : struct, Enum
        where TTrigger : struct, Enum
    {
        var grainDefinitions = _definitions.GetOrAdd(grainTypeName, _ => new ConcurrentDictionary<StateMachineVersion, StateMachineDefinitionEntry>());
        
        var entry = new StateMachineDefinitionEntry
        {
            Version = version,
            GrainTypeName = grainTypeName,
            StateType = typeof(TState),
            TriggerType = typeof(TTrigger),
            DefinitionFactory = () => definitionFactory(),
            Metadata = metadata ?? new StateMachineDefinitionMetadata(),
            RegisteredAt = DateTime.UtcNow
        };

        grainDefinitions.AddOrUpdate(version, entry, (_, _) => entry);

        _logger.LogInformation("Registered state machine definition for {GrainType} version {Version}",
            grainTypeName, version);

        return Task.CompletedTask;
    }

    public Task<StateMachine<TState, TTrigger>?> GetDefinitionAsync<TState, TTrigger>(
        string grainTypeName,
        StateMachineVersion version)
        where TState : struct, Enum
        where TTrigger : struct, Enum
    {
        if (!_definitions.TryGetValue(grainTypeName, out var grainDefinitions))
            return Task.FromResult<StateMachine<TState, TTrigger>?>(null);

        if (!grainDefinitions.TryGetValue(version, out var entry))
            return Task.FromResult<StateMachine<TState, TTrigger>?>(null);

        if (entry.StateType != typeof(TState) || entry.TriggerType != typeof(TTrigger))
        {
            _logger.LogWarning("Type mismatch for {GrainType} version {Version}. Expected {ExpectedState}/{ExpectedTrigger}, got {ActualState}/{ActualTrigger}",
                grainTypeName, version, typeof(TState), typeof(TTrigger), entry.StateType, entry.TriggerType);
            return Task.FromResult<StateMachine<TState, TTrigger>?>(null);
        }

        try
        {
            var definition = (StateMachine<TState, TTrigger>)entry.DefinitionFactory();
            return Task.FromResult<StateMachine<TState, TTrigger>?>(definition);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to create state machine definition for {GrainType} version {Version}",
                grainTypeName, version);
            return Task.FromResult<StateMachine<TState, TTrigger>?>(null);
        }
    }

    public Task<(StateMachineVersion Version, StateMachine<TState, TTrigger> Definition)?> GetLatestDefinitionAsync<TState, TTrigger>(
        string grainTypeName)
        where TState : struct, Enum
        where TTrigger : struct, Enum
    {
        if (!_definitions.TryGetValue(grainTypeName, out var grainDefinitions))
            return Task.FromResult<(StateMachineVersion, StateMachine<TState, TTrigger>)?>(null);

        var latestEntry = grainDefinitions.Values
            .Where(e => e.StateType == typeof(TState) && e.TriggerType == typeof(TTrigger))
            .OrderByDescending(e => e.Version)
            .FirstOrDefault();

        if (latestEntry == null)
            return Task.FromResult<(StateMachineVersion, StateMachine<TState, TTrigger>)?>(null);

        try
        {
            var definition = (StateMachine<TState, TTrigger>)latestEntry.DefinitionFactory();
            return Task.FromResult<(StateMachineVersion, StateMachine<TState, TTrigger>)?>((latestEntry.Version, definition));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to create latest state machine definition for {GrainType}",
                grainTypeName);
            return Task.FromResult<(StateMachineVersion, StateMachine<TState, TTrigger>)?>(null);
        }
    }

    public Task<IReadOnlyList<StateMachineVersion>> GetAvailableVersionsAsync(string grainTypeName)
    {
        if (!_definitions.TryGetValue(grainTypeName, out var grainDefinitions))
            return Task.FromResult<IReadOnlyList<StateMachineVersion>>(Array.Empty<StateMachineVersion>());

        var versions = grainDefinitions.Keys.OrderByDescending(v => v).ToList();
        return Task.FromResult<IReadOnlyList<StateMachineVersion>>(versions);
    }

    public Task<bool> IsVersionCompatibleAsync(string grainTypeName, StateMachineVersion version)
    {
        if (!_definitions.TryGetValue(grainTypeName, out var grainDefinitions))
            return Task.FromResult(false);

        if (!grainDefinitions.TryGetValue(version, out var entry))
            return Task.FromResult(false);

        // Check if version is marked as deprecated or unsupported
        if (entry.Metadata.IsDeprecated || entry.Metadata.IsUnsupported)
            return Task.FromResult(false);

        // Check against minimum supported version
        var latestEntry = grainDefinitions.Values.OrderByDescending(e => e.Version).FirstOrDefault();
        if (latestEntry?.Metadata.MinSupportedVersion != null)
        {
            return Task.FromResult(version >= latestEntry.Metadata.MinSupportedVersion);
        }

        return Task.FromResult(true);
    }

    public Task<MigrationPath?> GetMigrationPathAsync(
        string grainTypeName,
        StateMachineVersion fromVersion,
        StateMachineVersion toVersion)
    {
        if (!_definitions.TryGetValue(grainTypeName, out var grainDefinitions))
            return Task.FromResult<MigrationPath?>(null);

        if (!grainDefinitions.ContainsKey(fromVersion) || !grainDefinitions.ContainsKey(toVersion))
            return Task.FromResult<MigrationPath?>(null);

        // Get migration rules for this grain type
        var migrationRules = _migrationRules.GetOrAdd(grainTypeName, _ => new List<MigrationRule>());

        // Find direct migration rule
        var directRule = migrationRules.FirstOrDefault(r => 
            r.FromVersion.CompareTo(fromVersion) == 0 && 
            r.ToVersion.CompareTo(toVersion) == 0);

        if (directRule != null)
        {
            return Task.FromResult<MigrationPath?>(new MigrationPath
            {
                FromVersion = fromVersion,
                ToVersion = toVersion,
                Steps = new List<MigrationStep> { directRule.Step },
                IsDirectPath = true
            });
        }

        // Try to find multi-step migration path
        var path = FindMigrationPath(grainTypeName, fromVersion, toVersion, migrationRules, grainDefinitions.Keys);
        return Task.FromResult(path);
    }

    /// <summary>
    /// Registers a migration rule between two versions.
    /// </summary>
    public void RegisterMigrationRule(string grainTypeName, MigrationRule rule)
    {
        var rules = _migrationRules.GetOrAdd(grainTypeName, _ => new List<MigrationRule>());
        rules.Add(rule);

        _logger.LogInformation("Registered migration rule for {GrainType} from {FromVersion} to {ToVersion}",
            grainTypeName, rule.FromVersion, rule.ToVersion);
    }

    private MigrationPath? FindMigrationPath(
        string grainTypeName,
        StateMachineVersion fromVersion,
        StateMachineVersion toVersion,
        List<MigrationRule> migrationRules,
        ICollection<StateMachineVersion> availableVersions)
    {
        // Simple implementation - could be enhanced with graph algorithms for complex scenarios
        var currentVersion = fromVersion;
        var steps = new List<MigrationStep>();
        var visited = new HashSet<StateMachineVersion> { fromVersion };

        while (currentVersion.CompareTo(toVersion) != 0)
        {
            var nextRule = migrationRules
                .Where(r => r.FromVersion.CompareTo(currentVersion) == 0)
                .Where(r => !visited.Contains(r.ToVersion))
                .OrderBy(r => Math.Abs(r.ToVersion.CompareTo(toVersion)))
                .FirstOrDefault();

            if (nextRule == null)
                break;

            steps.Add(nextRule.Step);
            visited.Add(nextRule.ToVersion);
            currentVersion = nextRule.ToVersion;

            // Prevent infinite loops
            if (steps.Count > 10)
                break;
        }

        if (currentVersion.CompareTo(toVersion) == 0)
        {
            return new MigrationPath
            {
                FromVersion = fromVersion,
                ToVersion = toVersion,
                Steps = steps,
                IsDirectPath = steps.Count == 1
            };
        }

        return null;
    }
}

/// <summary>
/// Entry in the state machine definition registry.
/// </summary>
public class StateMachineDefinitionEntry
{
    public StateMachineVersion Version { get; set; } = new();
    public string GrainTypeName { get; set; } = "";
    public Type StateType { get; set; } = typeof(object);
    public Type TriggerType { get; set; } = typeof(object);
    public Func<object> DefinitionFactory { get; set; } = () => new object();
    public StateMachineDefinitionMetadata Metadata { get; set; } = new();
    public DateTime RegisteredAt { get; set; } = DateTime.UtcNow;
}

/// <summary>
/// Metadata for a state machine definition.
/// </summary>
[GenerateSerializer]
public class StateMachineDefinitionMetadata
{
    [Id(0)] public string Description { get; set; } = "";
    [Id(1)] public string Author { get; set; } = "";
    [Id(2)] public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    [Id(3)] public bool IsDeprecated { get; set; }
    [Id(4)] public bool IsUnsupported { get; set; }
    [Id(5)] public StateMachineVersion? MinSupportedVersion { get; set; }
    [Id(6)] public List<string> BreakingChanges { get; set; } = new();
    [Id(7)] public List<string> Features { get; set; } = new();
    [Id(8)] public Dictionary<string, object> Properties { get; set; } = new();
}

/// <summary>
/// Represents a migration path between two versions.
/// </summary>
[GenerateSerializer]
public class MigrationPath
{
    [Id(0)] public StateMachineVersion FromVersion { get; set; } = new();
    [Id(1)] public StateMachineVersion ToVersion { get; set; } = new();
    [Id(2)] public List<MigrationStep> Steps { get; set; } = new();
    [Id(3)] public bool IsDirectPath { get; set; }
    [Id(4)] public TimeSpan EstimatedDuration => TimeSpan.FromMilliseconds(Steps.Sum(s => s.EstimatedDurationMs));
}

/// <summary>
/// A single step in a migration path.
/// </summary>
[GenerateSerializer]
public class MigrationStep
{
    [Id(0)] public string Name { get; set; } = "";
    [Id(1)] public string Description { get; set; } = "";
    [Id(2)] public MigrationStepType Type { get; set; }
    [Id(3)] public int EstimatedDurationMs { get; set; }
    [Id(4)] public Func<object, Task<object>>? MigrationFunction { get; set; }
    [Id(5)] public Dictionary<string, object> Parameters { get; set; } = new();
}

/// <summary>
/// Rule for migrating between specific versions.
/// </summary>
public class MigrationRule
{
    public StateMachineVersion FromVersion { get; set; } = new();
    public StateMachineVersion ToVersion { get; set; } = new();
    public MigrationStep Step { get; set; } = new();
}

/// <summary>
/// Types of migration steps.
/// </summary>
public enum MigrationStepType
{
    /// <summary>
    /// Automatic migration with no custom logic required.
    /// </summary>
    Automatic,
    
    /// <summary>
    /// Custom migration logic is required.
    /// </summary>
    Custom,
    
    /// <summary>
    /// State transformation is required.
    /// </summary>
    StateTransformation,
    
    /// <summary>
    /// Event replay is required.
    /// </summary>
    EventReplay,
    
    /// <summary>
    /// Manual intervention is required.
    /// </summary>
    Manual
}