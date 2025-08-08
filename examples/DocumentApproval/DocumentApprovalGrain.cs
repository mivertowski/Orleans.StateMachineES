using Orleans.StateMachineES.Hierarchical;
using Orleans.StateMachineES.EventSourcing;
using Orleans.StateMachineES.Timers;
using Orleans.StateMachineES.Tracing;
using Orleans.StateMachineES.Sagas;
using Orleans.StateMachineES.Sagas.Advanced;
using Stateless;

namespace Orleans.StateMachineES.Examples.DocumentApproval;

/// <summary>
/// Document approval workflow demonstrating hierarchical state machines, saga orchestration,
/// and complex approval routing with parallel and conditional approval paths.
/// </summary>
public class DocumentApprovalGrain : HierarchicalStateMachineGrain<DocumentState, DocumentTrigger>, IDocumentApprovalGrain
{
    public enum DocumentState
    {
        // Main workflow states
        Draft,
        SubmittedForReview,
        InReview,
        Approved,
        Rejected,
        Published,
        Archived,

        // Sub-states for InReview (hierarchical)
        InReview_PendingInitialReview,
        InReview_TechnicalReview,
        InReview_LegalReview,
        InReview_ComplianceReview,
        InReview_ManagerialApproval,
        InReview_ExecutiveApproval,
        InReview_ParallelReviews,
        InReview_ConditionalApproval,

        // Error states
        ReviewTimeout,
        ReviewerUnavailable,
        ComplianceRejection
    }

    public enum DocumentTrigger
    {
        Submit,
        StartReview,
        TechnicalApproval,
        TechnicalRejection,
        LegalApproval,
        LegalRejection,
        ComplianceApproval,
        ComplianceRejection,
        ManagerialApproval,
        ManagerialRejection,
        ExecutiveApproval,
        ExecutiveRejection,
        FinalApproval,
        Reject,
        Publish,
        Archive,
        RequestChanges,
        SubmitRevision,

        // Timer triggers
        ReviewTimeout,
        ApprovalTimeout,

        // Saga coordination triggers
        ParallelReviewsCompleted,
        ConditionalCheckPassed,
        ConditionalCheckFailed
    }

    private readonly ILogger<DocumentApprovalGrain> _logger;
    private readonly IReviewerService _reviewerService;
    private readonly INotificationService _notificationService;
    private readonly ISagaCoordinatorGrain _sagaCoordinator;

    public DocumentApprovalGrain(
        ILogger<DocumentApprovalGrain> logger,
        IReviewerService reviewerService,
        INotificationService notificationService,
        ISagaCoordinatorGrain sagaCoordinator)
    {
        _logger = logger;
        _reviewerService = reviewerService;
        _notificationService = notificationService;
        _sagaCoordinator = sagaCoordinator;
    }

    protected override StateMachine<DocumentState, DocumentTrigger> BuildStateMachine()
    {
        var config = new StateMachine<DocumentState, DocumentTrigger>(DocumentState.Draft);

        // Configure main workflow states
        ConfigureDraftState(config);
        ConfigureSubmissionFlow(config);
        ConfigureHierarchicalReviewStates(config);
        ConfigureApprovalStates(config);
        ConfigurePublicationFlow(config);
        ConfigureTimers();

        return config;
    }

    private void ConfigureDraftState(StateMachine<DocumentState, DocumentTrigger> config)
    {
        config.Configure(DocumentState.Draft)
            .Permit(DocumentTrigger.Submit, DocumentState.SubmittedForReview)
            .OnEntry(() => RecordEvent("DocumentDraftCreated"))
            .OnExit(async () =>
            {
                await RecordEventAsync("DocumentSubmitted");
                await _notificationService.NotifySubmissionAsync(GetDocumentId());
            });
    }

    private void ConfigureSubmissionFlow(StateMachine<DocumentState, DocumentTrigger> config)
    {
        config.Configure(DocumentState.SubmittedForReview)
            .Permit(DocumentTrigger.StartReview, DocumentState.InReview)
            .OnEntry(async () =>
            {
                await RecordEventAsync("DocumentSubmittedForReview");
                await InitiateSagaOrchestrationAsync();
                // Automatically start review process
                await FireAsync(DocumentTrigger.StartReview);
            });
    }

