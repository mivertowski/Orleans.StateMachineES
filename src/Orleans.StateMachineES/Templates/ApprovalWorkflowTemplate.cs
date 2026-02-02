using Stateless;

namespace Orleans.StateMachineES.Templates;

/// <summary>
/// Configuration options for an approval workflow template.
/// </summary>
public class ApprovalWorkflowOptions
{
    /// <summary>
    /// Number of approval levels required.
    /// Default: 1
    /// </summary>
    public int ApprovalLevels { get; set; } = 1;

    /// <summary>
    /// Whether to allow escalation to a higher authority.
    /// Default: true
    /// </summary>
    public bool AllowEscalation { get; set; } = true;

    /// <summary>
    /// Whether to allow the requestor to cancel the request.
    /// Default: true
    /// </summary>
    public bool AllowCancel { get; set; } = true;

    /// <summary>
    /// Whether to allow resubmission after rejection.
    /// Default: true
    /// </summary>
    public bool AllowResubmit { get; set; } = true;

    /// <summary>
    /// Whether approval is automatic if no action is taken within the timeout.
    /// Default: false
    /// </summary>
    public bool AutoApproveOnTimeout { get; set; } = false;

    /// <summary>
    /// Callback invoked when entering the pending state.
    /// </summary>
    public Action? OnPending { get; set; }

    /// <summary>
    /// Callback invoked when approved.
    /// </summary>
    public Action? OnApproved { get; set; }

    /// <summary>
    /// Callback invoked when rejected.
    /// </summary>
    public Action? OnRejected { get; set; }

    /// <summary>
    /// Callback invoked when escalated.
    /// </summary>
    public Action? OnEscalated { get; set; }

    /// <summary>
    /// Callback invoked when cancelled.
    /// </summary>
    public Action? OnCancelled { get; set; }
}

