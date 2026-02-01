using Stateless;

namespace Orleans.StateMachineES.Templates;

/// <summary>
/// Interface for reusable state machine templates.
/// Templates provide pre-configured state machine patterns for common workflows.
/// </summary>
/// <typeparam name="TState">The type representing the states.</typeparam>
/// <typeparam name="TTrigger">The type representing the triggers.</typeparam>
public interface IStateMachineTemplate<TState, TTrigger>
    where TState : notnull
    where TTrigger : notnull
{
    /// <summary>
    /// The name of this template.
    /// </summary>
    string TemplateName { get; }

    /// <summary>
    /// A description of what this template provides.
    /// </summary>
    string Description { get; }

    /// <summary>
    /// The initial state for state machines using this template.
    /// </summary>
    TState InitialState { get; }

    /// <summary>
    /// Configures the state machine with this template's states and transitions.
    /// </summary>
    /// <param name="stateMachine">The state machine to configure.</param>
    void Configure(StateMachine<TState, TTrigger> stateMachine);

    /// <summary>
    /// Gets default metadata for state machines using this template.
    /// </summary>
    IReadOnlyDictionary<string, object> GetDefaultMetadata();

    /// <summary>
    /// Gets the states defined by this template.
    /// </summary>
    IReadOnlyList<TState> GetDefinedStates();

    /// <summary>
    /// Gets the triggers defined by this template.
    /// </summary>
    IReadOnlyList<TTrigger> GetDefinedTriggers();
}

/// <summary>
/// Base class for state machine templates with common functionality.
/// </summary>
public abstract class StateMachineTemplateBase<TState, TTrigger> : IStateMachineTemplate<TState, TTrigger>
    where TState : notnull
    where TTrigger : notnull
{
    /// <inheritdoc/>
    public abstract string TemplateName { get; }

    /// <inheritdoc/>
    public abstract string Description { get; }

    /// <inheritdoc/>
    public abstract TState InitialState { get; }

    /// <summary>
    /// The states used by this template.
    /// </summary>
    protected List<TState> States { get; } = new();

    /// <summary>
    /// The triggers used by this template.
    /// </summary>
    protected List<TTrigger> Triggers { get; } = new();

    /// <summary>
    /// Default metadata for the template.
    /// </summary>
    protected Dictionary<string, object> Metadata { get; } = new();

    /// <inheritdoc/>
    public abstract void Configure(StateMachine<TState, TTrigger> stateMachine);

    /// <inheritdoc/>
    public virtual IReadOnlyDictionary<string, object> GetDefaultMetadata() => Metadata;

    /// <inheritdoc/>
    public IReadOnlyList<TState> GetDefinedStates() => States;

    /// <inheritdoc/>
    public IReadOnlyList<TTrigger> GetDefinedTriggers() => Triggers;

    /// <summary>
    /// Registers a state as part of this template.
    /// </summary>
    protected void RegisterState(TState state)
    {
        if (!States.Contains(state))
        {
            States.Add(state);
        }
    }

    /// <summary>
    /// Registers a trigger as part of this template.
    /// </summary>
    protected void RegisterTrigger(TTrigger trigger)
    {
        if (!Triggers.Contains(trigger))
        {
            Triggers.Add(trigger);
        }
    }

    /// <summary>
    /// Adds metadata to the template.
    /// </summary>
    protected void AddMetadata(string key, object value)
    {
        Metadata[key] = value;
    }
}
