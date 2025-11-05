using Microsoft.Extensions.Logging;
using StateMachineVersion = Orleans.StateMachineES.Abstractions.Models.StateMachineVersion;
using RiskLevel = Orleans.StateMachineES.Abstractions.Models.RiskLevel;

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
public class VersionCompatibilityChecker(
    IStateMachineDefinitionRegistry registry,
    ILogger<VersionCompatibilityChecker> logger) : IVersionCompatibilityChecker
{
    private readonly IStateMachineDefinitionRegistry _registry = registry ?? throw new ArgumentNullException(nameof(registry));
    private readonly ILogger<VersionCompatibilityChecker> _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    private readonly CompatibilityRulesEngine _rulesEngine = new(logger);
    private readonly MigrationPathCalculator _pathCalculator = new(logger);

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
            var availableVersions = await _registry.GetAvailableVersionsAsync(grainTypeName);
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
            var context = BuildCompatibilityContextAsync(grainTypeName, fromVersion, toVersion);

            // Evaluate rules
            var result = await _rulesEngine.EvaluateCompatibilityAsync(context);

            // Calculate migration path if needed
            MigrationPath? migrationPath = null;
            if (!result.IsCompatible || result.CompatibilityLevel == VersionCompatibilityLevel.RequiresMigration)
            {
                migrationPath = await _pathCalculator.CalculateOptimalPathAsync(
                    grainTypeName, fromVersion, toVersion, availableVersions);
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
        var versions = await _registry.GetAvailableVersionsAsync(grainTypeName);
        var versionList = versions.ToList();

        foreach (var fromVersion in versionList)
        {
            foreach (var toVersion in versionList)
            {
                if (fromVersion.CompareTo(toVersion) < 0)
                {
                    var result = await CheckCompatibilityAsync(grainTypeName, fromVersion, toVersion);
                    
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
        var availableVersions = await _registry.GetAvailableVersionsAsync(grainTypeName);

        foreach (var targetVersion in availableVersions.Where(v => v.CompareTo(currentVersion) > 0))
        {
            var compatibility = await CheckCompatibilityAsync(grainTypeName, currentVersion, targetVersion);

            var paths = await _pathCalculator.CalculateAlternativePathsAsync(
                grainTypeName, currentVersion, targetVersion, availableVersions, 3);

            var recommendation = new UpgradeRecommendation
            {
                CurrentVersion = currentVersion,
                TargetVersion = targetVersion,
                RecommendationType = DetermineRecommendationType(compatibility),
                CompatibilityLevel = compatibility.CompatibilityLevel,
                MigrationPaths = paths,
                EstimatedEffort = EstimateUpgradeEffort(compatibility),
                Benefits = GenerateBenefitsList(currentVersion, targetVersion),
                Risks = [.. compatibility.BreakingChanges.Select(bc => bc.Description)],
                RiskLevel = DetermineRiskLevel(currentVersion, targetVersion, compatibility)
            };

            recommendations.Add(recommendation);
        }

        return [.. recommendations.OrderByDescending(r => r.RecommendationType).ThenBy(r => r.EstimatedEffort)];
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
            ExistingVersions = [.. existingVersions],
            CanDeploy = true,
            ValidationTime = DateTime.UtcNow
        };

        foreach (var existingVersion in existingVersions)
        {
            // Check forward compatibility
            var forwardCheck = await CheckCompatibilityAsync(grainTypeName, existingVersion, newVersion);
            
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
                var backwardCheck = await CheckCompatibilityAsync(grainTypeName, newVersion, existingVersion);
                
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

    /// <summary>
    /// Builds compatibility context for evaluation.
    /// </summary>
    /// <remarks>
    /// Currently performs basic context initialization. Future enhancement:
    /// Use ImprovedStateMachineIntrospector to analyze actual state machine changes
    /// (states, triggers, transitions, guards) by comparing version definitions
    /// from the registry.
    /// </remarks>
    private static CompatibilityContext BuildCompatibilityContextAsync(
        string grainTypeName,
        StateMachineVersion fromVersion,
        StateMachineVersion toVersion)
    {
        // Build context for rules evaluation
        // This would typically analyze the actual state machine definitions
        // For now, we create a basic context with version metadata
        var context = new CompatibilityContext
        {
            FromVersion = fromVersion,
            ToVersion = toVersion,
            GrainTypeName = grainTypeName,
            MigrationComplexity = MigrationComplexity.Simple
        };

        return context;
    }

    private static UpgradeRecommendationType DetermineRecommendationType(CompatibilityCheckResult compatibility)
    {
        if (!compatibility.IsCompatible)
            return UpgradeRecommendationType.NotRecommended;

        if (compatibility.CompatibilityLevel == VersionCompatibilityLevel.FullyCompatible)
            return UpgradeRecommendationType.HighlyRecommended;

        if (compatibility.BreakingChanges.Count == 0)
            return UpgradeRecommendationType.Recommended;

        return UpgradeRecommendationType.ConsiderWithCaution;
    }

    private static MigrationEffort EstimateUpgradeEffort(CompatibilityCheckResult compatibility)
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

    private static RiskLevel DetermineRiskLevel(StateMachineVersion fromVersion, StateMachineVersion toVersion, CompatibilityCheckResult compatibility)
    {
        // Major version changes are high risk
        if (toVersion.Major > fromVersion.Major)
            return RiskLevel.High;

        // Critical breaking changes are very high risk
        if (compatibility.BreakingChanges.Any(bc => bc.Impact == BreakingChangeImpact.Critical))
            return RiskLevel.VeryHigh;

        // High impact breaking changes are high risk
        if (compatibility.BreakingChanges.Any(bc => bc.Impact == BreakingChangeImpact.High))
            return RiskLevel.High;

        // Medium impact or many breaking changes are medium risk
        if (compatibility.BreakingChanges.Any(bc => bc.Impact == BreakingChangeImpact.Medium) || 
            compatibility.BreakingChanges.Count > 3)
            return RiskLevel.Medium;

        // Minor version changes with few breaking changes are low risk
        if (toVersion.Minor > fromVersion.Minor && compatibility.BreakingChanges.Count <= 3)
            return RiskLevel.Low;

        // Patch versions are low risk
        if (toVersion.Patch > fromVersion.Patch)
            return RiskLevel.Low;

        return RiskLevel.Low;
    }

    private static List<string> GenerateBenefitsList(StateMachineVersion fromVersion, StateMachineVersion toVersion)
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

    private static DeploymentStrategy DetermineDeploymentStrategy(DeploymentCompatibilityResult result)
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
    public List<BreakingChange> BreakingChanges { get; set; } = [];
    public List<string> Warnings { get; set; } = [];
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
public class CompatibilityMatrix(string grainTypeName)
{
    private readonly Dictionary<(StateMachineVersion, StateMachineVersion), CompatibilityCheckResult> _entries = [];

    public string GrainTypeName { get; } = grainTypeName;

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

    /// <summary>
    /// Gets all versions involved in the compatibility matrix.
    /// </summary>
    public IEnumerable<StateMachineVersion> Versions 
    { 
        get 
        {
            return _entries.Keys.SelectMany(k => new[] { k.Item1, k.Item2 }).Distinct();
        } 
    }

    /// <summary>
    /// Gets compatibility statistics for this matrix.
    /// </summary>
    public CompatibilityStatistics Statistics 
    { 
        get 
        {
            var versions = Versions.ToList();
            var totalCompatible = _entries.Count(e => e.Value.IsCompatible);
            var totalIncompatible = _entries.Count - totalCompatible;
            
            return new CompatibilityStatistics
            {
                TotalVersions = versions.Count,
                TotalCompatible = totalCompatible,
                TotalIncompatible = totalIncompatible,
                CompatibilityPercentage = _entries.Count > 0 ? (double)totalCompatible / _entries.Count * 100 : 0
            };
        } 
    }
}

/// <summary>
/// Statistics about compatibility in a compatibility matrix.
/// </summary>
public class CompatibilityStatistics
{
    /// <summary>
    /// Total number of versions in the matrix.
    /// </summary>
    public int TotalVersions { get; set; }

    /// <summary>
    /// Number of compatible version pairs.
    /// </summary>
    public int TotalCompatible { get; set; }

    /// <summary>
    /// Number of incompatible version pairs.
    /// </summary>
    public int TotalIncompatible { get; set; }

    /// <summary>
    /// Percentage of compatible version pairs.
    /// </summary>
    public double CompatibilityPercentage { get; set; }
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
    public List<MigrationPath> MigrationPaths { get; set; } = [];
    public MigrationEffort EstimatedEffort { get; set; }
    public List<string> Benefits { get; set; } = [];
    public List<string> Risks { get; set; } = [];
    public RiskLevel RiskLevel { get; set; }
    
    /// <summary>
    /// Alias for TargetVersion to maintain test compatibility.
    /// </summary>
    public StateMachineVersion ToVersion => TargetVersion;
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
    public List<StateMachineVersion> ExistingVersions { get; set; } = [];
    public bool CanDeploy { get; set; }
    public List<DeploymentIssue> Issues { get; set; } = [];
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