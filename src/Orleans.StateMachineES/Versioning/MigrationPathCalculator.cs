using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;

namespace Orleans.StateMachineES.Versioning;

/// <summary>
/// Calculates optimal migration paths between state machine versions.
/// Uses graph algorithms to find the safest and most efficient upgrade paths.
/// </summary>
public sealed class MigrationPathCalculator
{
    private readonly ILogger? _logger;
    private readonly Dictionary<string, VersionGraph> _versionGraphs;
    private readonly MigrationCostEvaluator _costEvaluator;

    /// <summary>
    /// Initializes a new instance of the MigrationPathCalculator class.
    /// </summary>
    /// <param name="logger">Optional logger for diagnostics.</param>
    public MigrationPathCalculator(ILogger? logger = null)
    {
        _logger = logger;
        _versionGraphs = new Dictionary<string, VersionGraph>();
        _costEvaluator = new MigrationCostEvaluator();
    }

    /// <summary>
    /// Calculates the optimal migration path between two versions.
    /// </summary>
    /// <param name="grainType">The grain type name.</param>
    /// <param name="fromVersion">The starting version.</param>
    /// <param name="toVersion">The target version.</param>
    /// <param name="availableVersions">All available versions.</param>
    /// <param name="compatibilityMatrix">Compatibility information between versions.</param>
    /// <returns>The optimal migration path.</returns>
    public async Task<MigrationPath> CalculateOptimalPathAsync(
        string grainType,
        StateMachineVersion fromVersion,
        StateMachineVersion toVersion,
        IEnumerable<StateMachineVersion> availableVersions,
        CompatibilityMatrix? compatibilityMatrix = null)
    {
        _logger?.LogDebug("Calculating migration path for {GrainType} from {From} to {To}",
            grainType, fromVersion, toVersion);

        // Build or retrieve version graph
        var graph = await GetOrBuildVersionGraphAsync(grainType, availableVersions, compatibilityMatrix)
            .ConfigureAwait(false);

        // Find all possible paths
        var allPaths = FindAllPaths(graph, fromVersion, toVersion);

        if (allPaths.Count == 0)
        {
            _logger?.LogWarning("No migration path found from {From} to {To}", fromVersion, toVersion);
            return MigrationPath.NoPath(fromVersion, toVersion, "No compatible migration path exists");
        }

        // Evaluate and rank paths
        var evaluatedPaths = await EvaluatePathsAsync(allPaths, compatibilityMatrix).ConfigureAwait(false);

        // Select optimal path
        var optimalPath = SelectOptimalPath(evaluatedPaths);

        _logger?.LogInformation("Optimal migration path found with {Steps} steps and cost {Cost}",
            optimalPath.Steps.Count, optimalPath.TotalCost);

        return optimalPath;
    }

    /// <summary>
    /// Calculates multiple alternative migration paths.
    /// </summary>
    public async Task<List<MigrationPath>> CalculateAlternativePathsAsync(
        string grainType,
        StateMachineVersion fromVersion,
        StateMachineVersion toVersion,
        IEnumerable<StateMachineVersion> availableVersions,
        int maxPaths = 3,
        CompatibilityMatrix? compatibilityMatrix = null)
    {
        var graph = await GetOrBuildVersionGraphAsync(grainType, availableVersions, compatibilityMatrix)
            .ConfigureAwait(false);

        var allPaths = FindAllPaths(graph, fromVersion, toVersion);
        var evaluatedPaths = await EvaluatePathsAsync(allPaths, compatibilityMatrix).ConfigureAwait(false);

        // Return top N paths
        return evaluatedPaths
            .OrderBy(p => p.TotalCost)
            .ThenBy(p => p.Steps.Count)
            .Take(maxPaths)
            .ToList();
    }

