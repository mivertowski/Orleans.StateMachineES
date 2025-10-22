using Microsoft.Extensions.Logging;
using Orleans.StateMachineES.Sagas;

namespace Orleans.StateMachineES.Tests.Integration.TestGrains;

/// <summary>
/// Complex saga grain for testing multi-step saga orchestration.
/// </summary>
public class ComplexSagaGrain([PersistentState("sagaState", "Default")] IPersistentState<SagaGrainState<ComplexSagaData>> state) : SagaOrchestratorGrain<ComplexSagaData>(state), IComplexSagaGrain
{
    private ILogger<ComplexSagaGrain>? _logger;

    public override async Task OnActivateAsync(CancellationToken cancellationToken)
    {
        await base.OnActivateAsync(cancellationToken);
        _logger = ServiceProvider.GetService(typeof(ILogger<ComplexSagaGrain>)) as ILogger<ComplexSagaGrain>;
    }

    protected override void ConfigureSagaSteps()
    {
        // Configure saga steps for complex multi-step process
        AddStep("payment", new PaymentProcessingStep(_logger))
            .WithTimeout(TimeSpan.FromSeconds(30))
            .WithRetry(3);

        AddStep("inventory", new InventoryReservationStep(_logger))
            .WithTimeout(TimeSpan.FromSeconds(20))
            .WithRetry(2);

        AddStep("shipping", new ShippingArrangementStep(_logger))
            .WithTimeout(TimeSpan.FromSeconds(15))
            .WithRetry(1);

        AddStep("notification", new NotificationStep(_logger))
            .WithTimeout(TimeSpan.FromSeconds(10))
            .WithRetry(3);
    }

    public new async Task<Integration.SagaExecutionResult> ExecuteAsync(ComplexSagaData data, string correlationId)
    {
        var result = await base.ExecuteAsync(data, correlationId);
        
        // Convert from Orleans.StateMachineES.Sagas.SagaExecutionResult to Integration.SagaExecutionResult
        return new Integration.SagaExecutionResult
        {
            IsSuccess = result.IsSuccess,
            IsCompensated = result.IsCompensated,
            Status = result.IsSuccess ? "Completed" : result.IsCompensated ? "Compensated" : "Failed",
            CompletedSteps = result.ExecutionHistory?.Where(h => h.IsSuccess).Select(h => h.StepName).ToList() ?? [],
            CompensatedSteps = result.CompensationHistory?.Where(c => c.IsSuccess).Select(c => c.StepName).ToList() ?? []
        };
    }
}

/// <summary>
/// Payment processing step implementation.
/// </summary>
public class PaymentProcessingStep(ILogger? logger) : ISagaStep<ComplexSagaData>
{
    private readonly ILogger? _logger = logger;

    public string StepName => "PaymentProcessing";
    public TimeSpan Timeout => TimeSpan.FromSeconds(30);
    public bool CanRetry => true;
    public int MaxRetryAttempts => 3;

    public async Task<SagaStepResult> ExecuteAsync(ComplexSagaData data, SagaContext context)
    {
        _logger?.LogInformation("Processing payment for saga {SagaId}", data.SagaId);
        
        // Simulate payment processing
        await Task.Delay(100);

        // Check if compensation is required (for testing)
        if (data.RequiresCompensation && data.Steps.Contains("payment"))
        {
            return SagaStepResult.BusinessFailure("Payment processing failed - insufficient funds");
        }

        return SagaStepResult.Success("Payment processed successfully");
    }

    public async Task<CompensationResult> CompensateAsync(ComplexSagaData data, SagaStepResult? stepResult, SagaContext context)
    {
        _logger?.LogInformation("Compensating payment for saga {SagaId}", data.SagaId);
        
        // Simulate payment refund
        await Task.Delay(50);
        
        return CompensationResult.Success(TimeSpan.FromMilliseconds(50));
    }
}

/// <summary>
/// Inventory reservation step implementation.
/// </summary>
public class InventoryReservationStep(ILogger? logger) : ISagaStep<ComplexSagaData>
{
    private readonly ILogger? _logger = logger;

