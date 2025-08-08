using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Orleans;
using Orleans.Runtime;
using Orleans.Storage;
using Stateless;

namespace Orleans.StateMachineES.Sagas;

/// <summary>
/// Base grain class for saga orchestration.
/// Provides comprehensive saga management with compensation, retry logic, and audit trails.
/// </summary>
/// <typeparam name="TSagaData">The type of data passed between saga steps.</typeparam>
public abstract class SagaOrchestratorGrain<TSagaData> : 
    Grain,
    ISagaCoordinatorGrain<TSagaData>
    where TSagaData : class
{
    private ILogger<SagaOrchestratorGrain<TSagaData>>? _sagaLogger;
    private readonly List<SagaStepDefinition<TSagaData>> _steps = new();
    private readonly List<SagaStepExecution> _executionHistory = new();
    private SagaContext? _sagaContext;
    private StateMachine<SagaStatus, SagaTrigger>? _stateMachine;
    
    /// <summary>
    /// The saga state containing all saga-specific data.
    /// </summary>
    private readonly IPersistentState<SagaGrainState<TSagaData>> _state;
    
    /// <summary>
    /// Constructor for dependency injection.
    /// </summary>
    /// <param name="state">The persistent state for the saga.</param>
    protected SagaOrchestratorGrain([PersistentState("sagaState", "Default")] IPersistentState<SagaGrainState<TSagaData>> state)
    {
        _state = state;
    }

    /// <inheritdoc/>
    public override async Task OnActivateAsync(CancellationToken cancellationToken)
    {
        await base.OnActivateAsync(cancellationToken);
        
        _sagaLogger = this.ServiceProvider.GetService<ILogger<SagaOrchestratorGrain<TSagaData>>>();
        
        // Initialize state machine
        _stateMachine = BuildStateMachine();
        if (_state.State.Status != SagaStatus.NotStarted)
        {
            // Restore state machine to current status
            _stateMachine = new StateMachine<SagaStatus, SagaTrigger>(_state.State.Status);
            _stateMachine = BuildStateMachine();
        }
        
        // Configure saga-specific steps
        ConfigureSagaSteps();
        
        _sagaLogger?.LogInformation("Saga orchestrator grain {SagaId} activated with {StepCount} steps", 
            this.GetPrimaryKeyString(), _steps.Count);
    }
    
    /// <summary>
    /// Fires a trigger and updates the state.
    /// </summary>
    private async Task FireTriggerAsync(SagaTrigger trigger)
    {
        if (_stateMachine?.CanFire(trigger) == true)
        {
            _stateMachine.Fire(trigger);
            _state.State.Status = _stateMachine.State;
            await _state.WriteStateAsync();
        }
    }

    /// <summary>
    /// Configure the saga steps. Override this method to define the saga workflow.
    /// </summary>
    protected abstract void ConfigureSagaSteps();

    /// <summary>
    /// Builds the state machine for saga orchestration.
    /// </summary>
    protected virtual StateMachine<SagaStatus, SagaTrigger> BuildStateMachine()
    {
        var machine = new StateMachine<SagaStatus, SagaTrigger>(SagaStatus.NotStarted);

        machine.Configure(SagaStatus.NotStarted)
            .Permit(SagaTrigger.Start, SagaStatus.Running);

        machine.Configure(SagaStatus.Running)
            .OnEntry(() => _sagaLogger?.LogDebug("Saga {SagaId} is now running", this.GetPrimaryKeyString()))
            .PermitReentry(SagaTrigger.StepCompleted)
            .Permit(SagaTrigger.Complete, SagaStatus.Completed)
            .Permit(SagaTrigger.StepFailedBusiness, SagaStatus.Compensating)
            .Permit(SagaTrigger.StepFailedTechnical, SagaStatus.Compensating)
            .Permit(SagaTrigger.StartCompensation, SagaStatus.Compensating)
            .Permit(SagaTrigger.Timeout, SagaStatus.Compensating);

        machine.Configure(SagaStatus.Compensating)
            .OnEntry(() => _sagaLogger?.LogWarning("Saga {SagaId} started compensation", this.GetPrimaryKeyString()))
            .Permit(SagaTrigger.CompensationCompleted, SagaStatus.Compensated)
            .Permit(SagaTrigger.CompensationFailed, SagaStatus.CompensationFailed);

        machine.Configure(SagaStatus.Completed)
            .OnEntry(() => _sagaLogger?.LogInformation("Saga {SagaId} completed successfully", this.GetPrimaryKeyString()))
            .Ignore(SagaTrigger.StepCompleted)
            .Ignore(SagaTrigger.Complete);

        machine.Configure(SagaStatus.Compensated)
            .OnEntry(() => _sagaLogger?.LogInformation("Saga {SagaId} was compensated", this.GetPrimaryKeyString()))
            .Ignore(SagaTrigger.CompensationCompleted);

        machine.Configure(SagaStatus.CompensationFailed)
            .OnEntry(() => _sagaLogger?.LogError("Saga {SagaId} compensation failed", this.GetPrimaryKeyString()));

        machine.Configure(SagaStatus.Failed)
            .OnEntry(() => _sagaLogger?.LogError("Saga {SagaId} failed", this.GetPrimaryKeyString()))
            .Ignore(SagaTrigger.Fail);

        return machine;
    }


    /// <summary>
    /// Adds a saga step to the execution sequence.
    /// </summary>
    /// <param name="step">The saga step to add.</param>
    /// <returns>A builder for further configuration.</returns>
    protected SagaStepBuilder<TSagaData> AddStep(ISagaStep<TSagaData> step)
    {
        var definition = new SagaStepDefinition<TSagaData>(step, _steps.Count);
        _steps.Add(definition);
        return new SagaStepBuilder<TSagaData>(definition);
    }

    /// <summary>
    /// Adds a saga step by name and creates an instance.
    /// </summary>
    /// <param name="stepName">The name of the step.</param>
    /// <param name="step">The saga step instance.</param>
    /// <returns>A builder for further configuration.</returns>
    protected SagaStepBuilder<TSagaData> AddStep(string stepName, ISagaStep<TSagaData> step)
    {
        var definition = new SagaStepDefinition<TSagaData>(step, _steps.Count, stepName);
        _steps.Add(definition);
        return new SagaStepBuilder<TSagaData>(definition);
    }

    /// <summary>
    /// Executes the saga with the provided data.
    /// </summary>
    /// <param name="sagaData">The data to process through the saga.</param>
    /// <param name="correlationId">Optional correlation ID for tracking.</param>
    /// <returns>The result of the saga execution.</returns>
    public virtual async Task<SagaExecutionResult> ExecuteAsync(TSagaData sagaData, string? correlationId = null)
    {
        var startTime = DateTime.UtcNow;
        
        try
        {
            // Initialize saga context
            _sagaContext = CreateSagaContext(sagaData, correlationId);
            
            // Update grain state
            _state.State.SagaData = sagaData;
            _state.State.SagaContext = _sagaContext;
            _state.State.StartTime = startTime;
            _state.State.Status = SagaStatus.Running;
            
            // Transition to running state
            await FireTriggerAsync(SagaTrigger.Start);
            
            _sagaLogger?.LogInformation("Starting saga {SagaId} with correlation {CorrelationId}", 
                _sagaContext.SagaId, _sagaContext.CorrelationId);

            // Execute saga steps sequentially
            for (int stepIndex = 0; stepIndex < _steps.Count; stepIndex++)
            {
                var stepDefinition = _steps[stepIndex];
                _sagaContext.CurrentStepIndex = stepIndex;
                _sagaContext.CurrentStepName = stepDefinition.StepName;

                var (stepResult, retryAttempts) = await ExecuteStepWithRetryAsync(stepDefinition, sagaData, _sagaContext);
                
                // Record step execution
                var execution = new SagaStepExecution
                {
                    StepIndex = stepIndex,
                    StepName = stepDefinition.StepName,
                    ExecutionTime = stepResult.ExecutionTime,
                    Duration = stepResult.Duration,
                    IsSuccess = stepResult.IsSuccess,
                    ErrorMessage = stepResult.ErrorMessage,
                    Result = stepResult.Result,
                    RetryAttempts = retryAttempts
                };
                _executionHistory.Add(execution);

                if (!stepResult.IsSuccess)
                {
                    _sagaLogger?.LogWarning("Saga step {StepName} failed: {ErrorMessage}", 
                        stepDefinition.StepName, stepResult.ErrorMessage);

                    // Start compensation process
                    var compensationResult = await CompensateAsync($"Step '{stepDefinition.StepName}' failed: {stepResult.ErrorMessage}");
                    
                    return new SagaExecutionResult
                    {
                        IsSuccess = false,
                        IsCompensated = compensationResult.IsSuccess,
                        ErrorMessage = stepResult.ErrorMessage,
                        ExecutionHistory = _executionHistory.AsReadOnly(),
                        CompensationHistory = _state.State.CompensationHistory,
                        Duration = DateTime.UtcNow - startTime
                    };
                }

                // Fire step completed trigger
                await FireTriggerAsync(SagaTrigger.StepCompleted);
                _sagaLogger?.LogDebug("Saga step {StepName} completed successfully", stepDefinition.StepName);
            }

            // Mark saga as completed
            _state.State.Status = SagaStatus.Completed;
            _state.State.CompletionTime = DateTime.UtcNow;
            await FireTriggerAsync(SagaTrigger.Complete);

            _sagaLogger?.LogInformation("Saga {SagaId} completed successfully in {Duration}ms", 
                _sagaContext.SagaId, (DateTime.UtcNow - startTime).TotalMilliseconds);

            return new SagaExecutionResult
            {
                IsSuccess = true,
                IsCompensated = false,
                ExecutionHistory = _executionHistory.AsReadOnly(),
                CompensationHistory = _state.State.CompensationHistory,
                Duration = DateTime.UtcNow - startTime
            };
        }
        catch (Exception ex)
        {
            _sagaLogger?.LogError(ex, "Unexpected error during saga execution");
            
            _state.State.Status = SagaStatus.Failed;
            _state.State.ErrorMessage = ex.Message;
            
            // Attempt compensation
            var compensationResult = await CompensateAsync($"Unexpected error: {ex.Message}");
            
            return new SagaExecutionResult
            {
                IsSuccess = false,
                IsCompensated = compensationResult.IsSuccess,
                ErrorMessage = ex.Message,
                ExecutionHistory = _executionHistory.AsReadOnly(),
                CompensationHistory = _state.State.CompensationHistory,
                Duration = DateTime.UtcNow - startTime
            };
        }
    }

    /// <summary>
    /// Compensates the saga by rolling back completed steps in reverse order.
    /// </summary>
    /// <param name="reason">The reason for compensation.</param>
    /// <returns>The result of the compensation process.</returns>
    public virtual async Task<CompensationResult> CompensateAsync(string reason)
    {
        if (_sagaContext == null)
        {
            return CompensationResult.Failure("Saga context not initialized");
        }

        var startTime = DateTime.UtcNow;
        _sagaContext.IsCompensating = true;
        _state.State.Status = SagaStatus.Compensating;
        
        // Transition to compensating state
        await FireTriggerAsync(SagaTrigger.StartCompensation);
        
        _sagaLogger?.LogWarning("Starting compensation for saga {SagaId}: {Reason}", 
            _sagaContext.SagaId, reason);

        try
        {
            // Compensate in reverse order of execution
            var completedSteps = _executionHistory.Where(h => h.IsSuccess).Reverse().ToList();
            var allCompensationSucceeded = true;
            
            foreach (var stepExecution in completedSteps)
            {
                var stepDefinition = _steps[stepExecution.StepIndex];
                
                _sagaLogger?.LogDebug("Compensating step {StepName}", stepDefinition.StepName);
                
                var compensationResult = await CompensateStepAsync(stepDefinition, stepExecution);
                
                var compensationRecord = new CompensationExecution
                {
                    StepIndex = stepExecution.StepIndex,
                    StepName = stepExecution.StepName,
                    ExecutionTime = DateTime.UtcNow,
                    Duration = compensationResult.Duration,
                    IsSuccess = compensationResult.IsSuccess,
                    ErrorMessage = compensationResult.ErrorMessage
                };
                _state.State.CompensationHistory.Add(compensationRecord);

                if (!compensationResult.IsSuccess)
                {
                    allCompensationSucceeded = false;
                    _sagaLogger?.LogError("Failed to compensate step {StepName}: {ErrorMessage}", 
                        stepDefinition.StepName, compensationResult.ErrorMessage);
                }
            }

            if (allCompensationSucceeded)
            {
                _state.State.Status = SagaStatus.Compensated;
                _state.State.CompletionTime = DateTime.UtcNow;
                await FireTriggerAsync(SagaTrigger.CompensationCompleted);
                
                _sagaLogger?.LogInformation("Compensation completed for saga {SagaId} in {Duration}ms", 
                    _sagaContext.SagaId, (DateTime.UtcNow - startTime).TotalMilliseconds);

                return CompensationResult.Success(DateTime.UtcNow - startTime);
            }
            else
            {
                _state.State.Status = SagaStatus.CompensationFailed;
                await FireTriggerAsync(SagaTrigger.CompensationFailed);
                
                return CompensationResult.Failure("Some compensations failed", null, DateTime.UtcNow - startTime);
            }
        }
        catch (Exception ex)
        {
            _sagaLogger?.LogError(ex, "Unexpected error during compensation");
            
            _state.State.Status = SagaStatus.CompensationFailed;
            _state.State.ErrorMessage = ex.Message;
            await FireTriggerAsync(SagaTrigger.CompensationFailed);
            
            return CompensationResult.Failure($"Compensation failed: {ex.Message}", ex, DateTime.UtcNow - startTime);
        }
    }

    /// <summary>
    /// Gets the current status of the saga.
    /// </summary>
    /// <returns>The saga status information.</returns>
    public Task<SagaStatusInfo> GetStatusAsync()
    {
        var statusInfo = new SagaStatusInfo
        {
            SagaId = this.GetPrimaryKeyString(),
            Status = _state.State.Status,
            StartTime = _state.State.StartTime,
            CompletionTime = _state.State.CompletionTime,
            ErrorMessage = _state.State.ErrorMessage,
            CurrentStepIndex = _sagaContext?.CurrentStepIndex ?? -1,
            CurrentStepName = _sagaContext?.CurrentStepName,
            TotalSteps = _steps.Count,
            ExecutionHistory = _executionHistory.AsReadOnly(),
            CompensationHistory = _state.State.CompensationHistory
        };

        return Task.FromResult(statusInfo);
    }

    /// <summary>
    /// Gets the execution history of the saga.
    /// </summary>
    /// <returns>The complete execution history.</returns>
    public Task<SagaExecutionHistory> GetHistoryAsync()
    {
        var history = new SagaExecutionHistory
        {
            SagaId = this.GetPrimaryKeyString(),
            CorrelationId = _sagaContext?.CorrelationId ?? "",
            StartTime = _state.State.StartTime,
            CompletionTime = _state.State.CompletionTime,
            Status = _state.State.Status,
            StepExecutions = _executionHistory.AsReadOnly(),
            CompensationExecutions = _state.State.CompensationHistory,
            TotalDuration = _state.State.CompletionTime.HasValue 
                ? _state.State.CompletionTime.Value - _state.State.StartTime 
                : DateTime.UtcNow - _state.State.StartTime
        };

        return Task.FromResult(history);
    }

    /// <summary>
    /// Creates the saga context for execution.
    /// </summary>
    protected virtual SagaContext CreateSagaContext(TSagaData sagaData, string? correlationId)
    {
        return new SagaContext
        {
            SagaId = this.GetPrimaryKeyString(),
            CorrelationId = correlationId ?? Guid.NewGuid().ToString("N"),
            BusinessTransactionId = GenerateBusinessTransactionId(sagaData),
            InitiatedBy = GetInitiatingUser(),
            StartTime = DateTime.UtcNow,
            Metadata = CreateContextProperties(sagaData)
        };
    }

    /// <summary>
    /// Generates a business transaction ID. Override to provide domain-specific logic.
    /// </summary>
    protected virtual string GenerateBusinessTransactionId(TSagaData sagaData)
    {
        return $"TXN-{DateTime.UtcNow:yyyyMMdd-HHmmss}-{Guid.NewGuid():N}";
    }

    /// <summary>
    /// Gets the initiating user. Override to provide authentication context.
    /// </summary>
    protected virtual string GetInitiatingUser()
    {
        return "System";
    }

    /// <summary>
    /// Creates context properties. Override to add domain-specific properties.
    /// </summary>
    protected virtual Dictionary<string, object> CreateContextProperties(TSagaData sagaData)
    {
        return new Dictionary<string, object>();
    }

    /// <summary>
    /// Executes a saga step with retry logic.
    /// </summary>
    private async Task<(SagaStepResult stepResult, int retryAttempts)> ExecuteStepWithRetryAsync(
        SagaStepDefinition<TSagaData> stepDefinition, 
        TSagaData sagaData, 
        SagaContext context)
    {
        var retryCount = 0;
        var maxRetries = stepDefinition.CanRetry ? stepDefinition.MaxRetryAttempts : 0;

        while (retryCount <= maxRetries)
        {
            try
            {
                using var cts = new CancellationTokenSource(stepDefinition.Timeout);
                var startTime = DateTime.UtcNow;
                
                var stepResult = await stepDefinition.Step.ExecuteAsync(sagaData, context);
                
                // Set actual duration if not provided
                if (stepResult.Duration == TimeSpan.Zero)
                {
                    var duration = DateTime.UtcNow - startTime;
                    stepResult = stepResult.IsSuccess 
                        ? SagaStepResult.Success(stepResult.Result, duration)
                        : stepResult.IsBusinessFailure 
                            ? SagaStepResult.BusinessFailure(stepResult.ErrorMessage ?? "", duration)
                            : SagaStepResult.TechnicalFailure(stepResult.ErrorMessage ?? "", stepResult.Exception, duration);
                }
                
                // If successful or business failure, don't retry
                if (stepResult.IsSuccess || stepResult.IsBusinessFailure)
                {
                    return (stepResult, retryCount);
                }

                // Technical failure - retry if possible
                if (retryCount < maxRetries)
                {
                    retryCount++;
                    var delay = CalculateRetryDelay(retryCount);
                    
                    _sagaLogger?.LogWarning("Step {StepName} failed, retrying in {Delay}ms (attempt {Attempt}/{MaxAttempts}): {Error}",
                        stepDefinition.StepName, delay.TotalMilliseconds, retryCount, maxRetries, stepResult.ErrorMessage);
                    
                    await Task.Delay(delay, cts.Token);
                    continue;
                }

                return (stepResult, retryCount);
            }
            catch (OperationCanceledException) when (retryCount <= maxRetries)
            {
                if (retryCount < maxRetries)
                {
                    retryCount++;
                    _sagaLogger?.LogWarning("Step {StepName} timed out, retrying (attempt {Attempt}/{MaxAttempts})",
                        stepDefinition.StepName, retryCount, maxRetries);
                    continue;
                }

                return (SagaStepResult.TechnicalFailure($"Step '{stepDefinition.StepName}' timed out after {maxRetries} retries"), retryCount);
            }
            catch (Exception ex)
            {
                _sagaLogger?.LogError(ex, "Unexpected error in step {StepName}", stepDefinition.StepName);
                return (SagaStepResult.TechnicalFailure($"Unexpected error: {ex.Message}", ex), retryCount);
            }
        }

        return (SagaStepResult.TechnicalFailure("Max retry attempts exceeded"), retryCount);
    }

    /// <summary>
    /// Compensates a specific step.
    /// </summary>
    private async Task<CompensationResult> CompensateStepAsync(
        SagaStepDefinition<TSagaData> stepDefinition,
        SagaStepExecution stepExecution)
    {
        if (_state.State.SagaData == null || _sagaContext == null)
        {
            return CompensationResult.Failure("Saga data or context not available");
        }

        var startTime = DateTime.UtcNow;
        
        try
        {
            using var cts = new CancellationTokenSource(stepDefinition.Timeout);
            
            // Create a SagaStepResult from the execution history
            var stepResult = stepExecution.IsSuccess 
                ? SagaStepResult.Success(stepExecution.Result)
                : SagaStepResult.BusinessFailure(stepExecution.ErrorMessage ?? "Unknown error");
                
            var compensationResult = await stepDefinition.Step.CompensateAsync(_state.State.SagaData, stepResult, _sagaContext);
            
            // Set actual duration if not provided
            if (compensationResult.Duration == TimeSpan.Zero)
            {
                var actualDuration = DateTime.UtcNow - startTime;
                compensationResult = compensationResult.IsSuccess 
                    ? CompensationResult.Success(actualDuration)
                    : CompensationResult.Failure(compensationResult.ErrorMessage ?? "", compensationResult.Exception, actualDuration);
            }
            
            return compensationResult;
        }
        catch (Exception ex)
        {
            _sagaLogger?.LogError(ex, "Error compensating step {StepName}", stepDefinition.StepName);
            return CompensationResult.Failure($"Compensation error: {ex.Message}", ex, DateTime.UtcNow - startTime);
        }
    }

    /// <summary>
    /// Calculates retry delay using exponential backoff with jitter.
    /// </summary>
    private static TimeSpan CalculateRetryDelay(int retryAttempt)
    {
        var baseDelay = TimeSpan.FromSeconds(Math.Pow(2, retryAttempt - 1));
        var jitter = TimeSpan.FromMilliseconds(Random.Shared.Next(0, 1000));
        return baseDelay + jitter;
    }
}