    /// <summary>
    /// Gets or builds the version graph for a grain type.
    /// </summary>
    private async Task<VersionGraph> GetOrBuildVersionGraphAsync(
        string grainType,
        IEnumerable<StateMachineVersion> availableVersions,
        CompatibilityMatrix? compatibilityMatrix)
    {
        if (_versionGraphs.TryGetValue(grainType, out var existingGraph))
        {
            return existingGraph;
        }

        var graph = new VersionGraph(grainType);
        var versions = availableVersions.ToList();

        // Add all versions as nodes
        foreach (var version in versions)
        {
            graph.AddNode(version);
        }

        // Add edges based on compatibility
        foreach (var fromVer in versions)
        {
            foreach (var toVer in versions)
            {
                if (fromVer.CompareTo(toVer) < 0) // Only consider upgrades
                {
                    var isCompatible = await CheckCompatibilityAsync(
                        fromVer, toVer, compatibilityMatrix).ConfigureAwait(false);

                    if (isCompatible)
                    {
                        var cost = _costEvaluator.EvaluateMigrationCost(fromVer, toVer);
                        graph.AddEdge(fromVer, toVer, cost);
                    }
                }
            }
        }

        _versionGraphs[grainType] = graph;
        return graph;
    }

    /// <summary>
    /// Checks if two versions are compatible.
    /// </summary>
    private async Task<bool> CheckCompatibilityAsync(
        StateMachineVersion from,
        StateMachineVersion to,
        CompatibilityMatrix? matrix)
    {
        if (matrix != null)
        {
            return matrix.IsCompatible(from, to);
        }

        // Default compatibility rules
        // Allow patch and minor version upgrades within same major version
        if (from.Major == to.Major)
        {
            return true;
        }

        // Allow single major version upgrades
        if (to.Major == from.Major + 1)
        {
            return true;
        }

        return false;
    }

    /// <summary>
    /// Finds all paths between two versions using depth-first search.
    /// </summary>
    private List<List<StateMachineVersion>> FindAllPaths(
        VersionGraph graph,
        StateMachineVersion from,
        StateMachineVersion to)
    {
        var allPaths = new List<List<StateMachineVersion>>();
        var currentPath = new List<StateMachineVersion> { from };
        var visited = new HashSet<StateMachineVersion>();

        FindPathsDFS(graph, from, to, visited, currentPath, allPaths);

        return allPaths;
    }

    /// <summary>
    /// Depth-first search implementation for finding paths.
    /// </summary>
    private void FindPathsDFS(
        VersionGraph graph,
        StateMachineVersion current,
        StateMachineVersion target,
        HashSet<StateMachineVersion> visited,
        List<StateMachineVersion> currentPath,
        List<List<StateMachineVersion>> allPaths)
    {
        if (current.Equals(target))
        {
            allPaths.Add(new List<StateMachineVersion>(currentPath));
            return;
        }

        visited.Add(current);

        foreach (var edge in graph.GetOutgoingEdges(current))
        {
            if (!visited.Contains(edge.To))
            {
                currentPath.Add(edge.To);
                FindPathsDFS(graph, edge.To, target, visited, currentPath, allPaths);
                currentPath.RemoveAt(currentPath.Count - 1);
            }
        }

        visited.Remove(current);
    }

    /// <summary>
    /// Evaluates migration paths and creates MigrationPath objects.
    /// </summary>
    private async Task<List<MigrationPath>> EvaluatePathsAsync(
        List<List<StateMachineVersion>> paths,
        CompatibilityMatrix? matrix)
    {
        var evaluatedPaths = new List<MigrationPath>();

        foreach (var versionPath in paths)
        {
            var migrationPath = await BuildMigrationPathAsync(versionPath, matrix).ConfigureAwait(false);
            evaluatedPaths.Add(migrationPath);
        }

        return evaluatedPaths;
    }