    private void ConfigureHierarchicalReviewStates(StateMachine<DocumentState, DocumentTrigger> config)
    {
        // Parent review state
        config.Configure(DocumentState.InReview)
            .InitialTransition(DocumentState.InReview_PendingInitialReview)
            .Permit(DocumentTrigger.FinalApproval, DocumentState.Approved)
            .Permit(DocumentTrigger.Reject, DocumentState.Rejected)
            .Permit(DocumentTrigger.ReviewTimeout, DocumentState.ReviewTimeout)
            .OnEntry(async () =>
            {
                await RecordEventAsync("ReviewProcessStarted");
                // Set overall review timeout (30 days)
                await SetTimerAsync("ReviewTimeout", TimeSpan.FromDays(30), DocumentTrigger.ReviewTimeout);
            })
            .OnExit(() => ClearTimer("ReviewTimeout"));

        // Initial review routing
        config.Configure(DocumentState.InReview_PendingInitialReview)
            .SubstateOf(DocumentState.InReview)
            .PermitDynamic(DocumentTrigger.StartReview, DetermineReviewPath)
            .OnEntry(async () =>
            {
                await RecordEventAsync("InitialReviewStarted");
                await DetermineAndStartReviewProcessAsync();
            });

        // Technical review
        config.Configure(DocumentState.InReview_TechnicalReview)
            .SubstateOf(DocumentState.InReview)
            .Permit(DocumentTrigger.TechnicalApproval, DocumentState.InReview_LegalReview)
            .Permit(DocumentTrigger.TechnicalRejection, DocumentState.Rejected)
            .OnEntry(async () =>
            {
                await RecordEventAsync("TechnicalReviewStarted");
                await AssignTechnicalReviewerAsync();
            });

        // Legal review
        config.Configure(DocumentState.InReview_LegalReview)
            .SubstateOf(DocumentState.InReview)
            .Permit(DocumentTrigger.LegalApproval, DocumentState.InReview_ComplianceReview)
            .Permit(DocumentTrigger.LegalRejection, DocumentState.Rejected)
            .OnEntry(async () =>
            {
                await RecordEventAsync("LegalReviewStarted");
                await AssignLegalReviewerAsync();
            });

        // Compliance review
        config.Configure(DocumentState.InReview_ComplianceReview)
            .SubstateOf(DocumentState.InReview)
            .Permit(DocumentTrigger.ComplianceApproval, DocumentState.InReview_ManagerialApproval)
            .Permit(DocumentTrigger.ComplianceRejection, DocumentState.ComplianceRejection)
            .OnEntry(async () =>
            {
                await RecordEventAsync("ComplianceReviewStarted");
                await AssignComplianceReviewerAsync();
            });

        // Managerial approval
        config.Configure(DocumentState.InReview_ManagerialApproval)
            .SubstateOf(DocumentState.InReview)
            .PermitIf(DocumentTrigger.ManagerialApproval, DocumentState.Approved, () => !RequiresExecutiveApproval())
            .PermitIf(DocumentTrigger.ManagerialApproval, DocumentState.InReview_ExecutiveApproval, RequiresExecutiveApproval)
            .Permit(DocumentTrigger.ManagerialRejection, DocumentState.Rejected)
            .OnEntry(async () =>
            {
                await RecordEventAsync("ManagerialReviewStarted");
                await AssignManagerAsync();
            });

        // Executive approval (for high-value documents)
        config.Configure(DocumentState.InReview_ExecutiveApproval)
            .SubstateOf(DocumentState.InReview)
            .Permit(DocumentTrigger.ExecutiveApproval, DocumentState.Approved)
            .Permit(DocumentTrigger.ExecutiveRejection, DocumentState.Rejected)
            .OnEntry(async () =>
            {
                await RecordEventAsync("ExecutiveReviewStarted");
                await AssignExecutiveAsync();
            });

        // Parallel reviews (for complex documents)
        config.Configure(DocumentState.InReview_ParallelReviews)
            .SubstateOf(DocumentState.InReview)
            .Permit(DocumentTrigger.ParallelReviewsCompleted, DocumentState.InReview_ConditionalApproval)
            .OnEntry(async () =>
            {
                await RecordEventAsync("ParallelReviewsStarted");
                await StartParallelReviewSagaAsync();
            });

        // Conditional approval logic
        config.Configure(DocumentState.InReview_ConditionalApproval)
            .SubstateOf(DocumentState.InReview)
            .Permit(DocumentTrigger.ConditionalCheckPassed, DocumentState.Approved)
            .Permit(DocumentTrigger.ConditionalCheckFailed, DocumentState.Rejected)
            .OnEntry(async () =>
            {
                await RecordEventAsync("ConditionalApprovalEvaluation");
                await EvaluateConditionalApprovalAsync();
            });
    }

