using Stateless;

namespace Orleans.StateMachineES.Composition;

/// <summary>
/// Interface for composable state machines that can be combined into larger workflows.
/// </summary>
/// <typeparam name="TState">The type of states in the state machine.</typeparam>
/// <typeparam name="TTrigger">The type of triggers that cause state transitions.</typeparam>
public interface IComposableStateMachine<TState, TTrigger>
    where TState : Enum
    where TTrigger : Enum
{
    /// <summary>
    /// Gets the unique identifier for this state machine component.
    /// </summary>
    string ComponentId { get; }

    /// <summary>
    /// Gets the description of this state machine component.
    /// </summary>
    string Description { get; }

    /// <summary>
    /// Configures the state machine with its states and transitions.
    /// </summary>
    /// <param name="stateMachine">The state machine to configure.</param>
    void Configure(StateMachine<TState, TTrigger> stateMachine);

    /// <summary>
    /// Gets the entry state for this component.
    /// </summary>
    TState EntryState { get; }

    /// <summary>
    /// Gets the exit states for this component.
    /// </summary>
    IReadOnlyCollection<TState> ExitStates { get; }

    /// <summary>
    /// Gets the triggers that can be mapped from parent state machines.
    /// </summary>
    IReadOnlyDictionary<string, TTrigger> MappableTriggers { get; }

    /// <summary>
    /// Validates that this component can be composed with other components.
    /// </summary>
    /// <returns>Validation results.</returns>
    CompositionValidationResult Validate();

    /// <summary>
    /// Called when the component is entered from a parent state machine.
    /// </summary>
    /// <param name="context">The composition context.</param>
    Task OnComponentEntryAsync(CompositionContext context);

    /// <summary>
    /// Called when the component is exited to a parent state machine.
    /// </summary>
    /// <param name="context">The composition context.</param>
    Task OnComponentExitAsync(CompositionContext context);
}

/// <summary>
/// Context passed between composed state machines.
/// </summary>
public class CompositionContext
{
    /// <summary>
    /// The ID of the parent state machine.
    /// </summary>
    public string ParentId { get; set; } = string.Empty;

    /// <summary>
    /// The ID of the current component.
    /// </summary>
    public string ComponentId { get; set; } = string.Empty;

    /// <summary>
    /// Shared data between components.
    /// </summary>
    public Dictionary<string, object> SharedData { get; set; } = [];

    /// <summary>
    /// The grain context if available.
    /// </summary>
    public IGrainContext? GrainContext { get; set; }

    /// <summary>
    /// Correlation ID for tracking across components.
    /// </summary>
    public string CorrelationId { get; set; } = Guid.NewGuid().ToString();

    /// <summary>
    /// Timestamp when the composition started.
    /// </summary>
    public DateTime StartedAt { get; set; } = DateTime.UtcNow;
}

/// <summary>
/// Result of composition validation.
/// </summary>
public class CompositionValidationResult
{
    /// <summary>
    /// Whether the validation passed.
    /// </summary>
    public bool IsValid { get; set; }

    /// <summary>
    /// Validation errors if any.
    /// </summary>
    public List<string> Errors { get; set; } = [];

    /// <summary>
    /// Validation warnings if any.
    /// </summary>
    public List<string> Warnings { get; set; } = [];

    /// <summary>
    /// Creates a successful validation result.
    /// </summary>
    public static CompositionValidationResult Success() => new() { IsValid = true };

    /// <summary>
    /// Creates a failed validation result.
    /// </summary>
    /// <param name="error">The error message.</param>
    public static CompositionValidationResult Failure(string error) => new()
    {
        IsValid = false,
        Errors = [error]
    };
}