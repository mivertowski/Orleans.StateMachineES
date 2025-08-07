using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Orleans;

namespace ivlt.Orleans.StateMachineES.Versioning;

/// <summary>
/// Service for checking version compatibility and determining upgrade paths for state machines.
/// Analyzes breaking changes, compatibility matrices, and provides upgrade recommendations.
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
/// </summary>
public class VersionCompatibilityChecker : IVersionCompatibilityChecker
{
    private readonly IStateMachineDefinitionRegistry _registry;
    private readonly ILogger<VersionCompatibilityChecker> _logger;

    public VersionCompatibilityChecker(
        IStateMachineDefinitionRegistry registry,
        ILogger<VersionCompatibilityChecker> logger)
    {
        _registry = registry;
        _logger = logger;
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
            // Basic version comparison
            var versionCompatibility = AnalyzeVersionCompatibility(fromVersion, toVersion);
            
            // Check if both versions exist in registry
            var fromExists = await _registry.GetDefinitionAsync<object, object>(grainTypeName, fromVersion) != null;
            var toExists = await _registry.GetDefinitionAsync<object, object>(grainTypeName, toVersion) != null;
            
            if (!fromExists || !toExists)
            {
                return CompatibilityCheckResult.Failure(
                    fromVersion,
                    toVersion,
                    !fromExists ? $"Source version {fromVersion} not found" : $"Target version {toVersion} not found",
                    CompatibilityIssueType.VersionNotFound);
            }

            // Check for migration path
            var migrationPath = await _registry.GetMigrationPathAsync(grainTypeName, fromVersion, toVersion);
            var hasMigrationPath = migrationPath != null;

            // Analyze breaking changes
            var breakingChanges = await AnalyzeBreakingChangesAsync(grainTypeName, fromVersion, toVersion);

            // Determine overall compatibility
            var isCompatible = versionCompatibility.IsCompatible && 
                               (hasMigrationPath || breakingChanges.Count == 0);

            if (isCompatible)
            {
                return CompatibilityCheckResult.Success(
                    fromVersion,
                    toVersion,
                    versionCompatibility.CompatibilityLevel,
                    migrationPath,
                    breakingChanges);
            }
            else
            {
                var primaryIssue = !versionCompatibility.IsCompatible
                    ? CompatibilityIssueType.VersionIncompatible
                    : CompatibilityIssueType.BreakingChanges;

                return CompatibilityCheckResult.Failure(
                    fromVersion,
                    toVersion,
                    "Versions are not compatible for direct upgrade",
                    primaryIssue,
                    breakingChanges);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error checking compatibility for {GrainType} from {FromVersion} to {ToVersion}",
                grainTypeName, fromVersion, toVersion);
            
            return CompatibilityCheckResult.Failure(
                fromVersion,
                toVersion,
                $"Compatibility check failed: {ex.Message}",
                CompatibilityIssueType.CheckFailed);
        }
    }

    public async Task<CompatibilityMatrix> AnalyzeCompatibilityMatrixAsync(string grainTypeName)
    {
        _logger.LogDebug("Analyzing compatibility matrix for {GrainType}", grainTypeName);

        var availableVersions = await _registry.GetAvailableVersionsAsync(grainTypeName);
        var matrix = new CompatibilityMatrix
        {
            GrainTypeName = grainTypeName,
            Versions = availableVersions.OrderBy(v => v).ToList(),
            CompatibilityResults = new Dictionary<(StateMachineVersion From, StateMachineVersion To), CompatibilityCheckResult>()
        };

        // Check compatibility between all version pairs
        foreach (var fromVersion in availableVersions)
        {
            foreach (var toVersion in availableVersions.Where(v => v > fromVersion))
            {
                var result = await CheckCompatibilityAsync(grainTypeName, fromVersion, toVersion);
                matrix.CompatibilityResults[(fromVersion, toVersion)] = result;
            }
        }

        // Calculate statistics
        var totalChecks = matrix.CompatibilityResults.Count;
        var compatibleChecks = matrix.CompatibilityResults.Values.Count(r => r.IsCompatible);
        
        matrix.Statistics = new CompatibilityStatistics
        {
            TotalVersions = availableVersions.Count,
            TotalCompatibilityChecks = totalChecks,
            CompatibleUpgrades = compatibleChecks,
            IncompatibleUpgrades = totalChecks - compatibleChecks,
            CompatibilityPercentage = totalChecks > 0 ? (double)compatibleChecks / totalChecks * 100 : 0
        };

        _logger.LogInformation("Compatibility matrix for {GrainType}: {CompatibleUpgrades}/{TotalChecks} compatible upgrades ({Percentage:F1}%)",
            grainTypeName, compatibleChecks, totalChecks, matrix.Statistics.CompatibilityPercentage);

        return matrix;
    }

    public async Task<IReadOnlyList<UpgradeRecommendation>> GetUpgradeRecommendationsAsync(
        string grainTypeName,
        StateMachineVersion currentVersion)
    {
        _logger.LogDebug("Getting upgrade recommendations for {GrainType} version {CurrentVersion}",
            grainTypeName, currentVersion);

        var availableVersions = await _registry.GetAvailableVersionsAsync(grainTypeName);
        var newerVersions = availableVersions.Where(v => v > currentVersion).OrderBy(v => v).ToList();
        
        var recommendations = new List<UpgradeRecommendation>();

        foreach (var targetVersion in newerVersions)
        {
            var compatibility = await CheckCompatibilityAsync(grainTypeName, currentVersion, targetVersion);
            
            var recommendation = new UpgradeRecommendation
            {
                FromVersion = currentVersion,
                ToVersion = targetVersion,
                RecommendationType = DetermineRecommendationType(compatibility),
                CompatibilityResult = compatibility,
                EstimatedEffort = EstimateUpgradeEffort(compatibility),
                RiskLevel = AssessRiskLevel(compatibility),
                Benefits = await DetermineBenefitsAsync(grainTypeName, currentVersion, targetVersion),
                Prerequisites = await DeterminePrerequisitesAsync(grainTypeName, currentVersion, targetVersion)
            };

            recommendations.Add(recommendation);
        }

        // Sort by recommendation type and risk level
        recommendations.Sort((r1, r2) =>
        {
            var typeComparison = r1.RecommendationType.CompareTo(r2.RecommendationType);
            return typeComparison != 0 ? typeComparison : r1.RiskLevel.CompareTo(r2.RiskLevel);
        });

        return recommendations;
    }

    public async Task<DeploymentCompatibilityResult> ValidateDeploymentCompatibilityAsync(
        string grainTypeName,
        StateMachineVersion newVersion,
        IEnumerable<StateMachineVersion> existingVersions)
    {
        _logger.LogDebug("Validating deployment compatibility for {GrainType} version {NewVersion}",
            grainTypeName, newVersion);

        var issues = new List<DeploymentIssue>();
        var warnings = new List<string>();
        var recommendations = new List<string>();

        foreach (var existingVersion in existingVersions)
        {
            // Check backward compatibility
            var backwardCompatibility = await CheckCompatibilityAsync(grainTypeName, existingVersion, newVersion);
            
            // Check forward compatibility  
            var forwardCompatibility = await CheckCompatibilityAsync(grainTypeName, newVersion, existingVersion);

            // Analyze compatibility issues
            if (!backwardCompatibility.IsCompatible)
            {
                issues.Add(new DeploymentIssue
                {
                    IssueType = DeploymentIssueType.BackwardIncompatibility,
                    Description = $"New version {newVersion} is not backward compatible with existing version {existingVersion}",
                    AffectedVersion = existingVersion,
                    Severity = DeploymentIssueSeverity.High
                });
            }

            if (newVersion.IsBreakingChangeFrom(existingVersion))
            {
                warnings.Add($"Version {newVersion} introduces breaking changes from {existingVersion}");
                recommendations.Add($"Consider gradual migration strategy for instances running {existingVersion}");
            }
        }

        var canDeploy = !issues.Any(i => i.Severity == DeploymentIssueSeverity.High);

        return new DeploymentCompatibilityResult
        {
            GrainTypeName = grainTypeName,
            NewVersion = newVersion,
            ExistingVersions = existingVersions.ToList(),
            CanDeploy = canDeploy,
            Issues = issues,
            Warnings = warnings,
            Recommendations = recommendations,
            SuggestedStrategy = DetermineDeploymentStrategy(newVersion, existingVersions, issues)
        };
    }

    #region Private Helper Methods

    private (bool IsCompatible, VersionCompatibilityLevel CompatibilityLevel) AnalyzeVersionCompatibility(
        StateMachineVersion fromVersion,
        StateMachineVersion toVersion)
    {
        if (fromVersion >= toVersion)
            return (false, VersionCompatibilityLevel.Incompatible);

        if (fromVersion.IsBreakingChangeFrom(toVersion))
            return (false, VersionCompatibilityLevel.Incompatible);

        if (fromVersion.Major == toVersion.Major)
        {
            if (fromVersion.Minor == toVersion.Minor)
                return (true, VersionCompatibilityLevel.FullyCompatible);
            else
                return (true, VersionCompatibilityLevel.BackwardCompatible);
        }

        return (false, VersionCompatibilityLevel.RequiresMigration);
    }

    private async Task<List<BreakingChange>> AnalyzeBreakingChangesAsync(
        string grainTypeName,
        StateMachineVersion fromVersion,
        StateMachineVersion toVersion)
    {
        // This would analyze the actual state machine definitions
        // For now, return a placeholder implementation
        var breakingChanges = new List<BreakingChange>();

        if (toVersion.Major > fromVersion.Major)
        {
            breakingChanges.Add(new BreakingChange
            {
                ChangeType = BreakingChangeType.MajorVersionIncrease,
                Description = $"Major version increase from {fromVersion.Major} to {toVersion.Major}",
                Impact = BreakingChangeImpact.High,
                Mitigation = "Full migration required"
            });
        }

        return await Task.FromResult(breakingChanges);
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

    private UpgradeEffort EstimateUpgradeEffort(CompatibilityCheckResult compatibility)
    {
        if (!compatibility.IsCompatible)
            return UpgradeEffort.VeryHigh;

        if (compatibility.CompatibilityLevel == VersionCompatibilityLevel.FullyCompatible)
            return UpgradeEffort.Low;

        if (compatibility.BreakingChanges.Count == 0)
            return UpgradeEffort.Medium;

        return UpgradeEffort.High;
    }

    private RiskLevel AssessRiskLevel(CompatibilityCheckResult compatibility)
    {
        if (!compatibility.IsCompatible)
            return RiskLevel.VeryHigh;

        if (compatibility.BreakingChanges.Any(bc => bc.Impact == BreakingChangeImpact.High))
            return RiskLevel.High;

        if (compatibility.BreakingChanges.Count > 0)
            return RiskLevel.Medium;

        return RiskLevel.Low;
    }

    private async Task<List<string>> DetermineBenefitsAsync(
        string grainTypeName,
        StateMachineVersion fromVersion,
        StateMachineVersion toVersion)
    {
        // This would analyze the actual differences between versions
        // For now, return generic benefits
        return await Task.FromResult(new List<string>
        {
            $"Updated to version {toVersion}",
            "Latest features and improvements",
            "Security updates and bug fixes"
        });
    }

    private async Task<List<string>> DeterminePrerequisitesAsync(
        string grainTypeName,
        StateMachineVersion fromVersion,
        StateMachineVersion toVersion)
    {
        // This would determine what needs to be in place before upgrading
        return await Task.FromResult(new List<string>
        {
            "Backup current state",
            "Verify system health",
            "Review breaking changes"
        });
    }

    private DeploymentStrategy DetermineDeploymentStrategy(
        StateMachineVersion newVersion,
        IEnumerable<StateMachineVersion> existingVersions,
        List<DeploymentIssue> issues)
    {
        if (issues.Any(i => i.Severity == DeploymentIssueSeverity.High))
            return DeploymentStrategy.CannotDeploy;

        if (existingVersions.Any(v => newVersion.IsBreakingChangeFrom(v)))
            return DeploymentStrategy.BlueGreenDeployment;

        return DeploymentStrategy.RollingUpdate;
    }

    #endregion
}