    private void ConfigureApprovalStates(StateMachine<DocumentState, DocumentTrigger> config)
    {
        config.Configure(DocumentState.Approved)
            .Permit(DocumentTrigger.Publish, DocumentState.Published)
            .OnEntry(async () =>
            {
                await RecordEventAsync("DocumentApproved");
                await _notificationService.NotifyApprovalAsync(GetDocumentId());
                // Auto-publish if configured
                if (await ShouldAutoPublishAsync())
                {
                    await FireAsync(DocumentTrigger.Publish);
                }
            });

        config.Configure(DocumentState.Rejected)
            .Permit(DocumentTrigger.SubmitRevision, DocumentState.Draft)
            .OnEntry(async () =>
            {
                await RecordEventAsync("DocumentRejected");
                await _notificationService.NotifyRejectionAsync(GetDocumentId());
            });
    }

    private void ConfigurePublicationFlow(StateMachine<DocumentState, DocumentTrigger> config)
    {
        config.Configure(DocumentState.Published)
            .Permit(DocumentTrigger.Archive, DocumentState.Archived)
            .OnEntry(async () =>
            {
                await RecordEventAsync("DocumentPublished");
                await _notificationService.NotifyPublicationAsync(GetDocumentId());
            });

        config.Configure(DocumentState.Archived)
            .OnEntry(() => RecordEvent("DocumentArchived"));
    }

    private void ConfigureTimers()
    {
        ConfigureTimer("ReviewTimeout", TimeSpan.FromDays(30), DocumentTrigger.ReviewTimeout);
        ConfigureTimer("ApprovalTimeout", TimeSpan.FromDays(7), DocumentTrigger.ApprovalTimeout);
    }

    // Dynamic routing logic

    private DocumentState DetermineReviewPath()
    {
        var documentInfo = GetDocumentInfo();
        
        // Route based on document type, value, sensitivity, etc.
        return documentInfo.RequiresParallelReview 
            ? DocumentState.InReview_ParallelReviews 
            : DocumentState.InReview_TechnicalReview;
    }

    private bool RequiresExecutiveApproval()
    {
        var documentInfo = GetDocumentInfo();
        return documentInfo.Value > 100000 || documentInfo.IsHighSensitivity;
    }

    // Saga orchestration methods

    private async Task InitiateSagaOrchestrationAsync()
    {
        using var activity = TracingHelper.StartChildActivity("InitiateSagaOrchestration", 
            nameof(DocumentApprovalGrain), this.GetPrimaryKeyString());

        var sagaId = $"approval-{this.GetPrimaryKeyString()}";
        var correlationId = Guid.NewGuid().ToString();

        // Create approval saga workflow
        var workflow = new SagaWorkflowBuilder()
            .WithId(sagaId)
            .WithCorrelationId(correlationId)
            .AddStep("InitialReview", async () => await StartInitialReviewAsync())
            .AddConditionalStep("TechnicalReview", 
                condition: () => RequiresTechnicalReview(),
                step: async () => await StartTechnicalReviewAsync())
            .AddConditionalStep("LegalReview", 
                condition: () => RequiresLegalReview(),
                step: async () => await StartLegalReviewAsync())
            .AddParallelSteps("ParallelReviews",
                ("ComplianceReview", async () => await StartComplianceReviewAsync()),
                ("SecurityReview", async () => await StartSecurityReviewAsync()),
                ("QualityReview", async () => await StartQualityReviewAsync()))
            .AddStep("FinalApproval", async () => await ProcessFinalApprovalAsync())
            .Build();

        await _sagaCoordinator.StartSagaAsync(workflow);
        
        activity?.SetTag("saga.id", sagaId);
        activity?.SetTag("correlation.id", correlationId);
    }

