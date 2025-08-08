using Microsoft.Extensions.Logging;
using Stateless;

namespace Orleans.StateMachineES.Composition.Components;

/// <summary>
/// Reusable validation component that can be composed into state machines
/// to add validation states and transitions.
/// </summary>
/// <typeparam name="TState">The type of states in the state machine.</typeparam>
/// <typeparam name="TTrigger">The type of triggers that cause state transitions.</typeparam>
public class ValidationComponent<TState, TTrigger> : ComposableStateMachineBase<TState, TTrigger>
    where TState : Enum
    where TTrigger : Enum
{
    private readonly TState _validatingState;
    private readonly TState _validState;
    private readonly TState _invalidState;
    private readonly TTrigger _startValidation;
    private readonly TTrigger _validationSucceeded;
    private readonly TTrigger _validationFailed;
    private readonly Func<bool>? _validationLogic;

    /// <summary>
    /// Initializes a new instance of the validation component.
    /// </summary>
    public ValidationComponent(
        string componentId,
        TState entryState,
        TState validatingState,
        TState validState,
        TState invalidState,
        TTrigger startValidation,
        TTrigger validationSucceeded,
        TTrigger validationFailed,
        Func<bool>? validationLogic,
        ILogger logger)
        : base(
            componentId,
            "Validation component for input validation and business rule checking",
            entryState,
            logger)
    {
        _validatingState = validatingState;
        _validState = validState;
        _invalidState = invalidState;
        _startValidation = startValidation;
        _validationSucceeded = validationSucceeded;
        _validationFailed = validationFailed;
        _validationLogic = validationLogic;

        // Register exit states
        AddExitStates(_validState, _invalidState);

        // Register mappable triggers
        RegisterDefaultTrigger(_startValidation);
        RegisterMappableTrigger("validate", _startValidation);
        RegisterMappableTrigger("success", _validationSucceeded);
        RegisterMappableTrigger("failure", _validationFailed);
    }

    /// <inheritdoc />
    public override void Configure(StateMachine<TState, TTrigger> stateMachine)
    {
        // Configure entry state to validation state
        ConfigureTransition(stateMachine, EntryState, _startValidation, _validatingState);

        // Configure validation state
        stateMachine.Configure(_validatingState)
            .OnEntry(() => PerformValidation(stateMachine))
            .Permit(_validationSucceeded, _validState)
            .Permit(_validationFailed, _invalidState);

        // Configure valid state
        ConfigureState(stateMachine, _validState,
            onEntry: () => _logger.LogInformation("Validation succeeded, entering valid state"));

        // Configure invalid state
        ConfigureState(stateMachine, _invalidState,
            onEntry: () => _logger.LogInformation("Validation failed, entering invalid state"));
    }

    /// <summary>
    /// Performs the validation logic.
    /// </summary>
    private void PerformValidation(StateMachine<TState, TTrigger> stateMachine)
    {
        _logger.LogInformation("Performing validation in component {ComponentId}", ComponentId);

        try
        {
            bool isValid = _validationLogic?.Invoke() ?? true;

            if (isValid)
            {
                _logger.LogInformation("Validation passed");
                stateMachine.Fire(_validationSucceeded);
            }
            else
            {
                _logger.LogWarning("Validation failed");
                stateMachine.Fire(_validationFailed);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error during validation");
            stateMachine.Fire(_validationFailed);
        }
    }

    /// <inheritdoc />
    protected override CompositionValidationResult ValidateComponent()
    {
        var errors = new List<string>();

        // Ensure all states are different
        var states = new[] { EntryState, _validatingState, _validState, _invalidState };
        if (states.Distinct().Count() != states.Length)
        {
            errors.Add("All states must be unique");
        }

        // Ensure all triggers are different
        var triggers = new[] { _startValidation, _validationSucceeded, _validationFailed };
        if (triggers.Distinct().Count() != triggers.Length)
        {
            errors.Add("All triggers must be unique");
        }

        return errors.Any()
            ? new CompositionValidationResult { IsValid = false, Errors = errors }
            : CompositionValidationResult.Success();
    }

    /// <inheritdoc />
    protected override async Task OnEntryAsync(CompositionContext context)
    {
        context.SharedData[$"{ComponentId}_StartTime"] = DateTime.UtcNow;
        await base.OnEntryAsync(context);
    }

    /// <inheritdoc />
    protected override async Task OnExitAsync(CompositionContext context)
    {
        if (context.SharedData.TryGetValue($"{ComponentId}_StartTime", out var startTime) &&
            startTime is DateTime start)
        {
            var duration = DateTime.UtcNow - start;
            _logger.LogInformation("Validation component {ComponentId} completed in {Duration}ms",
                ComponentId, duration.TotalMilliseconds);
        }

        await base.OnExitAsync(context);
    }
}