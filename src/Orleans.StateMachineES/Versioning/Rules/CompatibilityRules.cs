namespace Orleans.StateMachineES.Versioning;

/// <summary>
/// Interface for version compatibility rules.
/// </summary>
public interface ICompatibilityRule
{
    /// <summary>
    /// Gets the name of the rule.
    /// </summary>
    string Name { get; }

    /// <summary>
    /// Gets the category of the rule.
    /// </summary>
    RuleCategory Category { get; }

    /// <summary>
    /// Evaluates the compatibility rule.
    /// </summary>
    Task<RuleResult> EvaluateAsync(CompatibilityContext context);
}

/// <summary>
/// Base class for compatibility rules.
/// </summary>
public abstract class CompatibilityRuleBase : ICompatibilityRule
{
    public abstract string Name { get; }
    public abstract RuleCategory Category { get; }
    
    public abstract Task<RuleResult> EvaluateAsync(CompatibilityContext context);

    protected static RuleResult Success() => new() { Success = true };
    
    protected static RuleResult Failure(string reason) => new()
    { 
        Success = false, 
        FailureReason = reason 
    };

    protected static RuleResult WithBreakingChange(BreakingChange breakingChange)
    {
        var result = new RuleResult { Success = true };
        result.BreakingChanges.Add(breakingChange);
        return result;
    }

    protected static RuleResult WithWarning(string warning)
    {
        var result = new RuleResult { Success = true };
        result.Warnings.Add(warning);
        return result;
    }
}

/// <summary>
/// Rule for checking major version compatibility.
/// </summary>
public sealed class MajorVersionRule : CompatibilityRuleBase
{
    public override string Name => "Major Version Compatibility";
    public override RuleCategory Category => RuleCategory.Critical;

    public override Task<RuleResult> EvaluateAsync(CompatibilityContext context)
    {
        if (context.ToVersion.Major > context.FromVersion.Major)
        {
            return Task.FromResult(WithBreakingChange(new BreakingChange
            {
                ChangeType = BreakingChangeType.MajorVersionIncrease,
                Description = $"Major version increase from {context.FromVersion.Major} to {context.ToVersion.Major}",
                Impact = BreakingChangeImpact.Critical,
                Mitigation = "Review all breaking changes and update code accordingly"
            }));
        }

        return Task.FromResult(Success());
    }
}

/// <summary>
/// Rule for checking minor version compatibility.
/// </summary>
public sealed class MinorVersionRule : CompatibilityRuleBase
{
    public override string Name => "Minor Version Compatibility";
    public override RuleCategory Category => RuleCategory.Important;

    public override Task<RuleResult> EvaluateAsync(CompatibilityContext context)
    {
        if (context.ToVersion.Minor > context.FromVersion.Minor && 
            context.ToVersion.Major == context.FromVersion.Major)
        {
            return Task.FromResult(WithWarning(
                $"Minor version increase from {context.FromVersion.Minor} to {context.ToVersion.Minor}. " +
                "New features may be available."));
        }

        return Task.FromResult(Success());
    }
}

/// <summary>
/// Rule for checking patch version compatibility.
/// </summary>
public sealed class PatchVersionRule : CompatibilityRuleBase
{
    public override string Name => "Patch Version Compatibility";
    public override RuleCategory Category => RuleCategory.Informational;

    public override Task<RuleResult> EvaluateAsync(CompatibilityContext context)
    {
        if (context.ToVersion.Patch > context.FromVersion.Patch && 
            context.ToVersion.Major == context.FromVersion.Major &&
            context.ToVersion.Minor == context.FromVersion.Minor)
        {
            return Task.FromResult(Success()); // Patches are always compatible
        }

        return Task.FromResult(Success());
    }
}

/// <summary>
/// Rule for checking backward compatibility.
/// </summary>
public sealed class BackwardCompatibilityRule : CompatibilityRuleBase
{
    public override string Name => "Backward Compatibility";
    public override RuleCategory Category => RuleCategory.Critical;

    public override Task<RuleResult> EvaluateAsync(CompatibilityContext context)
    {
        // Check if downgrading
        if (context.ToVersion.CompareTo(context.FromVersion) < 0)
        {
            return Task.FromResult(WithBreakingChange(new BreakingChange
            {
                ChangeType = BreakingChangeType.DowngradeAttempt,
                Description = $"Attempting to downgrade from {context.FromVersion} to {context.ToVersion}",
                Impact = BreakingChangeImpact.Critical,
                Mitigation = "Downgrades require special migration procedures"
            }));
        }

        return Task.FromResult(Success());
    }
}

