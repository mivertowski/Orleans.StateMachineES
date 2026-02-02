using Stateless;

namespace Orleans.StateMachineES.Templates;

/// <summary>
/// Configuration options for an order processing workflow template.
/// </summary>
public class OrderProcessingOptions
{
    /// <summary>
    /// Whether payment is required before fulfillment.
    /// Default: true
    /// </summary>
    public bool RequirePayment { get; set; } = true;

    /// <summary>
    /// Whether to allow order cancellation.
    /// Default: true
    /// </summary>
    public bool AllowCancel { get; set; } = true;

    /// <summary>
    /// Whether to allow refunds after completion.
    /// Default: true
    /// </summary>
    public bool AllowRefund { get; set; } = true;

    /// <summary>
    /// States where cancellation is allowed.
    /// </summary>
    public List<string> CancellableStates { get; set; } = new() { "Created", "Confirmed", "PaymentPending" };

    /// <summary>
    /// Callback invoked when order is created.
    /// </summary>
    public Action? OnCreated { get; set; }

    /// <summary>
    /// Callback invoked when order is confirmed.
    /// </summary>
    public Action? OnConfirmed { get; set; }

    /// <summary>
    /// Callback invoked when payment is received.
    /// </summary>
    public Action? OnPaid { get; set; }

    /// <summary>
    /// Callback invoked when order is shipped.
    /// </summary>
    public Action? OnShipped { get; set; }

    /// <summary>
    /// Callback invoked when order is completed.
    /// </summary>
    public Action? OnCompleted { get; set; }

    /// <summary>
    /// Callback invoked when order is cancelled.
    /// </summary>
    public Action? OnCancelled { get; set; }

    /// <summary>
    /// Callback invoked when order is refunded.
    /// </summary>
    public Action? OnRefunded { get; set; }
}

