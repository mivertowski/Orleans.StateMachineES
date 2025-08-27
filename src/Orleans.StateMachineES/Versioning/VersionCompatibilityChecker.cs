using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;

namespace Orleans.StateMachineES.Versioning;

/// <summary>
/// Service for checking version compatibility and determining upgrade paths for state machines.
/// Analyzes breaking changes, compatibility matrices, and provides upgrade recommendations.
/// This is the main facade that coordinates the CompatibilityRulesEngine and MigrationPathCalculator.
/// </summary>
public interface IVersionCompatibilityChecker
{
    /// <summary>
    /// Checks if two versions are compatible for upgrade.
    /// </summary>
    Task<CompatibilityCheckResult> CheckCompatibilityAsync(
        string grainTypeName,
        StateMachineVersion fromVersion,
        StateMachineVersion toVersion);

    /// <summary>
    /// Analyzes all available versions and their compatibility.
    /// </summary>
    Task<CompatibilityMatrix> AnalyzeCompatibilityMatrixAsync(string grainTypeName);

    /// <summary>
    /// Gets recommended upgrade paths from a given version.
    /// </summary>
    Task<IReadOnlyList<UpgradeRecommendation>> GetUpgradeRecommendationsAsync(
        string grainTypeName,
        StateMachineVersion currentVersion);

    /// <summary>
    /// Validates if a version can be safely deployed alongside existing versions.
    /// </summary>
    Task<DeploymentCompatibilityResult> ValidateDeploymentCompatibilityAsync(
        string grainTypeName,
        StateMachineVersion newVersion,
        IEnumerable<StateMachineVersion> existingVersions);
}

/// <summary>
/// Default implementation of version compatibility checker.
/// Coordinates between the CompatibilityRulesEngine and MigrationPathCalculator.
/// </summary>
public class VersionCompatibilityChecker : IVersionCompatibilityChecker
{
    private readonly IStateMachineDefinitionRegistry _registry;
    private readonly ILogger<VersionCompatibilityChecker> _logger;
    private readonly CompatibilityRulesEngine _rulesEngine;
    private readonly MigrationPathCalculator _pathCalculator;

    public VersionCompatibilityChecker(
        IStateMachineDefinitionRegistry registry,
        ILogger<VersionCompatibilityChecker> logger)
    {
        _registry = registry ?? throw new ArgumentNullException(nameof(registry));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _rulesEngine = new CompatibilityRulesEngine(logger);
        _pathCalculator = new MigrationPathCalculator(logger);
    }