// Supporting types for compatibility checking

[GenerateSerializer]
public class CompatibilityCheckResult
{
    [Id(0)] public bool IsCompatible { get; set; }
    [Id(1)] public StateMachineVersion FromVersion { get; set; } = new();
    [Id(2)] public StateMachineVersion ToVersion { get; set; } = new();
    [Id(3)] public VersionCompatibilityLevel CompatibilityLevel { get; set; }
    [Id(4)] public MigrationPath? MigrationPath { get; set; }
    [Id(5)] public List<BreakingChange> BreakingChanges { get; set; } = new();
    [Id(6)] public string? ErrorMessage { get; set; }
    [Id(7)] public CompatibilityIssueType? IssueType { get; set; }
    [Id(8)] public DateTime CheckedAt { get; set; } = DateTime.UtcNow;

    public static CompatibilityCheckResult Success(
        StateMachineVersion fromVersion,
        StateMachineVersion toVersion,
        VersionCompatibilityLevel level,
        MigrationPath? migrationPath = null,
        List<BreakingChange>? breakingChanges = null)
    {
        return new CompatibilityCheckResult
        {
            IsCompatible = true,
            FromVersion = fromVersion,
            ToVersion = toVersion,
            CompatibilityLevel = level,
            MigrationPath = migrationPath,
            BreakingChanges = breakingChanges ?? new List<BreakingChange>()
        };
    }

