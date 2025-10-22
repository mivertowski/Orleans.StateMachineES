using Microsoft.Extensions.Logging;
using Stateless;

namespace Orleans.StateMachineES.Composition.Components;

/// <summary>
/// Reusable retry component that can be composed into state machines
/// to add retry logic with configurable attempts and backoff strategies.
/// </summary>
/// <typeparam name="TState">The type of states in the state machine.</typeparam>
/// <typeparam name="TTrigger">The type of triggers that cause state transitions.</typeparam>
public class RetryComponent<TState, TTrigger> : ComposableStateMachineBase<TState, TTrigger>
    where TState : Enum
    where TTrigger : Enum
{
    private readonly TState _attemptingState;
    private readonly TState _successState;
    private readonly TState _failedState;
    private readonly TState _retryingState;
    private readonly TTrigger _attempt;
    private readonly TTrigger _succeed;
    private readonly TTrigger _fail;
    private readonly TTrigger _retry;
    private readonly TTrigger _exhausted;
    private readonly int _maxAttempts;
    private readonly TimeSpan _retryDelay;
    private readonly BackoffStrategy _backoffStrategy;
    private int _currentAttempt;

    /// <summary>
    /// Initializes a new instance of the retry component.
    /// </summary>
    public RetryComponent(
        string componentId,
        TState entryState,
        TState attemptingState,
        TState successState,
        TState failedState,
        TState retryingState,
        TTrigger attempt,
        TTrigger succeed,
        TTrigger fail,
        TTrigger retry,
        TTrigger exhausted,
        int maxAttempts,
        TimeSpan retryDelay,
        BackoffStrategy backoffStrategy,
        ILogger logger)
        : base(
            componentId,
            $"Retry component with {maxAttempts} attempts and {backoffStrategy} backoff",
            entryState,
            logger)
    {
        _attemptingState = attemptingState;
        _successState = successState;
        _failedState = failedState;
        _retryingState = retryingState;
        _attempt = attempt;
        _succeed = succeed;
        _fail = fail;
        _retry = retry;
        _exhausted = exhausted;
        _maxAttempts = maxAttempts;
        _retryDelay = retryDelay;
        _backoffStrategy = backoffStrategy;
        _currentAttempt = 0;

        // Register exit states
        AddExitStates(_successState, _failedState);

        // Register mappable triggers
        RegisterDefaultTrigger(_attempt);
        RegisterMappableTrigger("start", _attempt);
        RegisterMappableTrigger("retry", _retry);
    }

    /// <inheritdoc />
    public override void Configure(StateMachine<TState, TTrigger> stateMachine)
    {
        // Configure entry to attempting state
        ConfigureTransition(stateMachine, EntryState, _attempt, _attemptingState);

        // Configure attempting state
        stateMachine.Configure(_attemptingState)
            .OnEntry(() =>
            {
                _currentAttempt++;
                _logger.LogInformation("Attempt {CurrentAttempt}/{MaxAttempts} in component {ComponentId}",
                    _currentAttempt, _maxAttempts, ComponentId);
            })
            .Permit(_succeed, _successState)
            .PermitIf(_fail, _retryingState, () => _currentAttempt < _maxAttempts)
            .PermitIf(_fail, _failedState, () => _currentAttempt >= _maxAttempts);

        // Configure retrying state
        stateMachine.Configure(_retryingState)
            .OnEntry(async () =>
            {
                var delay = CalculateRetryDelay();
                _logger.LogInformation("Retrying in {Delay}ms (attempt {CurrentAttempt}/{MaxAttempts})",
                    delay.TotalMilliseconds, _currentAttempt, _maxAttempts);
                
                await Task.Delay(delay);
                stateMachine.Fire(_retry);
            })
            .Permit(_retry, _attemptingState);

        // Configure success state
        ConfigureState(stateMachine, _successState,
            onEntry: () =>
            {
                _logger.LogInformation("Operation succeeded after {Attempts} attempt(s)", _currentAttempt);
                _currentAttempt = 0; // Reset for potential reuse
            });

        // Configure failed state
        ConfigureState(stateMachine, _failedState,
            onEntry: () =>
            {
                _logger.LogError("Operation failed after {MaxAttempts} attempts", _maxAttempts);
                _currentAttempt = 0; // Reset for potential reuse
            });
    }

    /// <summary>
    /// Calculates the retry delay based on the backoff strategy.
    /// </summary>
    private TimeSpan CalculateRetryDelay()
    {
        return _backoffStrategy switch
        {
            BackoffStrategy.Fixed => _retryDelay,
            BackoffStrategy.Linear => TimeSpan.FromMilliseconds(_retryDelay.TotalMilliseconds * _currentAttempt),
            BackoffStrategy.Exponential => TimeSpan.FromMilliseconds(_retryDelay.TotalMilliseconds * Math.Pow(2, _currentAttempt - 1)),
            BackoffStrategy.Jittered => RetryComponent<TState, TTrigger>.AddJitter(_retryDelay),
            _ => _retryDelay
        };
    }

    /// <summary>
    /// Adds jitter to the retry delay to avoid thundering herd.
    /// </summary>
    private static TimeSpan AddJitter(TimeSpan baseDelay)
    {
        var random = new Random();
        var jitter = random.Next(0, (int)(baseDelay.TotalMilliseconds * 0.3));
        return baseDelay.Add(TimeSpan.FromMilliseconds(jitter));
    }

    /// <inheritdoc />
    protected override CompositionValidationResult ValidateComponent()
    {
        var errors = new List<string>();

        if (_maxAttempts < 1)
        {
            errors.Add("Max attempts must be at least 1");
        }

        if (_retryDelay < TimeSpan.Zero)
        {
            errors.Add("Retry delay cannot be negative");
        }

        // Ensure all states are different
        var states = new[] { EntryState, _attemptingState, _successState, _failedState, _retryingState };
        if (states.Distinct().Count() != states.Length)
        {
            errors.Add("All states must be unique");
        }

        return errors.Any()
            ? new CompositionValidationResult { IsValid = false, Errors = errors }
            : CompositionValidationResult.Success();
    }

    /// <inheritdoc />
    protected override async Task OnEntryAsync(CompositionContext context)
    {
        context.SharedData[$"{ComponentId}_MaxAttempts"] = _maxAttempts;
        context.SharedData[$"{ComponentId}_BackoffStrategy"] = _backoffStrategy.ToString();
        _currentAttempt = 0; // Reset attempt counter
        await base.OnEntryAsync(context);
    }

    /// <inheritdoc />
    protected override async Task OnExitAsync(CompositionContext context)
    {
        context.SharedData[$"{ComponentId}_TotalAttempts"] = _currentAttempt;
        // Success would be determined by the final state reached
        context.SharedData[$"{ComponentId}_Success"] = false;
        await base.OnExitAsync(context);
    }

    /// <summary>
    /// Resets the retry counter.
    /// </summary>
    public void Reset()
    {
        _currentAttempt = 0;
        _logger.LogInformation("Retry component {ComponentId} reset", ComponentId);
    }

    /// <summary>
    /// Gets the current attempt number.
    /// </summary>
    public int CurrentAttempt => _currentAttempt;

    /// <summary>
    /// Gets the remaining attempts.
    /// </summary>
    public int RemainingAttempts => Math.Max(0, _maxAttempts - _currentAttempt);
}

/// <summary>
/// Backoff strategies for retry delays.
/// </summary>
public enum BackoffStrategy
{
    /// <summary>
    /// Fixed delay between retries.
    /// </summary>
    Fixed,

    /// <summary>
    /// Linear increase in delay (delay * attempt number).
    /// </summary>
    Linear,

    /// <summary>
    /// Exponential increase in delay (delay * 2^attempt).
    /// </summary>
    Exponential,

    /// <summary>
    /// Fixed delay with random jitter to avoid thundering herd.
    /// </summary>
    Jittered
}