    public async Task<CompatibilityCheckResult> CheckCompatibilityAsync(
        string grainTypeName,
        StateMachineVersion fromVersion,
        StateMachineVersion toVersion)
    {
        _logger.LogDebug("Checking compatibility for {GrainType} from {FromVersion} to {ToVersion}",
            grainTypeName, fromVersion, toVersion);

        try
        {
            // Check if versions exist
            var availableVersions = await _registry.GetAvailableVersionsAsync(grainTypeName).ConfigureAwait(false);
            var fromExists = availableVersions.Any(v => v.CompareTo(fromVersion) == 0);
            var toExists = availableVersions.Any(v => v.CompareTo(toVersion) == 0);

            if (!fromExists || !toExists)
            {
                return CompatibilityCheckResult.Failure(
                    fromVersion,
                    toVersion,
                    $"Version not found: {(!fromExists ? fromVersion : toVersion)}");
            }

            // Build compatibility context
            var context = await BuildCompatibilityContextAsync(grainTypeName, fromVersion, toVersion)
                .ConfigureAwait(false);

            // Evaluate rules
            var result = await _rulesEngine.EvaluateCompatibilityAsync(context).ConfigureAwait(false);

            // Calculate migration path if needed
            MigrationPath? migrationPath = null;
            if (!result.IsCompatible || result.CompatibilityLevel == VersionCompatibilityLevel.RequiresMigration)
            {
                migrationPath = await _pathCalculator.CalculateOptimalPathAsync(
                    grainTypeName, fromVersion, toVersion, availableVersions).ConfigureAwait(false);
            }

            return new CompatibilityCheckResult
            {
                FromVersion = fromVersion,
                ToVersion = toVersion,
                IsCompatible = result.IsCompatible,
                CompatibilityLevel = result.CompatibilityLevel,
                BreakingChanges = result.BreakingChanges,
                Warnings = result.Warnings,
                MigrationPath = migrationPath,
                CheckedAt = DateTime.UtcNow
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error checking compatibility for {GrainType}", grainTypeName);
            return CompatibilityCheckResult.Failure(fromVersion, toVersion, ex.Message);
        }
    }

    public async Task<CompatibilityMatrix> AnalyzeCompatibilityMatrixAsync(string grainTypeName)
    {
        _logger.LogInformation("Analyzing compatibility matrix for {GrainType}", grainTypeName);

        var matrix = new CompatibilityMatrix(grainTypeName);
        var versions = await _registry.GetAvailableVersionsAsync(grainTypeName).ConfigureAwait(false);
        var versionList = versions.ToList();

        foreach (var fromVersion in versionList)
        {
            foreach (var toVersion in versionList)
            {
                if (fromVersion.CompareTo(toVersion) < 0)
                {
                    var result = await CheckCompatibilityAsync(grainTypeName, fromVersion, toVersion)
                        .ConfigureAwait(false);
                    
                    matrix.AddEntry(fromVersion, toVersion, result);
                }
            }
        }

        return matrix;
    }

    public async Task<IReadOnlyList<UpgradeRecommendation>> GetUpgradeRecommendationsAsync(
        string grainTypeName,
        StateMachineVersion currentVersion)
    {
        _logger.LogDebug("Getting upgrade recommendations for {GrainType} from {Version}",
            grainTypeName, currentVersion);

        var recommendations = new List<UpgradeRecommendation>();
        var availableVersions = await _registry.GetAvailableVersionsAsync(grainTypeName).ConfigureAwait(false);

        foreach (var targetVersion in availableVersions.Where(v => v.CompareTo(currentVersion) > 0))
        {
            var compatibility = await CheckCompatibilityAsync(grainTypeName, currentVersion, targetVersion)
                .ConfigureAwait(false);

            var paths = await _pathCalculator.CalculateAlternativePathsAsync(
                grainTypeName, currentVersion, targetVersion, availableVersions, 3).ConfigureAwait(false);

            var recommendation = new UpgradeRecommendation
            {
                CurrentVersion = currentVersion,
                TargetVersion = targetVersion,
                RecommendationType = DetermineRecommendationType(compatibility),
                CompatibilityLevel = compatibility.CompatibilityLevel,
                MigrationPaths = paths,
                EstimatedEffort = EstimateUpgradeEffort(compatibility),
                Benefits = GenerateBenefitsList(currentVersion, targetVersion),
                Risks = compatibility.BreakingChanges.Select(bc => bc.Description).ToList()
            };

            recommendations.Add(recommendation);
        }

        return recommendations.OrderByDescending(r => r.RecommendationType)
                             .ThenBy(r => r.EstimatedEffort)
                             .ToList();
    }

    public async Task<DeploymentCompatibilityResult> ValidateDeploymentCompatibilityAsync(
        string grainTypeName,
        StateMachineVersion newVersion,
        IEnumerable<StateMachineVersion> existingVersions)
    {
        _logger.LogInformation("Validating deployment compatibility for {GrainType} version {NewVersion}",
            grainTypeName, newVersion);

        var result = new DeploymentCompatibilityResult
        {
            NewVersion = newVersion,
            ExistingVersions = existingVersions.ToList(),
            CanDeploy = true,
            ValidationTime = DateTime.UtcNow
        };

        foreach (var existingVersion in existingVersions)
        {
            // Check forward compatibility
            var forwardCheck = await CheckCompatibilityAsync(grainTypeName, existingVersion, newVersion)
                .ConfigureAwait(false);
            
            if (!forwardCheck.IsCompatible)
            {
                result.Issues.Add(new DeploymentIssue
                {
                    Type = DeploymentIssueType.IncompatibleVersion,
                    Severity = DeploymentIssueSeverity.Error,
                    Description = $"Version {existingVersion} cannot upgrade to {newVersion}",
                    AffectedVersion = existingVersion
                });
                result.CanDeploy = false;
            }

            // Check backward compatibility if needed
            if (existingVersion.CompareTo(newVersion) > 0)
            {
                var backwardCheck = await CheckCompatibilityAsync(grainTypeName, newVersion, existingVersion)
                    .ConfigureAwait(false);
                
                if (!backwardCheck.IsCompatible)
                {
                    result.Issues.Add(new DeploymentIssue
                    {
                        Type = DeploymentIssueType.BackwardIncompatible,
                        Severity = DeploymentIssueSeverity.Warning,
                        Description = $"Version {newVersion} is not backward compatible with {existingVersion}",
                        AffectedVersion = existingVersion
                    });
                }
            }
        }

        // Determine deployment strategy
        result.RecommendedStrategy = DetermineDeploymentStrategy(result);

        return result;
    }

    private async Task<CompatibilityContext> BuildCompatibilityContextAsync(
        string grainTypeName,
        StateMachineVersion fromVersion,
        StateMachineVersion toVersion)
    {
        // Build context for rules evaluation
        // This would typically analyze the actual state machine definitions
        // For now, we create a basic context
        var context = new CompatibilityContext
        {
            FromVersion = fromVersion,
            ToVersion = toVersion,
            GrainTypeName = grainTypeName
        };

        // TODO: Add logic to analyze actual state machine changes
        // This would involve introspecting the state machine definitions
        // and identifying state, trigger, and transition changes

        return context;
    }

    private UpgradeRecommendationType DetermineRecommendationType(CompatibilityCheckResult compatibility)
    {
        if (!compatibility.IsCompatible)
            return UpgradeRecommendationType.NotRecommended;

        if (compatibility.CompatibilityLevel == VersionCompatibilityLevel.FullyCompatible)
            return UpgradeRecommendationType.HighlyRecommended;

        if (compatibility.BreakingChanges.Count == 0)
            return UpgradeRecommendationType.Recommended;

        return UpgradeRecommendationType.ConsiderWithCaution;
    }

    private MigrationEffort EstimateUpgradeEffort(CompatibilityCheckResult compatibility)
    {
        if (!compatibility.IsCompatible)
            return MigrationEffort.High;

        var criticalChanges = compatibility.BreakingChanges.Count(c => c.Impact == BreakingChangeImpact.Critical);
        var highChanges = compatibility.BreakingChanges.Count(c => c.Impact == BreakingChangeImpact.High);

        if (criticalChanges > 0 || highChanges > 2)
            return MigrationEffort.High;

        if (highChanges > 0 || compatibility.BreakingChanges.Count > 5)
            return MigrationEffort.Medium;

        return MigrationEffort.Low;
    }

    private List<string> GenerateBenefitsList(StateMachineVersion fromVersion, StateMachineVersion toVersion)
    {
        var benefits = new List<string>();

        if (toVersion.Major > fromVersion.Major)
        {
            benefits.Add("Major new features and improvements");
            benefits.Add("Performance optimizations");
        }
        else if (toVersion.Minor > fromVersion.Minor)
        {
            benefits.Add("New functionality");
            benefits.Add("Bug fixes");
        }
        else if (toVersion.Patch > fromVersion.Patch)
        {
            benefits.Add("Bug fixes and security updates");
        }

        return benefits;
    }

    private DeploymentStrategy DetermineDeploymentStrategy(DeploymentCompatibilityResult result)
    {
        if (!result.CanDeploy)
            return DeploymentStrategy.Blocked;

        var hasErrors = result.Issues.Any(i => i.Severity == DeploymentIssueSeverity.Error);
        var hasWarnings = result.Issues.Any(i => i.Severity == DeploymentIssueSeverity.Warning);

        if (hasErrors)
            return DeploymentStrategy.RequiresMigration;

        if (hasWarnings)
            return DeploymentStrategy.RollingUpgrade;

        return DeploymentStrategy.DirectUpgrade;
    }
}

/// <summary>
/// Result of a compatibility check.
/// </summary>
public class CompatibilityCheckResult
{
    public StateMachineVersion FromVersion { get; set; } = null!;
    public StateMachineVersion ToVersion { get; set; } = null!;
    public bool IsCompatible { get; set; }
    public VersionCompatibilityLevel CompatibilityLevel { get; set; }
    public List<BreakingChange> BreakingChanges { get; set; } = new();
    public List<string> Warnings { get; set; } = new();
    public MigrationPath? MigrationPath { get; set; }
    public DateTime CheckedAt { get; set; }
    public string? FailureReason { get; set; }