    public static CompatibilityCheckResult Failure(
        StateMachineVersion fromVersion,
        StateMachineVersion toVersion,
        string errorMessage,
        CompatibilityIssueType issueType,
        List<BreakingChange>? breakingChanges = null)
    {
        return new CompatibilityCheckResult
        {
            IsCompatible = false,
            FromVersion = fromVersion,
            ToVersion = toVersion,
            ErrorMessage = errorMessage,
            IssueType = issueType,
            BreakingChanges = breakingChanges ?? new List<BreakingChange>()
        };
    }
}

public enum VersionCompatibilityLevel
{
    Incompatible,
    RequiresMigration,
    BackwardCompatible,
    FullyCompatible
}

public enum CompatibilityIssueType
{
    VersionNotFound,
    VersionIncompatible,
    BreakingChanges,
    CheckFailed
}

[GenerateSerializer]
public class BreakingChange
{
    [Id(0)] public BreakingChangeType ChangeType { get; set; }
    [Id(1)] public string Description { get; set; } = "";
    [Id(2)] public BreakingChangeImpact Impact { get; set; }
    [Id(3)] public string Mitigation { get; set; } = "";
}

public enum BreakingChangeType
{
    StateRemoved,
    TriggerRemoved,
    TransitionRemoved,
    GuardChanged,
    MajorVersionIncrease
}