/// <summary>
/// Rule for checking forward compatibility.
/// </summary>
public sealed class ForwardCompatibilityRule : CompatibilityRuleBase
{
    public override string Name => "Forward Compatibility";
    public override RuleCategory Category => RuleCategory.Important;

    public override Task<RuleResult> EvaluateAsync(CompatibilityContext context)
    {
        // Skip more than 2 major versions
        if (context.ToVersion.Major - context.FromVersion.Major > 2)
        {
            return Task.FromResult(WithWarning(
                $"Large version jump from {context.FromVersion} to {context.ToVersion}. " +
                "Consider incremental upgrades for safety."));
        }

        return Task.FromResult(Success());
    }
}

/// <summary>
/// Rule for checking state additions.
/// </summary>
public sealed class StateAdditionRule : CompatibilityRuleBase
{
    public override string Name => "State Addition";
    public override RuleCategory Category => RuleCategory.Standard;

    public override Task<RuleResult> EvaluateAsync(CompatibilityContext context)
    {
        if (context.StateChanges?.AddedStates?.Any() == true)
        {
            var result = new RuleResult { Success = true };
            
            foreach (var state in context.StateChanges.AddedStates)
            {
                result.Warnings.Add($"New state added: {state}. Ensure proper handling in existing code.");
            }

            return Task.FromResult(result);
        }

        return Task.FromResult(Success());
    }
}

/// <summary>
/// Rule for checking state removals.
/// </summary>
public sealed class StateRemovalRule : CompatibilityRuleBase
{
    public override string Name => "State Removal";
    public override RuleCategory Category => RuleCategory.Critical;

    public override Task<RuleResult> EvaluateAsync(CompatibilityContext context)
    {
        if (context.StateChanges?.RemovedStates?.Any() == true)
        {
            var result = new RuleResult { Success = true };
            
            foreach (var state in context.StateChanges.RemovedStates)
            {
                result.BreakingChanges.Add(new BreakingChange
                {
                    ChangeType = BreakingChangeType.StateRemoved,
                    Description = $"State '{state}' has been removed",
                    Impact = BreakingChangeImpact.High,
                    Mitigation = $"Migrate existing instances in state '{state}' before upgrading"
                });
            }

            return Task.FromResult(result);
        }

        return Task.FromResult(Success());
    }
}

/// <summary>
/// Rule for checking trigger modifications.
/// </summary>
public sealed class TriggerModificationRule : CompatibilityRuleBase
{
    public override string Name => "Trigger Modification";
    public override RuleCategory Category => RuleCategory.Important;

    public override Task<RuleResult> EvaluateAsync(CompatibilityContext context)
    {
        var result = new RuleResult { Success = true };

        if (context.TriggerChanges?.RemovedTriggers?.Any() == true)
        {
            foreach (var trigger in context.TriggerChanges.RemovedTriggers)
            {
                result.BreakingChanges.Add(new BreakingChange
                {
                    ChangeType = BreakingChangeType.TriggerRemoved,
                    Description = $"Trigger '{trigger}' has been removed",
                    Impact = BreakingChangeImpact.Medium,
                    Mitigation = $"Remove or replace usage of trigger '{trigger}'"
                });
            }
        }

        if (context.TriggerChanges?.AddedTriggers?.Any() == true)
        {
            foreach (var trigger in context.TriggerChanges.AddedTriggers)
            {
                result.Warnings.Add($"New trigger added: {trigger}");
            }
        }

        return Task.FromResult(result);
    }
}

/// <summary>
/// Rule for checking guard condition changes.
/// </summary>
public sealed class GuardConditionRule : CompatibilityRuleBase
{
    public override string Name => "Guard Condition";
    public override RuleCategory Category => RuleCategory.Standard;

    public override Task<RuleResult> EvaluateAsync(CompatibilityContext context)
    {
        if (context.GuardChanges?.Any() == true)
        {
            var result = new RuleResult { Success = true };

            foreach (var change in context.GuardChanges)
            {
                result.BreakingChanges.Add(new BreakingChange
                {
                    ChangeType = BreakingChangeType.GuardChanged,
                    Description = $"Guard condition changed in state '{change.State}'",
                    Impact = BreakingChangeImpact.Low,
                    Mitigation = "Test guard conditions thoroughly"
                });
            }

            return Task.FromResult(result);
        }

        return Task.FromResult(Success());
    }
}

/// <summary>
/// Rule for checking transition modifications.
/// </summary>
public sealed class TransitionModificationRule : CompatibilityRuleBase
{
    public override string Name => "Transition Modification";
    public override RuleCategory Category => RuleCategory.Important;