    public static CompatibilityCheckResult Failure(
        StateMachineVersion from,
        StateMachineVersion to,
        string reason)
    {
        return new CompatibilityCheckResult
        {
            FromVersion = from,
            ToVersion = to,
            IsCompatible = false,
            CompatibilityLevel = VersionCompatibilityLevel.Incompatible,
            FailureReason = reason,
            CheckedAt = DateTime.UtcNow
        };
    }
}

/// <summary>
/// Level of version compatibility.
/// </summary>
public enum VersionCompatibilityLevel
{
    FullyCompatible,
    Compatible,
    PartiallyCompatible,
    RequiresMigration,
    Incompatible
}

/// <summary>
/// Matrix of compatibility between versions.
/// </summary>
public class CompatibilityMatrix
{
    private readonly Dictionary<(StateMachineVersion, StateMachineVersion), CompatibilityCheckResult> _entries;

    public string GrainTypeName { get; }

    public CompatibilityMatrix(string grainTypeName)
    {
        GrainTypeName = grainTypeName;
        _entries = new Dictionary<(StateMachineVersion, StateMachineVersion), CompatibilityCheckResult>();
    }

    public void AddEntry(StateMachineVersion from, StateMachineVersion to, CompatibilityCheckResult result)
    {
        _entries[(from, to)] = result;
    }

