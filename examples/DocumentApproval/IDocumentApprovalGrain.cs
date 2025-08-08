using Orleans;
using Orleans.StateMachineES.Interfaces;
using static Orleans.StateMachineES.Examples.DocumentApproval.SimpleDocumentApprovalGrain;

namespace Orleans.StateMachineES.Examples.DocumentApproval;

/// <summary>
/// Interface for the document approval grain demonstrating hierarchical state machines
/// and complex approval workflows with saga orchestration.
/// </summary>
public interface IDocumentApprovalGrain : IStateMachineGrain<DocumentState, DocumentTrigger>
{
    /// <summary>
    /// Submits a document for approval workflow.
    /// </summary>
    /// <param name="request">Document submission details.</param>
    /// <returns>Confirmation message.</returns>
    Task<string> SubmitDocumentAsync(DocumentSubmissionRequest request);

    /// <summary>
    /// Approves a document at a specific review stage.
    /// </summary>
    /// <param name="stage">The review stage (Technical, Legal, Compliance, etc.).</param>
    /// <param name="reviewerId">The ID of the reviewer approving.</param>
    /// <param name="comments">Approval comments.</param>
    /// <returns>Confirmation message.</returns>
    Task<string> ApproveAtStageAsync(string stage, string reviewerId, string comments);

    /// <summary>
    /// Rejects a document at a specific review stage.
    /// </summary>
    /// <param name="stage">The review stage.</param>
    /// <param name="reviewerId">The ID of the reviewer rejecting.</param>
    /// <param name="reason">Reason for rejection.</param>
    /// <returns>Confirmation message.</returns>
    Task<string> RejectAtStageAsync(string stage, string reviewerId, string reason);

    /// <summary>
    /// Gets the current state of the document in the approval workflow.
    /// </summary>
    /// <returns>The current document state.</returns>
    Task<DocumentState> GetCurrentStateAsync();

    /// <summary>
    /// Gets the list of valid actions that can be performed in the current state.
    /// </summary>
    /// <returns>List of valid action names.</returns>
    Task<List<string>> GetValidActionsAsync();

    /// <summary>
    /// Gets comprehensive status information about the document approval process.
    /// </summary>
    /// <returns>Complete document status including review history and available actions.</returns>
    Task<DocumentStatusInfo> GetDocumentStatusAsync();
}