    public override Task<RuleResult> EvaluateAsync(CompatibilityContext context)
    {
        if (context.TransitionChanges?.Any() == true)
        {
            var result = new RuleResult { Success = true };

            foreach (var change in context.TransitionChanges)
            {
                var impact = DetermineTransitionImpact(change);
                
                result.BreakingChanges.Add(new BreakingChange
                {
                    ChangeType = BreakingChangeType.TransitionChanged,
                    Description = $"Transition changed: {change.Description}",
                    Impact = impact,
                    Mitigation = GenerateTransitionMitigation(change)
                });
            }

            return Task.FromResult(result);
        }

        return Task.FromResult(Success());
    }

    private static BreakingChangeImpact DetermineTransitionImpact(TransitionChange change)
    {
        // Determine impact based on transition type
        if (change.IsRemoval)
            return BreakingChangeImpact.High;
        
        if (change.IsTargetStateChange)
            return BreakingChangeImpact.Medium;
        
        return BreakingChangeImpact.Low;
    }

    private static string GenerateTransitionMitigation(TransitionChange change)
    {
        if (change.IsRemoval)
            return $"Remove code that relies on transition from {change.FromState} via {change.Trigger}";
        
        if (change.IsTargetStateChange)
            return $"Update expectations for transition from {change.FromState} via {change.Trigger} to new target state";
        
        return "Review and test transition behavior";
    }
}

/// <summary>
/// Rule for checking serialization compatibility.
/// </summary>
public sealed class SerializationCompatibilityRule : CompatibilityRuleBase
{
    public override string Name => "Serialization Compatibility";
    public override RuleCategory Category => RuleCategory.Critical;

    public override Task<RuleResult> EvaluateAsync(CompatibilityContext context)
    {
        if (context.DataFormatChanged)
        {
            return Task.FromResult(WithBreakingChange(new BreakingChange
            {
                ChangeType = BreakingChangeType.DataFormatChanged,
                Description = "State data serialization format has changed",
                Impact = BreakingChangeImpact.Critical,
                Mitigation = "Implement data migration for existing state data"
            }));
        }

        return Task.FromResult(Success());
    }
}

/// <summary>
/// Rule for checking state data migration requirements.
/// </summary>
public sealed class StateDataMigrationRule : CompatibilityRuleBase
{
    public override string Name => "State Data Migration";
    public override RuleCategory Category => RuleCategory.Important;

    public override Task<RuleResult> EvaluateAsync(CompatibilityContext context)
    {
        if (context.RequiresDataMigration)
        {
            var result = new RuleResult { Success = true };
            
            result.Warnings.Add("State data migration may be required. Ensure migration scripts are prepared.");
            
            if (context.MigrationComplexity == MigrationComplexity.High)
            {
                result.BreakingChanges.Add(new BreakingChange
                {
                    ChangeType = BreakingChangeType.ComplexMigration,
                    Description = "Complex data migration required",
                    Impact = BreakingChangeImpact.High,
                    Mitigation = "Prepare and test comprehensive migration scripts"
                });
            }

            return Task.FromResult(result);
        }

        return Task.FromResult(Success());
    }
}

/// <summary>
/// Rule categories for prioritization.
/// </summary>
public enum RuleCategory
{
    /// <summary>
    /// Critical rules that must pass.
    /// </summary>
    Critical,
    
    /// <summary>
    /// Important rules that should pass.
    /// </summary>
    Important,
    
    /// <summary>
    /// Standard rules for general compatibility.
    /// </summary>
    Standard,
    
    /// <summary>
    /// Informational rules for awareness.
    /// </summary>
    Informational
}

/// <summary>
/// Result from a rule evaluation.
/// </summary>
public sealed class RuleResult
{
    /// <summary>
    /// Gets or sets the rule name.
    /// </summary>
    public string RuleName { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the rule category.
    /// </summary>
    public RuleCategory RuleCategory { get; set; }

    /// <summary>
    /// Gets or sets whether the rule passed.
    /// </summary>
    public bool Success { get; set; }

    /// <summary>
    /// Gets or sets the failure reason if applicable.
    /// </summary>
    public string? FailureReason { get; set; }

    /// <summary>
    /// Gets or sets any error that occurred.
    /// </summary>
    public string? Error { get; set; }

    /// <summary>
    /// Gets the list of breaking changes detected.
    /// </summary>
    public List<BreakingChange> BreakingChanges { get; } = [];

    /// <summary>
    /// Gets the list of warnings.
    /// </summary>
    public List<string> Warnings { get; } = [];

    /// <summary>
    /// Gets whether there are breaking changes.
    /// </summary>
    public bool HasBreakingChanges => BreakingChanges.Count > 0;

    /// <summary>
    /// Gets whether there are warnings.
    /// </summary>
    public bool HasWarnings => Warnings.Count > 0;
}