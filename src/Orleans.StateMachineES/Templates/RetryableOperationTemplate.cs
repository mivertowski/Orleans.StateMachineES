using Stateless;

namespace Orleans.StateMachineES.Templates;

/// <summary>
/// Configuration options for a retryable operation template.
/// </summary>
public class RetryableOperationOptions
{
    /// <summary>
    /// Maximum number of retry attempts.
    /// Default: 3
    /// </summary>
    public int MaxRetries { get; set; } = 3;

    /// <summary>
    /// Whether to allow manual retry after max retries exceeded.
    /// Default: false
    /// </summary>
    public bool AllowManualRetryAfterMax { get; set; } = false;

    /// <summary>
    /// Whether to allow skipping the operation entirely.
    /// Default: false
    /// </summary>
    public bool AllowSkip { get; set; } = false;

    /// <summary>
    /// Callback invoked when starting the operation.
    /// </summary>
    public Action? OnStarted { get; set; }

    /// <summary>
    /// Callback invoked on successful completion.
    /// </summary>
    public Action? OnSuccess { get; set; }

    /// <summary>
    /// Callback invoked when a retry is attempted.
    /// Parameters: retry attempt number.
    /// </summary>
    public Action<int>? OnRetry { get; set; }

    /// <summary>
    /// Callback invoked when all retries are exhausted.
    /// </summary>
    public Action? OnMaxRetriesExceeded { get; set; }

    /// <summary>
    /// Callback invoked when operation fails permanently.
    /// </summary>
    public Action? OnFailed { get; set; }
}

/// <summary>
/// Pre-built template for operations that can be retried on failure.
/// Includes retry counting, max attempts, and failure handling.
/// </summary>
/// <typeparam name="TState">The type representing the states.</typeparam>
/// <typeparam name="TTrigger">The type representing the triggers.</typeparam>
public class RetryableOperationTemplate<TState, TTrigger> : StateMachineTemplateBase<TState, TTrigger>
    where TState : notnull
    where TTrigger : notnull
{
    private readonly TState _pendingState;
    private readonly TState _executingState;
    private readonly TState _successState;
    private readonly TState _retryingState;
    private readonly TState _failedState;
    private readonly TState? _skippedState;

    private readonly TTrigger _startTrigger;
    private readonly TTrigger _successTrigger;
    private readonly TTrigger _failureTrigger;
    private readonly TTrigger _retryTrigger;
    private readonly TTrigger? _skipTrigger;

    private readonly RetryableOperationOptions _options;

    private int _retryCount;

    /// <inheritdoc/>
    public override string TemplateName => "RetryableOperation";

    /// <inheritdoc/>
    public override string Description =>
        "A template for operations that can be retried on failure with configurable retry limits.";

    /// <inheritdoc/>
    public override TState InitialState => _pendingState;

    /// <summary>
    /// Gets the current retry count.
    /// </summary>
    public int RetryCount => _retryCount;

    /// <summary>
    /// Creates a new retryable operation template.
    /// </summary>
    public RetryableOperationTemplate(
        TState pendingState,
        TState executingState,
        TState successState,
        TState retryingState,
        TState failedState,
        TTrigger startTrigger,
        TTrigger successTrigger,
        TTrigger failureTrigger,
        TTrigger retryTrigger,
        TState? skippedState = default,
        TTrigger? skipTrigger = default,
        RetryableOperationOptions? options = null)
    {
        _pendingState = pendingState;
        _executingState = executingState;
        _successState = successState;
        _retryingState = retryingState;
        _failedState = failedState;
        _skippedState = skippedState;

        _startTrigger = startTrigger;
        _successTrigger = successTrigger;
        _failureTrigger = failureTrigger;
        _retryTrigger = retryTrigger;
        _skipTrigger = skipTrigger;

        _options = options ?? new RetryableOperationOptions();

        // Register states
        RegisterState(_pendingState);
        RegisterState(_executingState);
        RegisterState(_successState);
        RegisterState(_retryingState);
        RegisterState(_failedState);

        if (_skippedState != null && !EqualityComparer<TState>.Default.Equals(_skippedState, default!))
        {
            RegisterState(_skippedState);
        }

        // Register triggers
        RegisterTrigger(_startTrigger);
        RegisterTrigger(_successTrigger);
        RegisterTrigger(_failureTrigger);
        RegisterTrigger(_retryTrigger);

        if (_skipTrigger != null && !EqualityComparer<TTrigger>.Default.Equals(_skipTrigger, default!))
        {
            RegisterTrigger(_skipTrigger);
        }

        // Set metadata
        AddMetadata("MaxRetries", _options.MaxRetries);
        AddMetadata("AllowManualRetryAfterMax", _options.AllowManualRetryAfterMax);
        AddMetadata("AllowSkip", _options.AllowSkip);
    }

    /// <inheritdoc/>
    public override void Configure(StateMachine<TState, TTrigger> stateMachine)
    {
        // Configure Pending state
        var pendingConfig = stateMachine.Configure(_pendingState)
            .Permit(_startTrigger, _executingState);

        if (_options.AllowSkip &&
            _skipTrigger != null && !EqualityComparer<TTrigger>.Default.Equals(_skipTrigger, default!) &&
            _skippedState != null && !EqualityComparer<TState>.Default.Equals(_skippedState, default!))
        {
            pendingConfig.Permit(_skipTrigger, _skippedState);
        }

        // Configure Executing state
        var executingConfig = stateMachine.Configure(_executingState)
            .Permit(_successTrigger, _successState)
            .OnEntry(() =>
            {
                _options.OnStarted?.Invoke();
            });

        // Conditional transition based on retry count
        executingConfig.PermitIf(_failureTrigger, _retryingState, () => _retryCount < _options.MaxRetries);
        executingConfig.PermitIf(_failureTrigger, _failedState, () => _retryCount >= _options.MaxRetries);

        // Configure Retrying state
        var retryingConfig = stateMachine.Configure(_retryingState)
            .Permit(_retryTrigger, _executingState)
            .OnEntry(() =>
            {
                _retryCount++;
                _options.OnRetry?.Invoke(_retryCount);
            });

        // Configure Success state
        var successConfig = stateMachine.Configure(_successState)
            .OnEntry(() =>
            {
                _options.OnSuccess?.Invoke();
            });

        // Configure Failed state
        var failedConfig = stateMachine.Configure(_failedState)
            .OnEntry(() =>
            {
                _options.OnMaxRetriesExceeded?.Invoke();
                _options.OnFailed?.Invoke();
            });

        // Allow manual retry from failed if configured
        if (_options.AllowManualRetryAfterMax)
        {
            failedConfig.Permit(_retryTrigger, _executingState);
        }

        // Configure Skipped state if exists
        if (_skippedState != null && !EqualityComparer<TState>.Default.Equals(_skippedState, default!))
        {
            stateMachine.Configure(_skippedState);
        }
    }

    /// <summary>
    /// Resets the retry counter.
    /// </summary>
    public void ResetRetryCount()
    {
        _retryCount = 0;
    }
}