/// <summary>
/// Pre-built template for order processing workflows with payment, shipping, and cancellation handling.
/// </summary>
/// <typeparam name="TState">The type representing the states.</typeparam>
/// <typeparam name="TTrigger">The type representing the triggers.</typeparam>
public class OrderProcessingTemplate<TState, TTrigger> : StateMachineTemplateBase<TState, TTrigger>
    where TState : notnull
    where TTrigger : notnull
{
    private readonly TState _createdState;
    private readonly TState _confirmedState;
    private readonly TState _paymentPendingState;
    private readonly TState _paidState;
    private readonly TState _shippingState;
    private readonly TState _shippedState;
    private readonly TState _completedState;
    private readonly TState? _cancelledState;
    private readonly TState? _refundedState;

    private readonly TTrigger _confirmTrigger;
    private readonly TTrigger _payTrigger;
    private readonly TTrigger _shipTrigger;
    private readonly TTrigger _deliverTrigger;
    private readonly TTrigger _completeTrigger;
    private readonly TTrigger? _cancelTrigger;
    private readonly TTrigger? _refundTrigger;

    private readonly OrderProcessingOptions _options;

    /// <inheritdoc/>
    public override string TemplateName => "OrderProcessing";

    /// <inheritdoc/>
    public override string Description =>
        "A complete order processing workflow with payment handling, shipping tracking, and cancellation/refund support.";

    /// <inheritdoc/>
    public override TState InitialState => _createdState;

    /// <summary>
    /// Creates a new order processing workflow template.
    /// </summary>
    public OrderProcessingTemplate(
        TState createdState,
        TState confirmedState,
        TState paymentPendingState,
        TState paidState,
        TState shippingState,
        TState shippedState,
        TState completedState,
        TTrigger confirmTrigger,
        TTrigger payTrigger,
        TTrigger shipTrigger,
        TTrigger deliverTrigger,
        TTrigger completeTrigger,
        TState? cancelledState = default,
        TState? refundedState = default,
        TTrigger? cancelTrigger = default,
        TTrigger? refundTrigger = default,
        OrderProcessingOptions? options = null)
    {
        _createdState = createdState;
        _confirmedState = confirmedState;
        _paymentPendingState = paymentPendingState;
        _paidState = paidState;
        _shippingState = shippingState;
        _shippedState = shippedState;
        _completedState = completedState;
        _cancelledState = cancelledState;
        _refundedState = refundedState;

        _confirmTrigger = confirmTrigger;
        _payTrigger = payTrigger;
        _shipTrigger = shipTrigger;
        _deliverTrigger = deliverTrigger;
        _completeTrigger = completeTrigger;
        _cancelTrigger = cancelTrigger;
        _refundTrigger = refundTrigger;

        _options = options ?? new OrderProcessingOptions();

        // Register states
        RegisterState(_createdState);
        RegisterState(_confirmedState);
        RegisterState(_paymentPendingState);
        RegisterState(_paidState);
        RegisterState(_shippingState);
        RegisterState(_shippedState);
        RegisterState(_completedState);

        if (_cancelledState != null && !EqualityComparer<TState>.Default.Equals(_cancelledState, default!))
        {
            RegisterState(_cancelledState);
        }

        if (_refundedState != null && !EqualityComparer<TState>.Default.Equals(_refundedState, default!))
        {
            RegisterState(_refundedState);
        }

        // Register triggers
        RegisterTrigger(_confirmTrigger);
        RegisterTrigger(_payTrigger);
        RegisterTrigger(_shipTrigger);
        RegisterTrigger(_deliverTrigger);
        RegisterTrigger(_completeTrigger);

        if (_cancelTrigger != null && !EqualityComparer<TTrigger>.Default.Equals(_cancelTrigger, default!))
        {
            RegisterTrigger(_cancelTrigger);
        }

        if (_refundTrigger != null && !EqualityComparer<TTrigger>.Default.Equals(_refundTrigger, default!))
        {
            RegisterTrigger(_refundTrigger);
        }

        // Set metadata
        AddMetadata("RequirePayment", _options.RequirePayment);
        AddMetadata("AllowCancel", _options.AllowCancel);
        AddMetadata("AllowRefund", _options.AllowRefund);
    }

    /// <inheritdoc/>
    public override void Configure(StateMachine<TState, TTrigger> stateMachine)
    {
        // Configure Created state
        var createdConfig = stateMachine.Configure(_createdState)
            .Permit(_confirmTrigger, _confirmedState);

        if (_options.OnCreated != null)
        {
            createdConfig.OnEntry(_options.OnCreated);
        }

        // Configure Confirmed state
        var confirmedConfig = stateMachine.Configure(_confirmedState);

        if (_options.RequirePayment)
        {
            confirmedConfig.Permit(_payTrigger, _paymentPendingState);
        }
        else
        {
            confirmedConfig.Permit(_shipTrigger, _shippingState);
        }

        if (_options.OnConfirmed != null)
        {
            confirmedConfig.OnEntry(_options.OnConfirmed);
        }

        // Configure PaymentPending state
        var paymentConfig = stateMachine.Configure(_paymentPendingState)
            .Permit(_payTrigger, _paidState);

        // Configure Paid state
        var paidConfig = stateMachine.Configure(_paidState)
            .Permit(_shipTrigger, _shippingState);

        if (_options.OnPaid != null)
        {
            paidConfig.OnEntry(_options.OnPaid);
        }

        // Configure Shipping state
        var shippingConfig = stateMachine.Configure(_shippingState)
            .Permit(_deliverTrigger, _shippedState);

        // Configure Shipped state
        var shippedConfig = stateMachine.Configure(_shippedState)
            .Permit(_completeTrigger, _completedState);

        if (_options.OnShipped != null)
        {
            shippedConfig.OnEntry(_options.OnShipped);
        }

        // Configure Completed state
        var completedConfig = stateMachine.Configure(_completedState);

        if (_options.OnCompleted != null)
        {
            completedConfig.OnEntry(_options.OnCompleted);
        }

        // Add cancel transitions if configured
        if (_options.AllowCancel &&
            _cancelTrigger != null && !EqualityComparer<TTrigger>.Default.Equals(_cancelTrigger, default!) &&
            _cancelledState != null && !EqualityComparer<TState>.Default.Equals(_cancelledState, default!))
        {
            createdConfig.Permit(_cancelTrigger, _cancelledState);
            confirmedConfig.Permit(_cancelTrigger, _cancelledState);
            paymentConfig.Permit(_cancelTrigger, _cancelledState);

            var cancelledConfig = stateMachine.Configure(_cancelledState);
            if (_options.OnCancelled != null)
            {
                cancelledConfig.OnEntry(_options.OnCancelled);
            }
        }

        // Add refund from completed if configured
        if (_options.AllowRefund &&
            _refundTrigger != null && !EqualityComparer<TTrigger>.Default.Equals(_refundTrigger, default!) &&
            _refundedState != null && !EqualityComparer<TState>.Default.Equals(_refundedState, default!))
        {
            completedConfig.Permit(_refundTrigger, _refundedState);

            var refundedConfig = stateMachine.Configure(_refundedState);
            if (_options.OnRefunded != null)
            {
                refundedConfig.OnEntry(_options.OnRefunded);
            }
        }
    }
}
