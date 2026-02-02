using System.Collections.Concurrent;
using System.Diagnostics;
using Microsoft.Extensions.Logging;

namespace Orleans.StateMachineES.Batch;

/// <summary>
/// Service for executing batch state machine operations with parallel processing support.
/// </summary>
public class BatchStateMachineService : IBatchStateMachineService
{
    private readonly IGrainFactory _grainFactory;
    private readonly ILogger<BatchStateMachineService>? _logger;

    /// <summary>
    /// Initializes a new instance of the BatchStateMachineService class.
    /// </summary>
    /// <param name="grainFactory">The Orleans grain factory.</param>
    /// <param name="logger">Optional logger.</param>
    public BatchStateMachineService(IGrainFactory grainFactory, ILogger<BatchStateMachineService>? logger = null)
    {
        _grainFactory = grainFactory ?? throw new ArgumentNullException(nameof(grainFactory));
        _logger = logger;
    }

    /// <inheritdoc/>
    public async Task<BatchOperationResult<TState>> ExecuteBatchAsync<TState, TTrigger, TGrain>(
        IEnumerable<BatchOperationRequest<TTrigger>> requests,
        Func<TGrain, TTrigger, object[]?, Task> fireAsync,
        Func<TGrain, Task<TState>> getState,
        BatchOperationOptions? options = null,
        CancellationToken cancellationToken = default)
        where TGrain : IGrainWithStringKey
    {
        return await ExecuteBatchAsync<TState, TTrigger>(
            requests,
            grainId => _grainFactory.GetGrain<TGrain>(grainId),
            async (grain, trigger, args) => await fireAsync((TGrain)grain, trigger, args),
            async grain => await getState((TGrain)grain),
            options,
            cancellationToken);
    }

    /// <inheritdoc/>
    public async Task<BatchOperationResult<TState>> ExecuteBatchAsync<TState, TTrigger>(
        IEnumerable<BatchOperationRequest<TTrigger>> requests,
        Func<string, object> grainResolver,
        Func<object, TTrigger, object[]?, Task> fireAsync,
        Func<object, Task<TState>> getState,
        BatchOperationOptions? options = null,
        CancellationToken cancellationToken = default)
    {
        options ??= new BatchOperationOptions();

        var requestList = requests.ToList();
        var totalCount = requestList.Count;

        if (totalCount == 0)
        {
            return new BatchOperationResult<TState>
            {
                TotalOperations = 0,
                StartTime = DateTime.UtcNow,
                EndTime = DateTime.UtcNow,
                Results = Array.Empty<BatchItemResult<TState>>()
            };
        }

        _logger?.LogInformation(
            "Starting batch operation with {Count} items, MaxParallelism: {Parallelism}",
            totalCount, options.MaxParallelism);

        // Invoke batch started callback
        try
        {
            options.OnBatchStarted?.Invoke(totalCount);
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "Exception in OnBatchStarted callback");
        }

        var startTime = DateTime.UtcNow;
        var stopwatch = Stopwatch.StartNew();

        // Order by priority if configured
        if (options.OrderByPriority)
        {
            requestList = requestList.OrderByDescending(r => r.Priority).ToList();
        }

        var results = new ConcurrentBag<BatchItemResult<TState>>();
        var completedCount = 0;
        var shouldStop = false;

