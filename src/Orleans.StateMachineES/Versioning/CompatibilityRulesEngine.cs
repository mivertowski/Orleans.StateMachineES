using Microsoft.Extensions.Logging;

namespace Orleans.StateMachineES.Versioning;

/// <summary>
/// Engine for evaluating version compatibility rules and detecting breaking changes.
/// Provides a rule-based system for determining compatibility between state machine versions.
/// </summary>
public sealed class CompatibilityRulesEngine
{
    private readonly List<ICompatibilityRule> _rules;
    private readonly ILogger? _logger;
    private readonly Dictionary<string, RuleEvaluationStats> _ruleStats;

    /// <summary>
    /// Initializes a new instance of the CompatibilityRulesEngine class.
    /// </summary>
    /// <param name="logger">Optional logger for diagnostics.</param>
    public CompatibilityRulesEngine(ILogger? logger = null)
    {
        _logger = logger;
        _rules = [];
        _ruleStats = [];
        
        InitializeDefaultRules();
    }

    /// <summary>
    /// Initializes the default set of compatibility rules.
    /// </summary>
    private void InitializeDefaultRules()
    {
        // Version-based rules
        _rules.Add(new MajorVersionRule());
        _rules.Add(new MinorVersionRule());
        _rules.Add(new PatchVersionRule());
        
        // Semantic versioning rules
        _rules.Add(new BackwardCompatibilityRule());
        _rules.Add(new ForwardCompatibilityRule());
        
        // State machine specific rules
        _rules.Add(new StateAdditionRule());
        _rules.Add(new StateRemovalRule());
        _rules.Add(new TriggerModificationRule());
        _rules.Add(new GuardConditionRule());
        _rules.Add(new TransitionModificationRule());
        
        // Data compatibility rules
        _rules.Add(new SerializationCompatibilityRule());
        _rules.Add(new StateDataMigrationRule());
    }

    /// <summary>
    /// Evaluates compatibility between two versions using all registered rules.
    /// </summary>
    /// <param name="context">The compatibility evaluation context.</param>
    /// <returns>A detailed compatibility result.</returns>
    public async Task<CompatibilityResult> EvaluateCompatibilityAsync(CompatibilityContext context)
    {
        if (context == null)
            throw new ArgumentNullException(nameof(context));

        var result = new CompatibilityResult
        {
            FromVersion = context.FromVersion,
            ToVersion = context.ToVersion,
            EvaluationTime = DateTime.UtcNow
        };

        _logger?.LogDebug("Evaluating compatibility from {FromVersion} to {ToVersion}",
            context.FromVersion, context.ToVersion);

        var evaluationTasks = _rules.Select(rule => EvaluateRuleAsync(rule, context)).ToList();
        var ruleResults = await Task.WhenAll(evaluationTasks).ConfigureAwait(false);

        foreach (var ruleResult in ruleResults)
        {
            result.RuleResults.Add(ruleResult);
            
            if (ruleResult.HasBreakingChanges)
            {
                result.BreakingChanges.AddRange(ruleResult.BreakingChanges);
            }
            
            if (ruleResult.HasWarnings)
            {
                result.Warnings.AddRange(ruleResult.Warnings);
            }

            // Track statistics
            TrackRuleEvaluation(ruleResult);
        }

        // Determine overall compatibility
        result.IsCompatible = DetermineOverallCompatibility(result);
        result.CompatibilityLevel = DetermineCompatibilityLevel(result);
        result.RequiredMigrationSteps = GenerateMigrationSteps(result);

        _logger?.LogInformation(
            "Compatibility evaluation complete: {IsCompatible}, Level: {Level}, Breaking Changes: {BreakingCount}",
            result.IsCompatible, result.CompatibilityLevel, result.BreakingChanges.Count);

        return result;
    }