public enum BreakingChangeImpact
{
    Low,
    Medium,
    High,
    Critical
}

[GenerateSerializer]
public class CompatibilityMatrix
{
    [Id(0)] public string GrainTypeName { get; set; } = "";
    [Id(1)] public List<StateMachineVersion> Versions { get; set; } = new();
    [Id(2)] public Dictionary<(StateMachineVersion From, StateMachineVersion To), CompatibilityCheckResult> CompatibilityResults { get; set; } = new();
    [Id(3)] public CompatibilityStatistics Statistics { get; set; } = new();
}

[GenerateSerializer]
public class CompatibilityStatistics
{
    [Id(0)] public int TotalVersions { get; set; }
    [Id(1)] public int TotalCompatibilityChecks { get; set; }
    [Id(2)] public int CompatibleUpgrades { get; set; }
    [Id(3)] public int IncompatibleUpgrades { get; set; }
    [Id(4)] public double CompatibilityPercentage { get; set; }
}

[GenerateSerializer]
public class UpgradeRecommendation
{
    [Id(0)] public StateMachineVersion FromVersion { get; set; } = new();
    [Id(1)] public StateMachineVersion ToVersion { get; set; } = new();
    [Id(2)] public UpgradeRecommendationType RecommendationType { get; set; }
    [Id(3)] public CompatibilityCheckResult CompatibilityResult { get; set; } = new();
    [Id(4)] public UpgradeEffort EstimatedEffort { get; set; }
    [Id(5)] public RiskLevel RiskLevel { get; set; }
    [Id(6)] public List<string> Benefits { get; set; } = new();
    [Id(7)] public List<string> Prerequisites { get; set; } = new();
}

public enum UpgradeRecommendationType
{
    HighlyRecommended,
    Recommended,
    ConsiderWithCaution,
    NotRecommended
}

public enum UpgradeEffort
{
    Low,
    Medium,
    High,
    VeryHigh
}

public enum RiskLevel
{
    Low,
    Medium,
    High,
    VeryHigh
}

[GenerateSerializer]
public class DeploymentCompatibilityResult
{
    [Id(0)] public string GrainTypeName { get; set; } = "";
    [Id(1)] public StateMachineVersion NewVersion { get; set; } = new();
    [Id(2)] public List<StateMachineVersion> ExistingVersions { get; set; } = new();
    [Id(3)] public bool CanDeploy { get; set; }
    [Id(4)] public List<DeploymentIssue> Issues { get; set; } = new();
    [Id(5)] public List<string> Warnings { get; set; } = new();
    [Id(6)] public List<string> Recommendations { get; set; } = new();
    [Id(7)] public DeploymentStrategy SuggestedStrategy { get; set; }
}

[GenerateSerializer]
public class DeploymentIssue
{
    [Id(0)] public DeploymentIssueType IssueType { get; set; }
    [Id(1)] public string Description { get; set; } = "";
    [Id(2)] public StateMachineVersion? AffectedVersion { get; set; }
    [Id(3)] public DeploymentIssueSeverity Severity { get; set; }
}

public enum DeploymentIssueType
{
    BackwardIncompatibility,
    ForwardIncompatibility,
    ConflictingVersions,
    MissingDependencies
}

public enum DeploymentIssueSeverity
{
    Low,
    Medium,
    High,
    Critical
}

public enum DeploymentStrategy
{
    RollingUpdate,
    BlueGreenDeployment,
    CanaryDeployment,
    CannotDeploy
}