        // Create cancellation token with timeout
        using var timeoutCts = new CancellationTokenSource(options.Timeout);
        using var linkedCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, timeoutCts.Token);

        try
        {
            // Use semaphore for controlled parallelism
            using var semaphore = new SemaphoreSlim(options.MaxParallelism);

            var tasks = requestList.Select(async (request, index) =>
            {
                if (shouldStop || linkedCts.Token.IsCancellationRequested)
                {
                    results.Add(BatchItemResult<TState>.Failure(
                        request.GrainId,
                        "Operation skipped due to batch cancellation",
                        null,
                        TimeSpan.Zero,
                        index,
                        request.CorrelationId));
                    return;
                }

                await semaphore.WaitAsync(linkedCts.Token);
                try
                {
                    if (shouldStop || linkedCts.Token.IsCancellationRequested)
                    {
                        results.Add(BatchItemResult<TState>.Failure(
                            request.GrainId,
                            "Operation skipped due to batch cancellation",
                            null,
                            TimeSpan.Zero,
                            index,
                            request.CorrelationId));
                        return;
                    }

                    var result = await ExecuteSingleOperationAsync(
                        request,
                        index,
                        grainResolver,
                        fireAsync,
                        getState,
                        options,
                        linkedCts.Token);

                    results.Add(result);

                    // Update progress
                    var completed = Interlocked.Increment(ref completedCount);

                    // Invoke item completed callback
                    try
                    {
                        options.OnItemCompleted?.Invoke(new BatchItemCompletedEventArgs
                        {
                            GrainId = request.GrainId,
                            IsSuccess = result.IsSuccess,
                            ErrorMessage = result.ErrorMessage,
                            BatchIndex = index,
                            CompletedCount = completed,
                            TotalCount = totalCount
                        });
                    }
                    catch (Exception ex)
                    {
                        _logger?.LogError(ex, "Exception in OnItemCompleted callback");
                    }

                    // Check if we should stop on failure
                    if (!result.IsSuccess && options.StopOnFirstFailure)
                    {
                        shouldStop = true;
                        _logger?.LogWarning(
                            "Stopping batch due to failure at index {Index}: {Error}",
                            index, result.ErrorMessage);
                    }
                }
                finally
                {
                    semaphore.Release();
                }
            });

            await Task.WhenAll(tasks);
        }
        catch (OperationCanceledException) when (timeoutCts.IsCancellationRequested)
        {
            _logger?.LogWarning("Batch operation timed out after {Timeout}", options.Timeout);
        }
        catch (OperationCanceledException)
        {
            _logger?.LogWarning("Batch operation was cancelled");
        }

        stopwatch.Stop();
        var endTime = DateTime.UtcNow;

        var resultList = results.OrderBy(r => r.BatchIndex).ToList();
        var successCount = resultList.Count(r => r.IsSuccess);
        var failureCount = resultList.Count(r => !r.IsSuccess && r.ErrorMessage != "Operation skipped due to batch cancellation");
        var skippedCount = resultList.Count(r => r.ErrorMessage == "Operation skipped due to batch cancellation");

        var batchResult = new BatchOperationResult<TState>
        {
            TotalOperations = totalCount,
            SuccessCount = successCount,
            FailureCount = failureCount,
            SkippedCount = skippedCount,
            Duration = stopwatch.Elapsed,
            StartTime = startTime,
            EndTime = endTime,
            Results = resultList
        };

        _logger?.LogInformation(
            "Batch operation completed: {Success}/{Total} succeeded, {Failed} failed, {Skipped} skipped in {Duration}ms",
            successCount, totalCount, failureCount, skippedCount, stopwatch.ElapsedMilliseconds);

        // Invoke batch completed callback
        try
        {
            options.OnBatchCompleted?.Invoke(new BatchCompletedEventArgs
            {
                SuccessCount = successCount,
                FailureCount = failureCount,
                Duration = stopwatch.Elapsed,
                WasCancelled = linkedCts.Token.IsCancellationRequested
            });
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "Exception in OnBatchCompleted callback");
        }

        return batchResult;
    }

    /// <summary>
    /// Executes a single operation with optional retry logic.
    /// </summary>
    private async Task<BatchItemResult<TState>> ExecuteSingleOperationAsync<TState, TTrigger>(
        BatchOperationRequest<TTrigger> request,
        int batchIndex,
        Func<string, object> grainResolver,
        Func<object, TTrigger, object[]?, Task> fireAsync,
        Func<object, Task<TState>> getState,
        BatchOperationOptions options,
        CancellationToken cancellationToken)
    {
        var itemStopwatch = Stopwatch.StartNew();
        var attempts = 0;
        var maxAttempts = options.EnableRetry ? options.MaxRetryAttempts : 1;

        while (attempts < maxAttempts)
        {
            attempts++;

            try
            {
                // Create timeout for this operation
                using var operationCts = new CancellationTokenSource(options.OperationTimeout);
                using var linkedCts = CancellationTokenSource.CreateLinkedTokenSource(
                    cancellationToken, operationCts.Token);

                var grain = grainResolver(request.GrainId);

                // Get state before transition
                var stateBefore = await getState(grain);

                // Fire the trigger
                await fireAsync(grain, request.Trigger, request.Arguments);

                // Get state after transition
                var stateAfter = await getState(grain);

                itemStopwatch.Stop();

                return BatchItemResult<TState>.Success(
                    request.GrainId,
                    stateBefore,
                    stateAfter,
                    itemStopwatch.Elapsed,
                    batchIndex,
                    request.CorrelationId);
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                itemStopwatch.Stop();
                return BatchItemResult<TState>.Failure(
                    request.GrainId,
                    "Operation cancelled",
                    nameof(OperationCanceledException),
                    itemStopwatch.Elapsed,
                    batchIndex,
                    request.CorrelationId);
            }
            catch (Exception ex)
            {
                _logger?.LogWarning(ex,
                    "Batch item {Index} (GrainId: {GrainId}) failed on attempt {Attempt}/{MaxAttempts}",
                    batchIndex, request.GrainId, attempts, maxAttempts);

                if (attempts < maxAttempts && options.EnableRetry)
                {
                    var delay = CalculateRetryDelay(attempts, options);
                    _logger?.LogDebug("Retrying batch item {Index} in {Delay}ms", batchIndex, delay.TotalMilliseconds);
                    await Task.Delay(delay, cancellationToken);
                    continue;
                }

                itemStopwatch.Stop();
                return BatchItemResult<TState>.Failure(
                    request.GrainId,
                    ex.Message,
                    ex.GetType().Name,
                    itemStopwatch.Elapsed,
                    batchIndex,
                    request.CorrelationId);
            }
        }

        // Should not reach here, but just in case
        itemStopwatch.Stop();
        return BatchItemResult<TState>.Failure(
            request.GrainId,
            "Maximum retry attempts exceeded",
            null,
            itemStopwatch.Elapsed,
            batchIndex,
            request.CorrelationId);
    }

    /// <summary>
    /// Calculates the retry delay with optional exponential backoff.
    /// </summary>
    private static TimeSpan CalculateRetryDelay(int attempt, BatchOperationOptions options)
    {
        if (!options.UseExponentialBackoff)
        {
            return options.RetryDelay;
        }

        // Exponential backoff: delay * 2^(attempt-1)
        var multiplier = Math.Pow(2, attempt - 1);
        var delay = TimeSpan.FromTicks((long)(options.RetryDelay.Ticks * multiplier));

        // Cap at 30 seconds
        return delay > TimeSpan.FromSeconds(30) ? TimeSpan.FromSeconds(30) : delay;
    }
}