/// <summary>
/// Saga grain state for saga-specific data.
/// </summary>
/// <typeparam name="TSagaData">The type of saga data.</typeparam>
[GenerateSerializer]
public class SagaGrainState<TSagaData>
    where TSagaData : class
{
    /// <summary>
    /// The data being processed by the saga.
    /// </summary>
    [Id(0)]
    public TSagaData? SagaData { get; set; }
    
    /// <summary>
    /// The saga execution context.
    /// </summary>
    [Id(1)]
    public SagaContext? SagaContext { get; set; }
    
    /// <summary>
    /// The current status of the saga.
    /// </summary>
    [Id(2)]
    public SagaStatus Status { get; set; } = SagaStatus.NotStarted;
    
    /// <summary>
    /// When the saga was started.
    /// </summary>
    [Id(3)]
    public DateTime StartTime { get; set; }
    
    /// <summary>
    /// When the saga completed (if completed).
    /// </summary>
    [Id(4)]
    public DateTime? CompletionTime { get; set; }
    
    /// <summary>
    /// Error message if the saga failed.
    /// </summary>
    [Id(5)]
    public string? ErrorMessage { get; set; }
    
    /// <summary>
    /// History of compensation executions.
    /// </summary>
    [Id(6)]
    public List<CompensationExecution> CompensationHistory { get; set; } = new();
    
    /// <summary>
    /// Custom properties for the saga state.
    /// </summary>
    [Id(7)]
    public Dictionary<string, object> Properties { get; set; } = new();
}