    public CompatibilityCheckResult? GetEntry(StateMachineVersion from, StateMachineVersion to)
    {
        return _entries.TryGetValue((from, to), out var result) ? result : null;
    }

    public bool IsCompatible(StateMachineVersion from, StateMachineVersion to)
    {
        return GetEntry(from, to)?.IsCompatible ?? false;
    }

    public IEnumerable<CompatibilityCheckResult> GetAllEntries()
    {
        return _entries.Values;
    }
}

/// <summary>
/// Upgrade recommendation.
/// </summary>
public class UpgradeRecommendation
{
    public StateMachineVersion CurrentVersion { get; set; } = null!;
    public StateMachineVersion TargetVersion { get; set; } = null!;
    public UpgradeRecommendationType RecommendationType { get; set; }
    public VersionCompatibilityLevel CompatibilityLevel { get; set; }
    public List<MigrationPath> MigrationPaths { get; set; } = new();
    public MigrationEffort EstimatedEffort { get; set; }
    public List<string> Benefits { get; set; } = new();
    public List<string> Risks { get; set; } = new();
}

/// <summary>
/// Type of upgrade recommendation.
/// </summary>
public enum UpgradeRecommendationType
{
    HighlyRecommended,
    Recommended,
    ConsiderWithCaution,
    NotRecommended
}

/// <summary>
/// Result of deployment compatibility validation.
/// </summary>
public class DeploymentCompatibilityResult
{
    public StateMachineVersion NewVersion { get; set; } = null!;
    public List<StateMachineVersion> ExistingVersions { get; set; } = new();
    public bool CanDeploy { get; set; }
    public List<DeploymentIssue> Issues { get; set; } = new();
    public DeploymentStrategy RecommendedStrategy { get; set; }
    public DateTime ValidationTime { get; set; }
}

/// <summary>
/// Deployment issue.
/// </summary>
public class DeploymentIssue
{
    public DeploymentIssueType Type { get; set; }
    public DeploymentIssueSeverity Severity { get; set; }
    public string Description { get; set; } = string.Empty;
    public StateMachineVersion? AffectedVersion { get; set; }
}

/// <summary>
/// Type of deployment issue.
/// </summary>
public enum DeploymentIssueType
{
    IncompatibleVersion,
    BackwardIncompatible,
    MissingMigration,
    PerformanceRegression
}

/// <summary>
/// Severity of deployment issue.
/// </summary>
public enum DeploymentIssueSeverity
{
    Info,
    Warning,
    Error,
    Critical
}

/// <summary>
/// Deployment strategy.
/// </summary>
public enum DeploymentStrategy
{
    DirectUpgrade,
    RollingUpgrade,
    BlueGreenDeployment,
    RequiresMigration,
    Blocked
}