    private async Task StartParallelReviewSagaAsync()
    {
        using var activity = TracingHelper.StartChildActivity("StartParallelReviewSaga", 
            nameof(DocumentApprovalGrain), this.GetPrimaryKeyString());

        var parallelOrchestrator = new ParallelSagaOrchestrator();
        
        var parallelTasks = new List<ISagaStep>
        {
            new ReviewStep("TechnicalReview", _reviewerService),
            new ReviewStep("LegalReview", _reviewerService),
            new ReviewStep("ComplianceReview", _reviewerService)
        };

        var result = await parallelOrchestrator.ExecuteParallelStepsAsync(
            parallelTasks, 
            cancellationToken: CancellationToken.None);

        if (result.AllSucceeded)
        {
            await FireAsync(DocumentTrigger.ParallelReviewsCompleted);
        }
        else
        {
            await FireAsync(DocumentTrigger.Reject);
        }
    }

    private async Task EvaluateConditionalApprovalAsync()
    {
        using var activity = TracingHelper.StartChildActivity("EvaluateConditionalApproval", 
            nameof(DocumentApprovalGrain), this.GetPrimaryKeyString());

        var documentInfo = GetDocumentInfo();
        var approvalCriteria = await GetApprovalCriteriaAsync();

        // Evaluate complex business rules
        var passesConditions = 
            documentInfo.QualityScore >= approvalCriteria.MinimumQualityScore &&
            documentInfo.ComplianceScore >= approvalCriteria.MinimumComplianceScore &&
            documentInfo.SecurityScore >= approvalCriteria.MinimumSecurityScore;

        if (passesConditions)
        {
            await FireAsync(DocumentTrigger.ConditionalCheckPassed);
            activity?.SetTag("conditional.approval", "passed");
        }
        else
        {
            await FireAsync(DocumentTrigger.ConditionalCheckFailed);
            activity?.SetTag("conditional.approval", "failed");
        }
    }

    // Business logic methods

    private async Task DetermineAndStartReviewProcessAsync()
    {
        await FireAsync(DocumentTrigger.StartReview);
    }

    private async Task AssignTechnicalReviewerAsync()
    {
        var reviewer = await _reviewerService.AssignTechnicalReviewerAsync(GetDocumentId());
        await RecordEventAsync("TechnicalReviewerAssigned", new { ReviewerId = reviewer.Id });
    }

    private async Task AssignLegalReviewerAsync()
    {
        var reviewer = await _reviewerService.AssignLegalReviewerAsync(GetDocumentId());
        await RecordEventAsync("LegalReviewerAssigned", new { ReviewerId = reviewer.Id });
    }

    private async Task AssignComplianceReviewerAsync()
    {
        var reviewer = await _reviewerService.AssignComplianceReviewerAsync(GetDocumentId());
        await RecordEventAsync("ComplianceReviewerAssigned", new { ReviewerId = reviewer.Id });
    }

    private async Task AssignManagerAsync()
    {
        var manager = await _reviewerService.AssignManagerAsync(GetDocumentId());
        await RecordEventAsync("ManagerAssigned", new { ManagerId = manager.Id });
    }

    private async Task AssignExecutiveAsync()
    {
        var executive = await _reviewerService.AssignExecutiveAsync(GetDocumentId());
        await RecordEventAsync("ExecutiveAssigned", new { ExecutiveId = executive.Id });
    }

    // Helper methods

    private string GetDocumentId() => this.GetPrimaryKeyString();

    private DocumentInfo GetDocumentInfo()
    {
        // In a real implementation, this would retrieve document metadata
        return new DocumentInfo
        {
            DocumentId = GetDocumentId(),
            Title = "Sample Document",
            Type = "Contract",
            Value = 50000,
            IsHighSensitivity = false,
            RequiresParallelReview = false,
            QualityScore = 85,
            ComplianceScore = 90,
            SecurityScore = 88
        };
    }

    private bool RequiresTechnicalReview()
    {
        var documentInfo = GetDocumentInfo();
        return documentInfo.Type == "Technical" || documentInfo.Type == "Contract";
    }