    /// <summary>
    /// Builds a detailed migration path from a version sequence.
    /// </summary>
    private async Task<MigrationPath> BuildMigrationPathAsync(
        List<StateMachineVersion> versionPath,
        CompatibilityMatrix? matrix)
    {
        var steps = new List<MigrationStep>();
        var totalCost = 0.0;
        var totalRisk = RiskLevel.Low;

        for (int i = 0; i < versionPath.Count - 1; i++)
        {
            var fromVer = versionPath[i];
            var toVer = versionPath[i + 1];

            var step = new MigrationStep
            {
                Order = i + 1,
                FromVersion = fromVer,
                ToVersion = toVer,
                Type = DetermineMigrationType(fromVer, toVer),
                Description = $"Migrate from {fromVer} to {toVer}",
                EstimatedEffort = EstimateStepEffort(fromVer, toVer),
                RiskLevel = AssessStepRisk(fromVer, toVer),
                RequiredActions = GenerateStepActions(fromVer, toVer),
                ValidationSteps = GenerateValidationSteps(fromVer, toVer)
            };

            steps.Add(step);
            
            var stepCost = _costEvaluator.EvaluateMigrationCost(fromVer, toVer);
            totalCost += stepCost;
            
            if (step.RiskLevel > totalRisk)
                totalRisk = step.RiskLevel;
        }

        return new MigrationPath
        {
            FromVersion = versionPath.First(),
            ToVersion = versionPath.Last(),
            Steps = steps,
            TotalCost = totalCost,
            TotalRisk = totalRisk,
            IsDirectPath = versionPath.Count == 2,
            EstimatedDuration = EstimateTotalDuration(steps)
        };
    }

    /// <summary>
    /// Determines the type of migration between versions.
    /// </summary>
    private MigrationType DetermineMigrationType(StateMachineVersion from, StateMachineVersion to)
    {
        if (to.Major > from.Major)
            return MigrationType.MajorUpgrade;
        
        if (to.Minor > from.Minor)
            return MigrationType.MinorUpgrade;
        
        if (to.Patch > from.Patch)
            return MigrationType.PatchUpdate;
        
        return MigrationType.Custom;
    }

    /// <summary>
    /// Estimates the effort required for a migration step.
    /// </summary>
    private MigrationEffort EstimateStepEffort(StateMachineVersion from, StateMachineVersion to)
    {
        if (to.Major > from.Major)
            return MigrationEffort.High;
        
        if (to.Minor > from.Minor)
            return MigrationEffort.Medium;
        
        return MigrationEffort.Low;
    }

    /// <summary>
    /// Assesses the risk level of a migration step.
    /// </summary>
    private RiskLevel AssessStepRisk(StateMachineVersion from, StateMachineVersion to)
    {
        // Major version changes are high risk
        if (to.Major > from.Major)
            return RiskLevel.High;
        
        // Multiple minor versions are medium risk
        if (to.Minor - from.Minor > 2)
            return RiskLevel.Medium;
        
        return RiskLevel.Low;
    }

    /// <summary>
    /// Generates required actions for a migration step.
    /// </summary>
    private List<string> GenerateStepActions(StateMachineVersion from, StateMachineVersion to)
    {
        var actions = new List<string>();

        if (to.Major > from.Major)
        {
            actions.Add("Review breaking changes documentation");
            actions.Add("Update code to handle API changes");
            actions.Add("Run comprehensive test suite");
        }
        else if (to.Minor > from.Minor)
        {
            actions.Add("Review new features and deprecations");
            actions.Add("Update configuration if needed");
            actions.Add("Run integration tests");
        }
        else
        {
            actions.Add("Apply patch update");
            actions.Add("Run basic smoke tests");
        }

        return actions;
    }

    /// <summary>
    /// Generates validation steps for a migration.
    /// </summary>
    private List<string> GenerateValidationSteps(StateMachineVersion from, StateMachineVersion to)
    {
        var steps = new List<string>
        {
            "Verify all state machines activate successfully",
            "Confirm state transitions work as expected",
            "Validate data integrity after migration"
        };

        if (to.Major > from.Major)
        {
            steps.Add("Perform thorough regression testing");
            steps.Add("Monitor for performance degradation");
            steps.Add("Verify backward compatibility if required");
        }

        return steps;
    }

