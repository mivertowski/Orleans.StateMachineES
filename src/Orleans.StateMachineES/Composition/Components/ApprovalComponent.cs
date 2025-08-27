using Microsoft.Extensions.Logging;
using Stateless;

namespace Orleans.StateMachineES.Composition.Components;

/// <summary>
/// Reusable approval component that can be composed into state machines
/// to add approval workflow states and transitions.
/// </summary>
/// <typeparam name="TState">The type of states in the state machine.</typeparam>
/// <typeparam name="TTrigger">The type of triggers that cause state transitions.</typeparam>
public class ApprovalComponent<TState, TTrigger> : ComposableStateMachineBase<TState, TTrigger>
    where TState : Enum
    where TTrigger : Enum
{
    private readonly TState _pendingApprovalState;
    private readonly TState _approvedState;
    private readonly TState _rejectedState;
    private readonly TState _escalatedState;
    private readonly TTrigger _submitForApproval;
    private readonly TTrigger _approve;
    private readonly TTrigger _reject;
    private readonly TTrigger _escalate;
    private readonly TTrigger _timeout;
    private readonly ApprovalConfiguration _configuration;
    private DateTime? _submittedAt;
    private string? _approverId;
    private string? _comments;

    /// <summary>
    /// Initializes a new instance of the approval component.
    /// </summary>
    public ApprovalComponent(
        string componentId,
        TState entryState,
        TState pendingApprovalState,
        TState approvedState,
        TState rejectedState,
        TState escalatedState,
        TTrigger submitForApproval,
        TTrigger approve,
        TTrigger reject,
        TTrigger escalate,
        TTrigger timeout,
        ApprovalConfiguration configuration,
        ILogger logger)
        : base(
            componentId,
            $"Approval component with {configuration.ApprovalLevels} levels and {configuration.Timeout} timeout",
            entryState,
            logger)
    {
        _pendingApprovalState = pendingApprovalState;
        _approvedState = approvedState;
        _rejectedState = rejectedState;
        _escalatedState = escalatedState;
        _submitForApproval = submitForApproval;
        _approve = approve;
        _reject = reject;
        _escalate = escalate;
        _timeout = timeout;
        _configuration = configuration;

        // Register exit states
        AddExitStates(_approvedState, _rejectedState);
        
        if (configuration.AllowEscalation)
        {
            AddExitState(_escalatedState);
        }

        // Register mappable triggers
        RegisterDefaultTrigger(_submitForApproval);
        RegisterMappableTrigger("submit", _submitForApproval);
        RegisterMappableTrigger("approve", _approve);
        RegisterMappableTrigger("reject", _reject);
        RegisterMappableTrigger("escalate", _escalate);
    }

    /// <inheritdoc />
    public override void Configure(StateMachine<TState, TTrigger> stateMachine)
    {
        // Configure entry to pending approval
        ConfigureTransition(stateMachine, EntryState, _submitForApproval, _pendingApprovalState);

        // Configure pending approval state
        var pendingConfig = stateMachine.Configure(_pendingApprovalState)
            .OnEntry(() =>
            {
                _submittedAt = DateTime.UtcNow;
                _logger.LogInformation("Approval request submitted in component {ComponentId} at {Time}",
                    ComponentId, _submittedAt);
                
                if (_configuration.AutoApproveIfNoResponse && _configuration.Timeout.HasValue)
                {
                    // Schedule timeout trigger
                    Task.Delay(_configuration.Timeout.Value).ContinueWith(_ =>
                    {
                        if (stateMachine.IsInState(_pendingApprovalState))
                        {
                            _logger.LogWarning("Approval timeout after {Timeout}, auto-approving",
                                _configuration.Timeout);
                            stateMachine.Fire(_timeout);
                        }
                    });
                }
            })
            .Permit(_approve, _approvedState)
            .Permit(_reject, _rejectedState);

        // Configure escalation if enabled
        if (_configuration.AllowEscalation)
        {
            pendingConfig.Permit(_escalate, _escalatedState);
            
            if (_configuration.Timeout.HasValue && !_configuration.AutoApproveIfNoResponse)
            {
                pendingConfig.Permit(_timeout, _escalatedState);
            }
        }

        // Configure auto-approval on timeout if enabled
        if (_configuration.AutoApproveIfNoResponse && _configuration.Timeout.HasValue)
        {
            pendingConfig.Permit(_timeout, _approvedState);
        }

        // Configure approved state
        ConfigureState(stateMachine, _approvedState,
            onEntry: () =>
            {
                var duration = _submittedAt.HasValue 
                    ? DateTime.UtcNow - _submittedAt.Value 
                    : TimeSpan.Zero;
                    
                _logger.LogInformation("Approval granted in component {ComponentId} by {ApproverId} after {Duration}",
                    ComponentId, _approverId ?? "System", duration);
            });

        // Configure rejected state
        ConfigureState(stateMachine, _rejectedState,
            onEntry: () =>
            {
                _logger.LogInformation("Approval rejected in component {ComponentId} by {ApproverId}: {Comments}",
                    ComponentId, _approverId ?? "Unknown", _comments ?? "No comments");
            });

        // Configure escalated state if enabled
        if (_configuration.AllowEscalation)
        {
            ConfigureState(stateMachine, _escalatedState,
                onEntry: () =>
                {
                    _logger.LogWarning("Approval escalated in component {ComponentId} after {Duration}",
                        ComponentId, DateTime.UtcNow - (_submittedAt ?? DateTime.UtcNow));
                });
        }
    }

    /// <summary>
    /// Sets the approver information.
    /// </summary>
    /// <param name="approverId">The ID of the approver.</param>
    /// <param name="comments">Optional comments.</param>
    public void SetApproverInfo(string approverId, string? comments = null)
    {
        _approverId = approverId;
        _comments = comments;
    }

    /// <summary>
    /// Checks if the approval can proceed based on configuration.
    /// </summary>
    /// <param name="approverId">The ID of the potential approver.</param>
    /// <returns>True if the approver is authorized, false otherwise.</returns>
    public bool CanApprove(string approverId)
    {
        if (_configuration.RequiredApprovers?.Any() == true)
        {
            return _configuration.RequiredApprovers.Contains(approverId);
        }
        
        if (_configuration.MinimumApprovalLevel.HasValue)
        {
            // This would check against an external authorization service
            // For now, we'll return true
            return true;
        }

        return true; // No restrictions
    }

    /// <inheritdoc />
    protected override CompositionValidationResult ValidateComponent()
    {
        var errors = new List<string>();

        // Ensure all required states are different
        var requiredStates = new List<TState> 
        { 
            EntryState, 
            _pendingApprovalState, 
            _approvedState, 
            _rejectedState 
        };

        if (_configuration.AllowEscalation)
        {
            requiredStates.Add(_escalatedState);
        }

        if (requiredStates.Distinct().Count() != requiredStates.Count)
        {
            errors.Add("All states must be unique");
        }

        // Validate configuration
        if (_configuration.ApprovalLevels < 1)
        {
            errors.Add("Approval levels must be at least 1");
        }

        if (_configuration.Timeout.HasValue && _configuration.Timeout.Value <= TimeSpan.Zero)
        {
            errors.Add("Timeout must be positive");
        }

        return errors.Any()
            ? new CompositionValidationResult { IsValid = false, Errors = errors }
            : CompositionValidationResult.Success();
    }

    /// <inheritdoc />
    protected override async Task OnEntryAsync(CompositionContext context)
    {
        context.SharedData[$"{ComponentId}_Configuration"] = _configuration;
        context.SharedData[$"{ComponentId}_StartTime"] = DateTime.UtcNow;
        await base.OnEntryAsync(context);
    }

    /// <inheritdoc />
    protected override async Task OnExitAsync(CompositionContext context)
    {
        context.SharedData[$"{ComponentId}_ApproverId"] = _approverId ?? string.Empty;
        context.SharedData[$"{ComponentId}_Comments"] = _comments ?? string.Empty;
        context.SharedData[$"{ComponentId}_Duration"] = 
            DateTime.UtcNow - (_submittedAt ?? DateTime.UtcNow);

        // Determine outcome based on exit state
        string outcome = "Unknown";
        // This would need to be determined from the state machine context
        // For now, we'll store it as Unknown

        context.SharedData[$"{ComponentId}_Outcome"] = outcome;

        await base.OnExitAsync(context);
    }
}