    private bool RequiresLegalReview()
    {
        var documentInfo = GetDocumentInfo();
        return documentInfo.Type == "Contract" || documentInfo.Value > 10000;
    }

    private async Task<bool> ShouldAutoPublishAsync()
    {
        // Business logic to determine if document should auto-publish
        return await Task.FromResult(false);
    }

    private async Task<ApprovalCriteria> GetApprovalCriteriaAsync()
    {
        return await Task.FromResult(new ApprovalCriteria
        {
            MinimumQualityScore = 80,
            MinimumComplianceScore = 85,
            MinimumSecurityScore = 80
        });
    }

    // Saga step implementations
    private async Task StartInitialReviewAsync()
    {
        await RecordEventAsync("InitialReviewStarted");
    }

    private async Task StartTechnicalReviewAsync()
    {
        await AssignTechnicalReviewerAsync();
    }

    private async Task StartLegalReviewAsync()
    {
        await AssignLegalReviewerAsync();
    }

    private async Task StartComplianceReviewAsync()
    {
        await AssignComplianceReviewerAsync();
    }

    private async Task StartSecurityReviewAsync()
    {
        var reviewer = await _reviewerService.AssignSecurityReviewerAsync(GetDocumentId());
        await RecordEventAsync("SecurityReviewerAssigned", new { ReviewerId = reviewer.Id });
    }

    private async Task StartQualityReviewAsync()
    {
        var reviewer = await _reviewerService.AssignQualityReviewerAsync(GetDocumentId());
        await RecordEventAsync("QualityReviewerAssigned", new { ReviewerId = reviewer.Id });
    }

    private async Task ProcessFinalApprovalAsync()
    {
        await FireAsync(DocumentTrigger.FinalApproval);
    }

    // Public interface implementation

    public async Task<string> SubmitDocumentAsync(DocumentSubmissionRequest request)
    {
        return await TracingHelper.TraceStateTransition(
            nameof(DocumentApprovalGrain),
            this.GetPrimaryKeyString(),
            State.ToString(),
            DocumentTrigger.Submit.ToString(),
            async () =>
            {
                await RecordEventAsync("DocumentSubmissionRequested", request);
                await FireAsync(DocumentTrigger.Submit);
                return "Document submitted for approval";
            });
    }

    public async Task<string> ApproveAtStageAsync(string stage, string reviewerId, string comments)
    {
        var trigger = stage switch
        {
            "Technical" => DocumentTrigger.TechnicalApproval,
            "Legal" => DocumentTrigger.LegalApproval,
            "Compliance" => DocumentTrigger.ComplianceApproval,
            "Managerial" => DocumentTrigger.ManagerialApproval,
            "Executive" => DocumentTrigger.ExecutiveApproval,
            _ => throw new ArgumentException($"Invalid approval stage: {stage}")
        };

        return await TracingHelper.TraceStateTransition(
            nameof(DocumentApprovalGrain),
            this.GetPrimaryKeyString(),
            State.ToString(),
            trigger.ToString(),
            async () =>
            {
                await RecordEventAsync($"{stage}Approval", new { ReviewerId = reviewerId, Comments = comments });
                await FireAsync(trigger);
                return $"Document approved at {stage} stage";
            });
    }

    public async Task<string> RejectAtStageAsync(string stage, string reviewerId, string reason)
    {
        var trigger = stage switch
        {
            "Technical" => DocumentTrigger.TechnicalRejection,
            "Legal" => DocumentTrigger.LegalRejection,
            "Compliance" => DocumentTrigger.ComplianceRejection,
            "Managerial" => DocumentTrigger.ManagerialRejection,
            "Executive" => DocumentTrigger.ExecutiveRejection,
            _ => DocumentTrigger.Reject
        };

        return await TracingHelper.TraceStateTransition(
            nameof(DocumentApprovalGrain),
            this.GetPrimaryKeyString(),
            State.ToString(),
            trigger.ToString(),
            async () =>
            {
                await RecordEventAsync($"{stage}Rejection", new { ReviewerId = reviewerId, Reason = reason });
                await FireAsync(trigger);
                return $"Document rejected at {stage} stage";
            });
    }

    public Task<DocumentState> GetCurrentStateAsync() => Task.FromResult(State);