/// <summary>
/// Pre-built template for approval workflows with configurable approval levels,
/// escalation, and rejection handling.
/// </summary>
/// <typeparam name="TState">The type representing the states.</typeparam>
/// <typeparam name="TTrigger">The type representing the triggers.</typeparam>
public class ApprovalWorkflowTemplate<TState, TTrigger> : StateMachineTemplateBase<TState, TTrigger>
    where TState : notnull
    where TTrigger : notnull
{
    private readonly TState _draftState;
    private readonly TState _pendingState;
    private readonly TState _approvedState;
    private readonly TState _rejectedState;
    private readonly TState? _escalatedState;
    private readonly TState? _cancelledState;

    private readonly TTrigger _submitTrigger;
    private readonly TTrigger _approveTrigger;
    private readonly TTrigger _rejectTrigger;
    private readonly TTrigger? _escalateTrigger;
    private readonly TTrigger? _cancelTrigger;
    private readonly TTrigger? _resubmitTrigger;

    private readonly ApprovalWorkflowOptions _options;

    /// <inheritdoc/>
    public override string TemplateName => "ApprovalWorkflow";

    /// <inheritdoc/>
    public override string Description =>
        "A configurable approval workflow with support for multi-level approvals, escalation, and rejection handling.";

    /// <inheritdoc/>
    public override TState InitialState => _draftState;

    /// <summary>
    /// Creates a new approval workflow template.
    /// </summary>
    /// <param name="draftState">The initial draft state.</param>
    /// <param name="pendingState">The pending approval state.</param>
    /// <param name="approvedState">The approved state.</param>
    /// <param name="rejectedState">The rejected state.</param>
    /// <param name="submitTrigger">Trigger to submit for approval.</param>
    /// <param name="approveTrigger">Trigger to approve.</param>
    /// <param name="rejectTrigger">Trigger to reject.</param>
    /// <param name="escalatedState">Optional escalated state.</param>
    /// <param name="cancelledState">Optional cancelled state.</param>
    /// <param name="escalateTrigger">Optional trigger to escalate.</param>
    /// <param name="cancelTrigger">Optional trigger to cancel.</param>
    /// <param name="resubmitTrigger">Optional trigger to resubmit.</param>
    /// <param name="options">Optional workflow options.</param>
    public ApprovalWorkflowTemplate(
        TState draftState,
        TState pendingState,
        TState approvedState,
        TState rejectedState,
        TTrigger submitTrigger,
        TTrigger approveTrigger,
        TTrigger rejectTrigger,
        TState? escalatedState = default,
        TState? cancelledState = default,
        TTrigger? escalateTrigger = default,
        TTrigger? cancelTrigger = default,
        TTrigger? resubmitTrigger = default,
        ApprovalWorkflowOptions? options = null)
    {
        _draftState = draftState;
        _pendingState = pendingState;
        _approvedState = approvedState;
        _rejectedState = rejectedState;
        _escalatedState = escalatedState;
        _cancelledState = cancelledState;

        _submitTrigger = submitTrigger;
        _approveTrigger = approveTrigger;
        _rejectTrigger = rejectTrigger;
        _escalateTrigger = escalateTrigger;
        _cancelTrigger = cancelTrigger;
        _resubmitTrigger = resubmitTrigger;

        _options = options ?? new ApprovalWorkflowOptions();

        // Register states
        RegisterState(_draftState);
        RegisterState(_pendingState);
        RegisterState(_approvedState);
        RegisterState(_rejectedState);

        if (_escalatedState != null && !EqualityComparer<TState>.Default.Equals(_escalatedState, default!))
        {
            RegisterState(_escalatedState);
        }

        if (_cancelledState != null && !EqualityComparer<TState>.Default.Equals(_cancelledState, default!))
        {
            RegisterState(_cancelledState);
        }

        // Register triggers
        RegisterTrigger(_submitTrigger);
        RegisterTrigger(_approveTrigger);
        RegisterTrigger(_rejectTrigger);

        if (_escalateTrigger != null && !EqualityComparer<TTrigger>.Default.Equals(_escalateTrigger, default!))
        {
            RegisterTrigger(_escalateTrigger);
        }

        if (_cancelTrigger != null && !EqualityComparer<TTrigger>.Default.Equals(_cancelTrigger, default!))
        {
            RegisterTrigger(_cancelTrigger);
        }

        if (_resubmitTrigger != null && !EqualityComparer<TTrigger>.Default.Equals(_resubmitTrigger, default!))
        {
            RegisterTrigger(_resubmitTrigger);
        }

        // Set metadata
        AddMetadata("ApprovalLevels", _options.ApprovalLevels);
        AddMetadata("AllowEscalation", _options.AllowEscalation);
        AddMetadata("AllowCancel", _options.AllowCancel);
        AddMetadata("AllowResubmit", _options.AllowResubmit);
    }

    /// <inheritdoc/>
    public override void Configure(StateMachine<TState, TTrigger> stateMachine)
    {
        // Configure Draft state
        var draftConfig = stateMachine.Configure(_draftState)
            .Permit(_submitTrigger, _pendingState);

        // Configure Pending state
        var pendingConfig = stateMachine.Configure(_pendingState)
            .Permit(_approveTrigger, _approvedState)
            .Permit(_rejectTrigger, _rejectedState);

        if (_options.OnPending != null)
        {
            pendingConfig.OnEntry(_options.OnPending);
        }

        // Add escalation if configured
        if (_options.AllowEscalation &&
            _escalateTrigger != null && !EqualityComparer<TTrigger>.Default.Equals(_escalateTrigger, default!) &&
            _escalatedState != null && !EqualityComparer<TState>.Default.Equals(_escalatedState, default!))
        {
            pendingConfig.Permit(_escalateTrigger, _escalatedState);
        }

        // Add cancel if configured
        if (_options.AllowCancel &&
            _cancelTrigger != null && !EqualityComparer<TTrigger>.Default.Equals(_cancelTrigger, default!) &&
            _cancelledState != null && !EqualityComparer<TState>.Default.Equals(_cancelledState, default!))
        {
            pendingConfig.Permit(_cancelTrigger, _cancelledState);
            draftConfig.Permit(_cancelTrigger, _cancelledState);
        }

        // Configure Approved state
        var approvedConfig = stateMachine.Configure(_approvedState);
        if (_options.OnApproved != null)
        {
            approvedConfig.OnEntry(_options.OnApproved);
        }

        // Configure Rejected state
        var rejectedConfig = stateMachine.Configure(_rejectedState);
        if (_options.OnRejected != null)
        {
            rejectedConfig.OnEntry(_options.OnRejected);
        }

        // Add resubmit if configured
        if (_options.AllowResubmit &&
            _resubmitTrigger != null && !EqualityComparer<TTrigger>.Default.Equals(_resubmitTrigger, default!))
        {
            rejectedConfig.Permit(_resubmitTrigger, _draftState);
        }

        // Configure Escalated state if exists
        if (_escalatedState != null && !EqualityComparer<TState>.Default.Equals(_escalatedState, default!))
        {
            var escalatedConfig = stateMachine.Configure(_escalatedState)
                .Permit(_approveTrigger, _approvedState)
                .Permit(_rejectTrigger, _rejectedState);

            if (_options.OnEscalated != null)
            {
                escalatedConfig.OnEntry(_options.OnEscalated);
            }
        }

        // Configure Cancelled state if exists
        if (_cancelledState != null && !EqualityComparer<TState>.Default.Equals(_cancelledState, default!))
        {
            var cancelledConfig = stateMachine.Configure(_cancelledState);
            if (_options.OnCancelled != null)
            {
                cancelledConfig.OnEntry(_options.OnCancelled);
            }
        }
    }
}