    /// <summary>
    /// Estimates the total duration for a migration path.
    /// </summary>
    private TimeSpan EstimateTotalDuration(List<MigrationStep> steps)
    {
        var totalMinutes = 0;

        foreach (var step in steps)
        {
            totalMinutes += step.EstimatedEffort switch
            {
                MigrationEffort.Low => 30,
                MigrationEffort.Medium => 120,
                MigrationEffort.High => 480,
                _ => 60
            };
        }

        return TimeSpan.FromMinutes(totalMinutes);
    }

    /// <summary>
    /// Selects the optimal path from evaluated paths.
    /// </summary>
    private MigrationPath SelectOptimalPath(List<MigrationPath> paths)
    {
        // Sort by: lowest risk, then lowest cost, then fewest steps
        return paths
            .OrderBy(p => p.TotalRisk)
            .ThenBy(p => p.TotalCost)
            .ThenBy(p => p.Steps.Count)
            .First();
    }

    /// <summary>
    /// Clears cached version graphs.
    /// </summary>
    public void ClearCache()
    {
        _versionGraphs.Clear();
    }
}

/// <summary>
/// Represents a graph of versions and their compatibility relationships.
/// </summary>
internal sealed class VersionGraph
{
    private readonly Dictionary<StateMachineVersion, List<VersionEdge>> _adjacencyList;

    public string GrainType { get; }

    public VersionGraph(string grainType)
    {
        GrainType = grainType;
        _adjacencyList = new Dictionary<StateMachineVersion, List<VersionEdge>>();
    }

    public void AddNode(StateMachineVersion version)
    {
        if (!_adjacencyList.ContainsKey(version))
        {
            _adjacencyList[version] = new List<VersionEdge>();
        }
    }

    public void AddEdge(StateMachineVersion from, StateMachineVersion to, double cost)
    {
        if (!_adjacencyList.ContainsKey(from))
            AddNode(from);

        _adjacencyList[from].Add(new VersionEdge(from, to, cost));
    }

    public IEnumerable<VersionEdge> GetOutgoingEdges(StateMachineVersion version)
    {
        return _adjacencyList.TryGetValue(version, out var edges) 
            ? edges 
            : Enumerable.Empty<VersionEdge>();
    }
}

/// <summary>
/// Represents an edge in the version graph.
/// </summary>
internal sealed class VersionEdge
{
    public StateMachineVersion From { get; }
    public StateMachineVersion To { get; }
    public double Cost { get; }

    public VersionEdge(StateMachineVersion from, StateMachineVersion to, double cost)
    {
        From = from;
        To = to;
        Cost = cost;
    }
}

/// <summary>
/// Evaluates the cost of migrations between versions.
/// </summary>
internal sealed class MigrationCostEvaluator
{
    public double EvaluateMigrationCost(StateMachineVersion from, StateMachineVersion to)
    {
        var cost = 0.0;

        // Major version changes are expensive
        if (to.Major > from.Major)
        {
            cost += (to.Major - from.Major) * 100;
        }

        // Minor version changes have moderate cost
        if (to.Minor > from.Minor)
        {
            cost += (to.Minor - from.Minor) * 10;
        }

        // Patch changes are cheap
        if (to.Patch > from.Patch)
        {
            cost += (to.Patch - from.Patch) * 1;
        }

        // Penalize large jumps
        var versionDistance = Math.Abs(to.CompareTo(from));
        if (versionDistance > 5)
        {
            cost += versionDistance * 5;
        }

        return cost;
    }
}

/// <summary>
/// Risk levels for migrations.
/// </summary>
public enum RiskLevel
{
    /// <summary>
    /// Low risk migration.
    /// </summary>
    Low = 0,
    
    /// <summary>
    /// Medium risk migration.
    /// </summary>
    Medium = 1,
    
    /// <summary>
    /// High risk migration.
    /// </summary>
    High = 2,
    
    /// <summary>
    /// Critical risk migration.
    /// </summary>
    Critical = 3
}