    public async Task<List<string>> GetValidActionsAsync()
    {
        var permittedTriggers = await GetPermittedTriggersAsync();
        return permittedTriggers.Select(t => t.ToString()).ToList();
    }

    public async Task<DocumentStatusInfo> GetDocumentStatusAsync()
    {
        var events = await GetEventsAsync();
        return new DocumentStatusInfo
        {
            DocumentId = this.GetPrimaryKeyString(),
            CurrentState = State,
            ValidActions = await GetValidActionsAsync(),
            ReviewHistory = events.Select(e => new ReviewEvent
            {
                EventType = e.EventType,
                Timestamp = e.Timestamp,
                Data = e.Data
            }).ToList(),
            LastUpdated = DateTime.UtcNow
        };
    }

    protected override string GetVersion() => "1.0.0";
}

// Supporting data models and interfaces
public class DocumentInfo
{
    public string DocumentId { get; set; } = string.Empty;
    public string Title { get; set; } = string.Empty;
    public string Type { get; set; } = string.Empty;
    public decimal Value { get; set; }
    public bool IsHighSensitivity { get; set; }
    public bool RequiresParallelReview { get; set; }
    public int QualityScore { get; set; }
    public int ComplianceScore { get; set; }
    public int SecurityScore { get; set; }
}

public class ApprovalCriteria
{
    public int MinimumQualityScore { get; set; }
    public int MinimumComplianceScore { get; set; }
    public int MinimumSecurityScore { get; set; }
}

public class DocumentSubmissionRequest
{
    public string Title { get; set; } = string.Empty;
    public string Type { get; set; } = string.Empty;
    public string Content { get; set; } = string.Empty;
    public decimal EstimatedValue { get; set; }
    public bool IsConfidential { get; set; }
    public string SubmitterId { get; set; } = string.Empty;
}

public class DocumentStatusInfo
{
    public string DocumentId { get; set; } = string.Empty;
    public DocumentApprovalGrain.DocumentState CurrentState { get; set; }
    public List<string> ValidActions { get; set; } = new();
    public List<ReviewEvent> ReviewHistory { get; set; } = new();
    public DateTime LastUpdated { get; set; }
}

public class ReviewEvent
{
    public string EventType { get; set; } = string.Empty;
    public DateTime Timestamp { get; set; }
    public object? Data { get; set; }
}

public class Reviewer
{
    public string Id { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string Role { get; set; } = string.Empty;
}

// Service interfaces
public interface IReviewerService
{
    Task<Reviewer> AssignTechnicalReviewerAsync(string documentId);
    Task<Reviewer> AssignLegalReviewerAsync(string documentId);
    Task<Reviewer> AssignComplianceReviewerAsync(string documentId);
    Task<Reviewer> AssignSecurityReviewerAsync(string documentId);
    Task<Reviewer> AssignQualityReviewerAsync(string documentId);
    Task<Reviewer> AssignManagerAsync(string documentId);
    Task<Reviewer> AssignExecutiveAsync(string documentId);
}

public interface INotificationService
{
    Task NotifySubmissionAsync(string documentId);
    Task NotifyApprovalAsync(string documentId);
    Task NotifyRejectionAsync(string documentId);
    Task NotifyPublicationAsync(string documentId);
}

// Saga step implementation
public class ReviewStep : ISagaStep
{
    private readonly string _reviewType;
    private readonly IReviewerService _reviewerService;

    public ReviewStep(string reviewType, IReviewerService reviewerService)
    {
        _reviewType = reviewType;
        _reviewerService = reviewerService;
    }

    public string Name => _reviewType;

    public async Task<SagaStepResult> ExecuteAsync(SagaExecutionContext context)
    {
        try
        {
            // Simulate review assignment and completion
            await Task.Delay(100); // Simulate async operation
            
            return SagaStepResult.Success(new Dictionary<string, object>
            {
                ["reviewType"] = _reviewType,
                ["completed"] = true,
                ["timestamp"] = DateTime.UtcNow
            });
        }
        catch (Exception ex)
        {
            return SagaStepResult.Failed(ex.Message);
        }
    }

    public async Task CompensateAsync(SagaExecutionContext context)
    {
        // Compensation logic for review step
        await Task.CompletedTask;
    }
}