namespace Orleans.StateMachineES.Visualization;

/// <summary>
/// Comprehensive analysis of a state machine's structure and properties.
/// </summary>
public class StateMachineAnalysis
{
    /// <summary>
    /// Type name of the state machine.
    /// </summary>
    public string StateMachineType { get; set; } = string.Empty;

    /// <summary>
    /// Type name of the states.
    /// </summary>
    public string StateType { get; set; } = string.Empty;

    /// <summary>
    /// Type name of the triggers.
    /// </summary>
    public string TriggerType { get; set; } = string.Empty;

    /// <summary>
    /// When this analysis was generated.
    /// </summary>
    public DateTime GeneratedAt { get; set; }

    /// <summary>
    /// Current state of the state machine at analysis time.
    /// </summary>
    public string CurrentState { get; set; } = string.Empty;

    /// <summary>
    /// Information about all states in the state machine.
    /// </summary>
    public List<StateInfo> States { get; set; } = [];

    /// <summary>
    /// Information about all triggers in the state machine.
    /// </summary>
    public List<TriggerInfo> Triggers { get; set; } = [];

    /// <summary>
    /// Complexity metrics for the state machine.
    /// </summary>
    public ComplexityMetrics Metrics { get; set; } = new();
}

/// <summary>
/// Information about a specific state in the state machine.
/// </summary>
public class StateInfo
{
    /// <summary>
    /// Name of the state.
    /// </summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>
    /// Whether this is the initial state.
    /// </summary>
    public bool IsInitial { get; set; }

    /// <summary>
    /// Whether this is the current state.
    /// </summary>
    public bool IsCurrent { get; set; }

    /// <summary>
    /// Entry actions configured for this state.
    /// </summary>
    public List<string> EntryActions { get; set; } = [];

    /// <summary>
    /// Exit actions configured for this state.
    /// </summary>
    public List<string> ExitActions { get; set; } = [];

    /// <summary>
    /// Number of internal transitions (self-loops) for this state.
    /// </summary>
    public int InternalTransitions { get; set; }

    /// <summary>
    /// Substates if this is a hierarchical state machine.
    /// </summary>
    public List<string> Substates { get; set; } = [];
}

/// <summary>
/// Information about a specific trigger in the state machine.
/// </summary>
public class TriggerInfo
{
    /// <summary>
    /// Name of the trigger.
    /// </summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>
    /// How many times this trigger is used across all states.
    /// </summary>
    public int UsageCount { get; set; }

    /// <summary>
    /// States that can fire this trigger.
    /// </summary>
    public List<string> SourceStates { get; set; } = [];

    /// <summary>
    /// States that this trigger can transition to.
    /// </summary>
    public List<string> TargetStates { get; set; } = [];

    /// <summary>
    /// Whether this trigger has guard conditions.
    /// </summary>
    public bool HasGuards { get; set; }

    /// <summary>
    /// Whether this trigger accepts parameters.
    /// </summary>
    public bool HasParameters { get; set; }
}
