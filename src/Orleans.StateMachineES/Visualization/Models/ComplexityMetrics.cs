namespace Orleans.StateMachineES.Visualization;

/// <summary>
/// Complexity metrics for analyzing state machine design quality.
/// </summary>
public class ComplexityMetrics
{
    /// <summary>
    /// Total number of states.
    /// </summary>
    public int StateCount { get; set; }

    /// <summary>
    /// Total number of unique triggers.
    /// </summary>
    public int TriggerCount { get; set; }

    /// <summary>
    /// Total number of state transitions.
    /// </summary>
    public int TransitionCount { get; set; }

    /// <summary>
    /// Cyclomatic complexity measure.
    /// </summary>
    public int CyclomaticComplexity { get; set; }

    /// <summary>
    /// Maximum depth of state hierarchy.
    /// </summary>
    public int MaxDepth { get; set; }

    /// <summary>
    /// Connectivity index (0-1, higher means more interconnected).
    /// </summary>
    public double ConnectivityIndex { get; set; }

    /// <summary>
    /// Complexity assessment based on metrics.
    /// </summary>
    public ComplexityLevel ComplexityLevel => CalculateComplexityLevel();

    private ComplexityLevel CalculateComplexityLevel()
    {
        var score = 0;

        if (StateCount > 20) score += 2;
        else if (StateCount > 10) score += 1;

        if (TriggerCount > 15) score += 2;
        else if (TriggerCount > 8) score += 1;

        if (CyclomaticComplexity > 20) score += 3;
        else if (CyclomaticComplexity > 10) score += 2;
        else if (CyclomaticComplexity > 5) score += 1;

        if (MaxDepth > 4) score += 2;
        else if (MaxDepth > 2) score += 1;

        return score switch
        {
            <= 2 => ComplexityLevel.Low,
            <= 5 => ComplexityLevel.Medium,
            <= 8 => ComplexityLevel.High,
            _ => ComplexityLevel.VeryHigh
        };
    }
}

/// <summary>
/// Complexity levels for state machine design assessment.
/// </summary>
public enum ComplexityLevel
{
    Low,
    Medium,
    High,
    VeryHigh
}
