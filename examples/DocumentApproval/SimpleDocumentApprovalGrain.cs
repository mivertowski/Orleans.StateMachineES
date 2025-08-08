using Orleans.StateMachineES.Timers;
using Orleans.StateMachineES.Tracing;
using Stateless;

namespace Orleans.StateMachineES.Examples.DocumentApproval;

/// <summary>
/// Simplified document approval workflow demonstrating hierarchical state machines.
/// </summary>
public class SimpleDocumentApprovalGrain : TimerEnabledStateMachineGrain<DocumentState, DocumentTrigger>, IDocumentApprovalGrain
{
    public enum DocumentState
    {
        Draft,
        SubmittedForReview,
        TechnicalReview,
        LegalReview,
        ManagerialApproval,
        Approved,
        Rejected,
        Published,
        Archived
    }

    public enum DocumentTrigger
    {
        Submit,
        StartTechnicalReview,
        TechnicalApproval,
        TechnicalRejection,
        StartLegalReview,
        LegalApproval,
        LegalRejection,
        StartManagerialReview,
        ManagerialApproval,
        ManagerialRejection,
        Publish,
        Archive,
        ReviewTimeout
    }

    private readonly ILogger<SimpleDocumentApprovalGrain> _logger;

    public SimpleDocumentApprovalGrain(ILogger<SimpleDocumentApprovalGrain> logger)
    {
        _logger = logger;
    }

    protected override StateMachine<DocumentState, DocumentTrigger> BuildStateMachine()
    {
        var config = new StateMachine<DocumentState, DocumentTrigger>(DocumentState.Draft);

        config.Configure(DocumentState.Draft)
            .Permit(DocumentTrigger.Submit, DocumentState.SubmittedForReview)
            .OnEntry(() => _logger.LogInformation("Document draft created"));

        config.Configure(DocumentState.SubmittedForReview)
            .Permit(DocumentTrigger.StartTechnicalReview, DocumentState.TechnicalReview)
            .OnEntry(async () =>
            {
                _logger.LogInformation("Document submitted for review");
                await FireAsync(DocumentTrigger.StartTechnicalReview);
            });

        config.Configure(DocumentState.TechnicalReview)
            .Permit(DocumentTrigger.TechnicalApproval, DocumentState.LegalReview)
            .Permit(DocumentTrigger.TechnicalRejection, DocumentState.Rejected)
            .Permit(DocumentTrigger.ReviewTimeout, DocumentState.Rejected)
            .OnEntry(async () =>
            {
                _logger.LogInformation("Technical review started");
                await SetTimerAsync("ReviewTimeout", TimeSpan.FromDays(7), DocumentTrigger.ReviewTimeout);
            })
            .OnExit(() => ClearTimer("ReviewTimeout"));

        config.Configure(DocumentState.LegalReview)
            .Permit(DocumentTrigger.LegalApproval, DocumentState.ManagerialApproval)
            .Permit(DocumentTrigger.LegalRejection, DocumentState.Rejected)
            .OnEntry(() => _logger.LogInformation("Legal review started"));

        config.Configure(DocumentState.ManagerialApproval)
            .Permit(DocumentTrigger.ManagerialApproval, DocumentState.Approved)
            .Permit(DocumentTrigger.ManagerialRejection, DocumentState.Rejected)
            .OnEntry(() => _logger.LogInformation("Managerial approval started"));

        config.Configure(DocumentState.Approved)
            .Permit(DocumentTrigger.Publish, DocumentState.Published)
            .OnEntry(() => _logger.LogInformation("Document approved"));

        config.Configure(DocumentState.Rejected)
            .OnEntry(() => _logger.LogInformation("Document rejected"));

        config.Configure(DocumentState.Published)
            .Permit(DocumentTrigger.Archive, DocumentState.Archived)
            .OnEntry(() => _logger.LogInformation("Document published"));

        config.Configure(DocumentState.Archived)
            .OnEntry(() => _logger.LogInformation("Document archived"));

        ConfigureTimers();
        return config;
    }

    private void ConfigureTimers()
    {
        ConfigureTimer("ReviewTimeout", TimeSpan.FromDays(7), DocumentTrigger.ReviewTimeout);
    }

    // Public interface methods

    public async Task<string> SubmitDocumentAsync(DocumentSubmissionRequest request)
    {
        return await TracingHelper.TraceStateTransition(
            nameof(SimpleDocumentApprovalGrain),
            this.GetPrimaryKeyString(),
            State.ToString(),
            DocumentTrigger.Submit.ToString(),
            async () =>
            {
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
            "Managerial" => DocumentTrigger.ManagerialApproval,
            _ => throw new ArgumentException($"Invalid approval stage: {stage}")
        };

        return await TracingHelper.TraceStateTransition(
            nameof(SimpleDocumentApprovalGrain),
            this.GetPrimaryKeyString(),
            State.ToString(),
            trigger.ToString(),
            async () =>
            {
                _logger.LogInformation("Document approved at {Stage} stage by {ReviewerId}: {Comments}", 
                    stage, reviewerId, comments);
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
            "Managerial" => DocumentTrigger.ManagerialRejection,
            _ => throw new ArgumentException($"Invalid rejection stage: {stage}")
        };

        return await TracingHelper.TraceStateTransition(
            nameof(SimpleDocumentApprovalGrain),
            this.GetPrimaryKeyString(),
            State.ToString(),
            trigger.ToString(),
            async () =>
            {
                _logger.LogInformation("Document rejected at {Stage} stage by {ReviewerId}: {Reason}", 
                    stage, reviewerId, reason);
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
        return new DocumentStatusInfo
        {
            DocumentId = this.GetPrimaryKeyString(),
            CurrentState = State,
            ValidActions = await GetValidActionsAsync(),
            ReviewHistory = new List<ReviewEvent>
            {
                new ReviewEvent
                {
                    EventType = "DocumentCreated",
                    Timestamp = DateTime.UtcNow.AddDays(-1),
                    Data = null
                }
            },
            LastUpdated = DateTime.UtcNow
        };
    }
}

// Supporting data models
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
    public SimpleDocumentApprovalGrain.DocumentState CurrentState { get; set; }
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