/// <summary>
/// Builder for configuring saga step definitions.
/// </summary>
/// <typeparam name="TSagaData">The saga data type.</typeparam>
public class SagaStepBuilder<TSagaData> where TSagaData : class
{
    private readonly SagaStepDefinition<TSagaData> _definition;

    internal SagaStepBuilder(SagaStepDefinition<TSagaData> definition)
    {
        _definition = definition;
    }

    /// <summary>
    /// Sets the timeout for this step.
    /// </summary>
    public SagaStepBuilder<TSagaData> WithTimeout(TimeSpan timeout)
    {
        _definition.Timeout = timeout;
        return this;
    }

    /// <summary>
    /// Enables retry for this step with the specified max attempts.
    /// </summary>
    public SagaStepBuilder<TSagaData> WithRetry(int maxAttempts = 3)
    {
        _definition.CanRetry = true;
        _definition.MaxRetryAttempts = maxAttempts;
        return this;
    }

    /// <summary>
    /// Sets metadata for this step.
    /// </summary>
    public SagaStepBuilder<TSagaData> WithMetadata(string key, object value)
    {
        _definition.Metadata[key] = value;
        return this;
    }
}

/// <summary>
/// Definition of a saga step with configuration.
/// </summary>
/// <typeparam name="TSagaData">The saga data type.</typeparam>
public class SagaStepDefinition<TSagaData> where TSagaData : class
{
    public ISagaStep<TSagaData> Step { get; }
    public int Order { get; }
    public string StepName { get; }
    public TimeSpan Timeout { get; set; }
    public bool CanRetry { get; set; }
    public int MaxRetryAttempts { get; set; } = 3;
    public Dictionary<string, object> Metadata { get; } = new();

    public SagaStepDefinition(ISagaStep<TSagaData> step, int order, string? stepName = null)
    {
        Step = step;
        Order = order;
        StepName = stepName ?? step.StepName;
        Timeout = step.Timeout;
        CanRetry = step.CanRetry;
        MaxRetryAttempts = step.MaxRetryAttempts;
    }
}