    /// <summary>
    /// Evaluates a single compatibility rule.
    /// </summary>
    private async Task<RuleResult> EvaluateRuleAsync(ICompatibilityRule rule, CompatibilityContext context)
    {
        try
        {
            var result = await rule.EvaluateAsync(context).ConfigureAwait(false);
            result.RuleName = rule.Name;
            result.RuleCategory = rule.Category;
            return result;
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "Error evaluating rule {RuleName}", rule.Name);
            
            return new RuleResult
            {
                RuleName = rule.Name,
                RuleCategory = rule.Category,
                Success = false,
                Error = ex.Message
            };
        }
    }

    /// <summary>
    /// Determines the overall compatibility based on rule results.
    /// </summary>
    private static bool DetermineOverallCompatibility(CompatibilityResult result)
    {
        // Incompatible if any critical breaking changes
        if (result.BreakingChanges.Any(bc => bc.Impact == BreakingChangeImpact.Critical))
            return false;

        // Incompatible if too many high-impact breaking changes
        var highImpactCount = result.BreakingChanges.Count(bc => bc.Impact == BreakingChangeImpact.High);
        if (highImpactCount > 3)
            return false;

        // Check for failed critical rules
        var criticalRuleFailed = result.RuleResults.Any(r => 
            r.RuleCategory == RuleCategory.Critical && !r.Success);
        
        return !criticalRuleFailed;
    }

    /// <summary>
    /// Determines the compatibility level based on evaluation results.
    /// </summary>
    private static VersionCompatibilityLevel DetermineCompatibilityLevel(CompatibilityResult result)
    {
        if (!result.IsCompatible)
            return VersionCompatibilityLevel.Incompatible;

        if (result.BreakingChanges.Count == 0 && result.Warnings.Count == 0)
            return VersionCompatibilityLevel.FullyCompatible;

        if (result.BreakingChanges.Count == 0)
            return VersionCompatibilityLevel.Compatible;

        if (result.BreakingChanges.All(bc => bc.Impact == BreakingChangeImpact.Low))
            return VersionCompatibilityLevel.PartiallyCompatible;

        return VersionCompatibilityLevel.RequiresMigration;
    }

    /// <summary>
    /// Generates migration steps based on breaking changes.
    /// </summary>
    private List<MigrationStep> GenerateMigrationSteps(CompatibilityResult result)
    {
        var steps = new List<MigrationStep>();
        var stepOrder = 1;

        // Group breaking changes by type
        var groupedChanges = result.BreakingChanges.GroupBy(bc => bc.ChangeType);

        foreach (var group in groupedChanges.OrderBy(g => GetMigrationPriority(g.Key)))
        {
            var step = new MigrationStep
            {
                Order = stepOrder++,
                Type = MapToMigrationType(group.Key),
                Description = GenerateStepDescription(group.Key, [.. group]),
                RequiredActions = GenerateRequiredActions(group.Key, [.. group]),
                EstimatedEffort = EstimateEffort([.. group]),
                AutomationAvailable = CheckAutomationAvailability(group.Key)
            };

            steps.Add(step);
        }

        return steps;
    }

    /// <summary>
    /// Gets the migration priority for a change type.
    /// </summary>
    private static int GetMigrationPriority(BreakingChangeType changeType)
    {
        return changeType switch
        {
            BreakingChangeType.StateRemoved => 1,
            BreakingChangeType.TriggerRemoved => 2,
            BreakingChangeType.TransitionChanged => 3,
            BreakingChangeType.GuardChanged => 4,
            BreakingChangeType.StateAdded => 5,
            BreakingChangeType.TriggerAdded => 6,
            _ => 99
        };
    }

    /// <summary>
    /// Maps breaking change type to migration type.
    /// </summary>
    private static MigrationType MapToMigrationType(BreakingChangeType changeType)
    {
        return changeType switch
        {
            BreakingChangeType.StateRemoved => MigrationType.StateRemoval,
            BreakingChangeType.StateAdded => MigrationType.StateAddition,
            BreakingChangeType.TriggerRemoved => MigrationType.TriggerRemoval,
            BreakingChangeType.TriggerAdded => MigrationType.TriggerAddition,
            BreakingChangeType.TransitionChanged => MigrationType.TransitionModification,
            BreakingChangeType.GuardChanged => MigrationType.GuardModification,
            BreakingChangeType.DataFormatChanged => MigrationType.DataMigration,
            _ => MigrationType.Custom
        };
    }

    /// <summary>
    /// Generates a description for a migration step.
    /// </summary>
    private static string GenerateStepDescription(BreakingChangeType changeType, List<BreakingChange> changes)
    {
        var count = changes.Count;
        var plural = count > 1 ? "s" : "";
        
        return changeType switch
        {
            BreakingChangeType.StateRemoved => 
                $"Handle removal of {count} state{plural}",
            BreakingChangeType.TriggerRemoved => 
                $"Update code for {count} removed trigger{plural}",
            BreakingChangeType.TransitionChanged => 
                $"Adjust {count} modified transition{plural}",
            _ => $"Address {count} {changeType} change{plural}"
        };
    }

    /// <summary>
    /// Generates required actions for a migration step.
    /// </summary>
    private static List<string> GenerateRequiredActions(BreakingChangeType changeType, List<BreakingChange> changes)
    {
        var actions = new List<string>();

        foreach (var change in changes)
        {
            if (!string.IsNullOrEmpty(change.Mitigation))
            {
                actions.Add(change.Mitigation);
            }
        }

        // Add generic actions if no specific mitigations
        if (actions.Count == 0)
        {
            actions.Add($"Review and update code affected by {changeType}");
            actions.Add("Test thoroughly after changes");
        }

        return actions;
    }

    /// <summary>
    /// Estimates the effort required for migration.
    /// </summary>
    private static MigrationEffort EstimateEffort(List<BreakingChange> changes)
    {
        var maxImpact = changes.Max(c => c.Impact);
        var changeCount = changes.Count;

        if (maxImpact == BreakingChangeImpact.Critical || changeCount > 10)
            return MigrationEffort.High;

        if (maxImpact == BreakingChangeImpact.High || changeCount > 5)
            return MigrationEffort.Medium;

        return MigrationEffort.Low;
    }

    /// <summary>
    /// Checks if automation is available for a change type.
    /// </summary>
    private static bool CheckAutomationAvailability(BreakingChangeType changeType)
    {
        // These change types have automation support
        return changeType switch
        {
            BreakingChangeType.StateAdded => true,
            BreakingChangeType.TriggerAdded => true,
            BreakingChangeType.DataFormatChanged => true,
            _ => false
        };
    }

    /// <summary>
    /// Tracks rule evaluation statistics.
    /// </summary>
    private void TrackRuleEvaluation(RuleResult result)
    {
        if (!_ruleStats.TryGetValue(result.RuleName, out var stats))
        {
            stats = new RuleEvaluationStats { RuleName = result.RuleName };
            _ruleStats[result.RuleName] = stats;
        }

        stats.TotalEvaluations++;
        if (result.Success) stats.SuccessfulEvaluations++;
        if (result.HasBreakingChanges) stats.BreakingChangeDetections++;
        if (result.HasWarnings) stats.WarningDetections++;
    }

    /// <summary>
    /// Registers a custom compatibility rule.
    /// </summary>
    public void RegisterRule(ICompatibilityRule rule)
    {
        if (rule == null)
            throw new ArgumentNullException(nameof(rule));

        _rules.Add(rule);
        _logger?.LogDebug("Registered custom rule: {RuleName}", rule.Name);
    }

    /// <summary>
    /// Gets statistics for all rule evaluations.
    /// </summary>
    public IReadOnlyDictionary<string, RuleEvaluationStats> GetStatistics()
    {
        return _ruleStats;
    }

    /// <summary>
    /// Clears all evaluation statistics.
    /// </summary>
    public void ClearStatistics()
    {
        _ruleStats.Clear();
    }
}

/// <summary>
/// Statistics for rule evaluations.
/// </summary>
public sealed class RuleEvaluationStats
{
    /// <summary>
    /// Gets or sets the rule name.
    /// </summary>
    public string RuleName { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the total number of evaluations.
    /// </summary>
    public int TotalEvaluations { get; set; }

    /// <summary>
    /// Gets or sets the number of successful evaluations.
    /// </summary>
    public int SuccessfulEvaluations { get; set; }

    /// <summary>
    /// Gets or sets the number of breaking changes detected.
    /// </summary>
    public int BreakingChangeDetections { get; set; }

    /// <summary>
    /// Gets or sets the number of warnings detected.
    /// </summary>
    public int WarningDetections { get; set; }

    /// <summary>
    /// Gets the success rate percentage.
    /// </summary>
    public double SuccessRate => 
        TotalEvaluations > 0 ? (SuccessfulEvaluations * 100.0) / TotalEvaluations : 0;
}