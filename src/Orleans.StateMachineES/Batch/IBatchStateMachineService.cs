namespace Orleans.StateMachineES.Batch;

/// <summary>
/// Service interface for executing batch state machine operations.
/// </summary>
public interface IBatchStateMachineService
{
    /// <summary>
    /// Executes a batch of state machine transitions.
    /// </summary>
    /// <typeparam name="TState">The type representing the states.</typeparam>
    /// <typeparam name="TTrigger">The type representing the triggers.</typeparam>
    /// <typeparam name="TGrain">The grain interface type.</typeparam>
    /// <param name="requests">The batch operation requests.</param>
    /// <param name="fireAsync">Function to fire the trigger on a grain.</param>
    /// <param name="getState">Function to get the current state from a grain.</param>
    /// <param name="options">Optional batch operation options.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The batch operation result.</returns>
    Task<BatchOperationResult<TState>> ExecuteBatchAsync<TState, TTrigger, TGrain>(
        IEnumerable<BatchOperationRequest<TTrigger>> requests,
        Func<TGrain, TTrigger, object[]?, Task> fireAsync,
        Func<TGrain, Task<TState>> getState,
        BatchOperationOptions? options = null,
        CancellationToken cancellationToken = default)
        where TGrain : IGrainWithStringKey;

    /// <summary>
    /// Executes a batch of state machine transitions using a grain resolver function.
    /// </summary>
    /// <typeparam name="TState">The type representing the states.</typeparam>
    /// <typeparam name="TTrigger">The type representing the triggers.</typeparam>
    /// <param name="requests">The batch operation requests.</param>
    /// <param name="grainResolver">Function to resolve a grain by ID.</param>
    /// <param name="fireAsync">Function to fire the trigger on a grain.</param>
    /// <param name="getState">Function to get the current state from a grain.</param>
    /// <param name="options">Optional batch operation options.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The batch operation result.</returns>
    Task<BatchOperationResult<TState>> ExecuteBatchAsync<TState, TTrigger>(
        IEnumerable<BatchOperationRequest<TTrigger>> requests,
        Func<string, object> grainResolver,
        Func<object, TTrigger, object[]?, Task> fireAsync,
        Func<object, Task<TState>> getState,
        BatchOperationOptions? options = null,
        CancellationToken cancellationToken = default);
}
