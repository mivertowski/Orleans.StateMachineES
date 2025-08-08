using Microsoft.Extensions.Logging;
using Stateless;

namespace Orleans.StateMachineES.Composition;

/// <summary>
/// Base class for composable state machine components that provides common functionality.
/// </summary>
/// <typeparam name="TState">The type of states in the state machine.</typeparam>
/// <typeparam name="TTrigger">The type of triggers that cause state transitions.</typeparam>
public abstract class ComposableStateMachineBase<TState, TTrigger> : IComposableStateMachine<TState, TTrigger>
    where TState : Enum
    where TTrigger : Enum
{
    protected readonly ILogger _logger;
    private readonly Dictionary<string, TTrigger> _mappableTriggers;
    private readonly HashSet<TState> _exitStates;

    /// <summary>
    /// Initializes a new instance of the composable state machine base class.
    /// </summary>
    /// <param name="componentId">The unique identifier for this component.</param>
    /// <param name="description">The description of this component.</param>
    /// <param name="entryState">The entry state for this component.</param>
    /// <param name="logger">Logger instance.</param>
    protected ComposableStateMachineBase(
        string componentId,
        string description,
        TState entryState,
        ILogger logger)
    {
        ComponentId = componentId ?? throw new ArgumentNullException(nameof(componentId));
        Description = description ?? throw new ArgumentNullException(nameof(description));
        EntryState = entryState;
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _mappableTriggers = new Dictionary<string, TTrigger>();
        _exitStates = new HashSet<TState>();
    }

    /// <inheritdoc />
    public string ComponentId { get; }

    /// <inheritdoc />
    public string Description { get; }

    /// <inheritdoc />
    public TState EntryState { get; }

    /// <inheritdoc />
    public IReadOnlyCollection<TState> ExitStates => _exitStates;

    /// <inheritdoc />
    public IReadOnlyDictionary<string, TTrigger> MappableTriggers => _mappableTriggers;

    /// <inheritdoc />
    public abstract void Configure(StateMachine<TState, TTrigger> stateMachine);

    /// <inheritdoc />
    public virtual CompositionValidationResult Validate()
    {
        var errors = new List<string>();

        // Validate component ID
        if (string.IsNullOrWhiteSpace(ComponentId))
        {
            errors.Add("Component ID cannot be empty");
        }

        // Validate entry state
        if (EntryState == null)
        {
            errors.Add("Entry state must be defined");
        }

        // Validate exit states
        if (!_exitStates.Any())
        {
            errors.Add("At least one exit state must be defined");
        }

        // Perform custom validation
        var customValidation = ValidateComponent();
        if (!customValidation.IsValid)
        {
            errors.AddRange(customValidation.Errors);
        }

        return errors.Any() 
            ? new CompositionValidationResult { IsValid = false, Errors = errors }
            : CompositionValidationResult.Success();
    }

    /// <inheritdoc />
    public virtual async Task OnComponentEntryAsync(CompositionContext context)
    {
        _logger.LogInformation("Entering component {ComponentId} from {ParentId}",
            ComponentId, context.ParentId);

        // Store component-specific data in context
        context.SharedData[$"{ComponentId}_EnteredAt"] = DateTime.UtcNow;
        context.ComponentId = ComponentId;

        await OnEntryAsync(context);
    }

    /// <inheritdoc />
    public virtual async Task OnComponentExitAsync(CompositionContext context)
    {
        _logger.LogInformation("Exiting component {ComponentId} to {ParentId}",
            ComponentId, context.ParentId);

        // Calculate time spent in component
        if (context.SharedData.TryGetValue($"{ComponentId}_EnteredAt", out var enteredAt) && 
            enteredAt is DateTime entryTime)
        {
            var duration = DateTime.UtcNow - entryTime;
            context.SharedData[$"{ComponentId}_Duration"] = duration;
            
            _logger.LogInformation("Component {ComponentId} executed for {Duration}",
                ComponentId, duration);
        }

        await OnExitAsync(context);
    }

    /// <summary>
    /// Adds an exit state to this component.
    /// </summary>
    /// <param name="state">The exit state to add.</param>
    protected void AddExitState(TState state)
    {
        _exitStates.Add(state);
    }

    /// <summary>
    /// Adds multiple exit states to this component.
    /// </summary>
    /// <param name="states">The exit states to add.</param>
    protected void AddExitStates(params TState[] states)
    {
        foreach (var state in states)
        {
            _exitStates.Add(state);
        }
    }

    /// <summary>
    /// Registers a trigger that can be mapped from parent state machines.
    /// </summary>
    /// <param name="key">The mapping key.</param>
    /// <param name="trigger">The trigger to map.</param>
    protected void RegisterMappableTrigger(string key, TTrigger trigger)
    {
        _mappableTriggers[key] = trigger;
    }

    /// <summary>
    /// Registers the default trigger for entering this component.
    /// </summary>
    /// <param name="trigger">The default trigger.</param>
    protected void RegisterDefaultTrigger(TTrigger trigger)
    {
        RegisterMappableTrigger("default", trigger);
    }

    /// <summary>
    /// Performs custom validation for this component.
    /// </summary>
    /// <returns>Validation result.</returns>
    protected virtual CompositionValidationResult ValidateComponent()
    {
        return CompositionValidationResult.Success();
    }

    /// <summary>
    /// Called when the component is entered.
    /// </summary>
    /// <param name="context">The composition context.</param>
    protected virtual Task OnEntryAsync(CompositionContext context)
    {
        return Task.CompletedTask;
    }

    /// <summary>
    /// Called when the component is exited.
    /// </summary>
    /// <param name="context">The composition context.</param>
    protected virtual Task OnExitAsync(CompositionContext context)
    {
        return Task.CompletedTask;
    }

    /// <summary>
    /// Helper method to configure a state with entry and exit actions.
    /// </summary>
    protected void ConfigureState(
        StateMachine<TState, TTrigger> stateMachine,
        TState state,
        Action? onEntry = null,
        Action? onExit = null)
    {
        var config = stateMachine.Configure(state);
        
        if (onEntry != null)
        {
            config.OnEntry(onEntry);
        }
        
        if (onExit != null)
        {
            config.OnExit(onExit);
        }
    }

    /// <summary>
    /// Helper method to configure a state transition.
    /// </summary>
    protected void ConfigureTransition(
        StateMachine<TState, TTrigger> stateMachine,
        TState fromState,
        TTrigger trigger,
        TState toState,
        Func<bool>? guard = null)
    {
        var config = stateMachine.Configure(fromState);
        
        if (guard != null)
        {
            config.PermitIf(trigger, toState, guard);
        }
        else
        {
            config.Permit(trigger, toState);
        }
    }

    /// <summary>
    /// Helper method to configure a reentrant transition.
    /// </summary>
    protected void ConfigureReentrantTransition(
        StateMachine<TState, TTrigger> stateMachine,
        TState state,
        TTrigger trigger,
        Action? action = null)
    {
        var config = stateMachine.Configure(state)
            .PermitReentry(trigger);
        
        if (action != null)
        {
            config.OnEntry(action);
        }
    }

    /// <summary>
    /// Helper method to configure an internal transition.
    /// </summary>
    protected void ConfigureInternalTransition(
        StateMachine<TState, TTrigger> stateMachine,
        TState state,
        TTrigger trigger,
        Action action)
    {
        stateMachine.Configure(state)
            .InternalTransition(trigger, action);
    }
}