    public string StepName => "InventoryReservation";
    public TimeSpan Timeout => TimeSpan.FromSeconds(20);
    public bool CanRetry => true;
    public int MaxRetryAttempts => 2;

    public async Task<SagaStepResult> ExecuteAsync(ComplexSagaData data, SagaContext context)
    {
        _logger?.LogInformation("Reserving inventory for saga {SagaId}", data.SagaId);
        
        // Simulate inventory check and reservation
        await Task.Delay(80);

        // Check if compensation is required
        if (data.RequiresCompensation && data.Steps.Contains("inventory"))
        {
            return SagaStepResult.BusinessFailure("Inventory reservation failed - insufficient stock");
        }

        return SagaStepResult.Success("Inventory reserved successfully");
    }

    public async Task<CompensationResult> CompensateAsync(ComplexSagaData data, SagaStepResult? stepResult, SagaContext context)
    {
        _logger?.LogInformation("Releasing inventory reservation for saga {SagaId}", data.SagaId);
        
        // Simulate inventory release
        await Task.Delay(30);
        
        return CompensationResult.Success(TimeSpan.FromMilliseconds(30));
    }
}

/// <summary>
/// Shipping arrangement step implementation.
/// </summary>
public class ShippingArrangementStep(ILogger? logger) : ISagaStep<ComplexSagaData>
{
    private readonly ILogger? _logger = logger;

    public string StepName => "ShippingArrangement";
    public TimeSpan Timeout => TimeSpan.FromSeconds(15);
    public bool CanRetry => true;
    public int MaxRetryAttempts => 1;

    public async Task<SagaStepResult> ExecuteAsync(ComplexSagaData data, SagaContext context)
    {
        _logger?.LogInformation("Arranging shipping for saga {SagaId}", data.SagaId);
        
        // Simulate shipping arrangement
        await Task.Delay(60);

        // Check if compensation is required
        if (data.RequiresCompensation && data.Steps.Contains("shipping"))
        {
            return SagaStepResult.BusinessFailure("Shipping arrangement failed - carrier unavailable");
        }

        return SagaStepResult.Success("Shipping arranged successfully");
    }

    public async Task<CompensationResult> CompensateAsync(ComplexSagaData data, SagaStepResult? stepResult, SagaContext context)
    {
        _logger?.LogInformation("Cancelling shipping arrangement for saga {SagaId}", data.SagaId);
        
        // Simulate shipping cancellation
        await Task.Delay(40);
        
        return CompensationResult.Success(TimeSpan.FromMilliseconds(40));
    }
}

/// <summary>
/// Notification step implementation.
/// </summary>
public class NotificationStep(ILogger? logger) : ISagaStep<ComplexSagaData>
{
    private readonly ILogger? _logger = logger;

    public string StepName => "CustomerNotification";
    public TimeSpan Timeout => TimeSpan.FromSeconds(10);
    public bool CanRetry => true;
    public int MaxRetryAttempts => 3;

    public async Task<SagaStepResult> ExecuteAsync(ComplexSagaData data, SagaContext context)
    {
        _logger?.LogInformation("Sending notifications for saga {SagaId}", data.SagaId);
        
        // Simulate notification sending
        await Task.Delay(40);

        // Check if compensation is required
        if (data.RequiresCompensation && data.Steps.Contains("notification"))
        {
            return SagaStepResult.BusinessFailure("Notification failed - customer unreachable");
        }

        return SagaStepResult.Success("Notifications sent successfully");
    }

    public async Task<CompensationResult> CompensateAsync(ComplexSagaData data, SagaStepResult? stepResult, SagaContext context)
    {
        _logger?.LogInformation("Sending cancellation notification for saga {SagaId}", data.SagaId);
        
        // Simulate cancellation notification
        await Task.Delay(20);
        
        return CompensationResult.Success(TimeSpan.FromMilliseconds(20));
    }
}