/// <summary>
/// Configuration for the approval component.
/// </summary>
public class ApprovalConfiguration
{
    /// <summary>
    /// Number of approval levels required.
    /// </summary>
    public int ApprovalLevels { get; set; } = 1;

    /// <summary>
    /// Timeout for approval decision.
    /// </summary>
    public TimeSpan? Timeout { get; set; }

    /// <summary>
    /// Whether to auto-approve if no response within timeout.
    /// </summary>
    public bool AutoApproveIfNoResponse { get; set; }

    /// <summary>
    /// Whether to allow escalation to higher level.
    /// </summary>
    public bool AllowEscalation { get; set; }

    /// <summary>
    /// List of required approver IDs.
    /// </summary>
    public List<string>? RequiredApprovers { get; set; }

    /// <summary>
    /// Minimum approval level required.
    /// </summary>
    public int? MinimumApprovalLevel { get; set; }

    /// <summary>
    /// Whether to require comments for rejection.
    /// </summary>
    public bool RequireRejectionComments { get; set; }

    /// <summary>
    /// Whether to notify on submission.
    /// </summary>
    public bool NotifyOnSubmission { get; set; }

    /// <summary>
    /// Whether to notify on decision.
    /// </summary>
    public bool NotifyOnDecision { get; set; }
}