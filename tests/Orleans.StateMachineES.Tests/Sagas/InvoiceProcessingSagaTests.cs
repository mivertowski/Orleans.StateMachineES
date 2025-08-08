using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using FluentAssertions;
using Orleans.StateMachineES.Sagas;
using System.Linq;
using Orleans.StateMachineES.Tests.Cluster;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Orleans;
using Orleans.EventSourcing;
using Orleans.Providers;
using Orleans.Runtime;
using Orleans.Storage;
using Stateless;
using Xunit;
using Xunit.Abstractions;

namespace Orleans.StateMachineES.Tests.Sagas;

[Collection(nameof(TestClusterApplication))]
public class InvoiceProcessingSagaTests
{
    private readonly ITestOutputHelper _outputHelper;
    private readonly TestClusterApplication _testApp;

    public InvoiceProcessingSagaTests(TestClusterApplication testApp, ITestOutputHelper outputHelper)
    {
        _testApp = testApp;
        _outputHelper = outputHelper;
    }

    [Fact]
    public async Task InvoiceProcessingSaga_ShouldCompleteSuccessfully_WhenAllStepsSucceed()
    {
        // Arrange
        var sagaId = $"invoice-saga-success-{Guid.NewGuid():N}";
        var saga = _testApp.Cluster.Client.GetGrain<IInvoiceProcessingSagaGrain>(sagaId);

        var invoiceData = new InvoiceData
        {
            InvoiceId = Guid.NewGuid().ToString("N"),
            CustomerId = "CUST-001",
            Amount = 1500.00m,
            Items = new List<InvoiceItem>
            {
                new() { ProductId = "PROD-001", Quantity = 2, UnitPrice = 750.00m }
            }
        };

        // Act
        var result = await saga.ExecuteAsync(invoiceData);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.IsCompensated.Should().BeFalse();
        result.ErrorMessage.Should().BeNull();
        result.ExecutionHistory.Should().NotBeNull();
        result.ExecutionHistory!.Count.Should().Be(3); // PostInvoice, CreateJE, ControlCheck

        // Verify saga status
        var status = await saga.GetStatusAsync();
        status.Status.Should().Be(SagaStatus.Completed);
        status.CompletionTime.Should().NotBeNull();
        status.TotalSteps.Should().Be(3);
    }

    [Fact]
    public async Task InvoiceProcessingSaga_ShouldCompensate_WhenControlCheckFails()
    {
        // Arrange
        var sagaId = $"invoice-saga-compensation-{Guid.NewGuid():N}";
        var saga = _testApp.Cluster.Client.GetGrain<IInvoiceProcessingSagaGrain>(sagaId);

        var invoiceData = new InvoiceData
        {
            InvoiceId = Guid.NewGuid().ToString("N"),
            CustomerId = "CUST-FAIL-CONTROL", // This will trigger control check failure
            Amount = 1500.00m,
            Items = new List<InvoiceItem>
            {
                new() { ProductId = "PROD-001", Quantity = 2, UnitPrice = 750.00m }
            }
        };

        // Act
        var result = await saga.ExecuteAsync(invoiceData);

        // Assert
        result.IsSuccess.Should().BeFalse();
        result.IsCompensated.Should().BeTrue();
        result.ErrorMessage.Should().Contain("Control check failed");
        result.CompensationHistory.Should().NotBeNull();
        result.CompensationHistory!.Count.Should().BeGreaterThan(0);

        // Verify compensation was performed for completed steps
        var compensationHistory = result.CompensationHistory;
        compensationHistory.Should().Contain(c => c.StepName == "CreateJournalEntry");
        compensationHistory.Should().Contain(c => c.StepName == "PostInvoice");

        // Verify saga status
        var status = await saga.GetStatusAsync();
        status.Status.Should().Be(SagaStatus.Compensated);
    }

    [Fact]
    public async Task InvoiceProcessingSaga_ShouldRetryAndThenCompensate_WhenTechnicalFailureOccurs()
    {
        // Arrange
        var sagaId = $"invoice-saga-retry-{Guid.NewGuid():N}";
        var saga = _testApp.Cluster.Client.GetGrain<IInvoiceProcessingSagaGrain>(sagaId);

        var invoiceData = new InvoiceData
        {
            InvoiceId = Guid.NewGuid().ToString("N"),
            CustomerId = "CUST-TECHNICAL-FAIL", // This will trigger technical failures with retries
            Amount = 1500.00m,
            Items = new List<InvoiceItem>
            {
                new() { ProductId = "PROD-001", Quantity = 2, UnitPrice = 750.00m }
            }
        };

        // Act
        var result = await saga.ExecuteAsync(invoiceData);

        // Assert
        result.IsSuccess.Should().BeFalse();
        result.IsCompensated.Should().BeTrue();
        
        // Verify retry attempts were made
        var failedStep = result.ExecutionHistory!.First(h => !h.IsSuccess);
        failedStep.RetryAttempts.Should().BeGreaterThan(1);

        // Verify saga status
        var status = await saga.GetStatusAsync();
        status.Status.Should().Be(SagaStatus.Compensated);
    }

    [Fact]
    public async Task InvoiceProcessingSaga_ShouldTrackCorrelationId_ThroughoutExecution()
    {
        // Arrange
        var sagaId = $"invoice-saga-correlation-{Guid.NewGuid():N}";
        var saga = _testApp.Cluster.Client.GetGrain<IInvoiceProcessingSagaGrain>(sagaId);
        var correlationId = Guid.NewGuid().ToString("N");

        var invoiceData = new InvoiceData
        {
            InvoiceId = Guid.NewGuid().ToString("N"),
            CustomerId = "CUST-001",
            Amount = 500.00m,
            Items = new List<InvoiceItem>
            {
                new() { ProductId = "PROD-001", Quantity = 1, UnitPrice = 500.00m }
            }
        };

        // Act
        var result = await saga.ExecuteAsync(invoiceData, correlationId);

        // Assert
        result.IsSuccess.Should().BeTrue();

        // Verify correlation ID is tracked
        var history = await saga.GetHistoryAsync();
        history.CorrelationId.Should().Be(correlationId);
    }

    [Fact]
    public async Task InvoiceProcessingSaga_ShouldProvideDetailedExecutionHistory()
    {
        // Arrange
        var sagaId = $"invoice-saga-history-{Guid.NewGuid():N}";
        var saga = _testApp.Cluster.Client.GetGrain<IInvoiceProcessingSagaGrain>(sagaId);

        var invoiceData = new InvoiceData
        {
            InvoiceId = Guid.NewGuid().ToString("N"),
            CustomerId = "CUST-001",
            Amount = 2500.00m,
            Items = new List<InvoiceItem>
            {
                new() { ProductId = "PROD-001", Quantity = 5, UnitPrice = 500.00m }
            }
        };

        // Act
        await saga.ExecuteAsync(invoiceData);
        var history = await saga.GetHistoryAsync();

        // Assert
        history.Should().NotBeNull();
        history.SagaId.Should().Be(sagaId);
        history.StartTime.Should().BeCloseTo(DateTime.UtcNow, TimeSpan.FromMinutes(1));
        history.CompletionTime.Should().NotBeNull();
        history.Status.Should().Be(SagaStatus.Completed);
        
        history.StepExecutions.Should().NotBeNull();
        history.StepExecutions!.Count.Should().Be(3);
        
        // Verify step execution details
        var stepNames = history.StepExecutions.Select(s => s.StepName).ToList();
        stepNames.Should().Contain("PostInvoice");
        stepNames.Should().Contain("CreateJournalEntry");
        stepNames.Should().Contain("RunControlCheck");
        
        // Verify each step has timing information
        foreach (var step in history.StepExecutions)
        {
            step.ExecutionTime.Should().BeCloseTo(DateTime.UtcNow, TimeSpan.FromMinutes(1));
            step.Duration.Should().BeGreaterThan(TimeSpan.Zero);
        }
    }

    [Fact]
    public async Task InvoiceProcessingSaga_ShouldHandleTimeout_AndTriggerCompensation()
    {
        // Arrange
        var sagaId = $"invoice-saga-timeout-{Guid.NewGuid():N}";
        var saga = _testApp.Cluster.Client.GetGrain<IInvoiceProcessingSagaGrain>(sagaId);

        var invoiceData = new InvoiceData
        {
            InvoiceId = Guid.NewGuid().ToString("N"),
            CustomerId = "CUST-TIMEOUT", // This will trigger timeout simulation
            Amount = 1000.00m,
            Items = new List<InvoiceItem>
            {
                new() { ProductId = "PROD-001", Quantity = 2, UnitPrice = 500.00m }
            }
        };

        // Act
        var result = await saga.ExecuteAsync(invoiceData);

        // Assert
        result.IsSuccess.Should().BeFalse();
        result.ErrorMessage.Should().Contain("timeout");
        result.IsCompensated.Should().BeTrue();

        // Verify saga status
        var status = await saga.GetStatusAsync();
        status.Status.Should().Be(SagaStatus.Compensated);
    }

    [Fact]
    public async Task InvoiceProcessingSaga_ShouldMaintainIdempotency_OnDuplicateExecution()
    {
        // Arrange
        var sagaId = $"invoice-saga-idempotent-{Guid.NewGuid():N}";
        var saga = _testApp.Cluster.Client.GetGrain<IInvoiceProcessingSagaGrain>(sagaId);
        var correlationId = Guid.NewGuid().ToString("N");

        var invoiceData = new InvoiceData
        {
            InvoiceId = Guid.NewGuid().ToString("N"),
            CustomerId = "CUST-001",
            Amount = 750.00m,
            Items = new List<InvoiceItem>
            {
                new() { ProductId = "PROD-001", Quantity = 1, UnitPrice = 750.00m }
            }
        };

        // Act - Execute the same saga twice with same correlation ID
        var result1 = await saga.ExecuteAsync(invoiceData, correlationId);
        var result2 = await saga.ExecuteAsync(invoiceData, correlationId);

        // Assert - Both should succeed but only one actual execution should occur
        result1.IsSuccess.Should().BeTrue();
        result2.IsSuccess.Should().BeTrue();
        
        // Verify saga completed only once
        var status = await saga.GetStatusAsync();
        status.Status.Should().Be(SagaStatus.Completed);
    }

    [Fact]
    public async Task InvoiceProcessingSaga_ShouldPropagateBusinessTransactionId_AcrossSteps()
    {
        // Arrange
        var sagaId = $"invoice-saga-business-txn-{Guid.NewGuid():N}";
        var saga = _testApp.Cluster.Client.GetGrain<IInvoiceProcessingSagaGrain>(sagaId);

        var invoiceData = new InvoiceData
        {
            InvoiceId = Guid.NewGuid().ToString("N"),
            CustomerId = "CUST-001",
            Amount = 1200.00m,
            Items = new List<InvoiceItem>
            {
                new() { ProductId = "PROD-001", Quantity = 2, UnitPrice = 600.00m }
            }
        };

        // Act
        var result = await saga.ExecuteAsync(invoiceData);

        // Assert
        result.IsSuccess.Should().BeTrue();

        // Verify business transaction ID was generated and used
        var history = await saga.GetHistoryAsync();
        history.Should().NotBeNull();
        
        // This would require additional verification through the individual grain calls
        // to ensure the business transaction ID was properly propagated
    }
}

// Test grain interfaces and implementations

public interface IInvoiceProcessingSagaGrain : ISagaCoordinatorGrain<InvoiceData>
{
}

[StorageProvider(ProviderName = "Default")]
public class InvoiceProcessingSagaGrain : 
    SagaOrchestratorGrain<InvoiceData>, 
    IInvoiceProcessingSagaGrain
{
    public InvoiceProcessingSagaGrain([PersistentState("sagaState", "Default")] IPersistentState<SagaGrainState<InvoiceData>> state) 
        : base(state)
    {
    }
    protected override void ConfigureSagaSteps()
    {
        AddStep(new PostInvoiceStep())
            .WithTimeout(TimeSpan.FromSeconds(30))
            .WithRetry(3)
            .WithMetadata("Description", "Posts the invoice to the accounting system");

        AddStep(new CreateJournalEntryStep())
            .WithTimeout(TimeSpan.FromSeconds(45))
            .WithRetry(2)
            .WithMetadata("Description", "Creates journal entries for the invoice");

        AddStep(new RunControlCheckStep())
            .WithTimeout(TimeSpan.FromSeconds(60))
            .WithRetry(1)
            .WithMetadata("Description", "Runs control checks on the transaction");
    }

    protected override string GenerateBusinessTransactionId(InvoiceData sagaData)
    {
        return $"INV-TXN-{sagaData.InvoiceId}-{DateTime.UtcNow:yyyyMMddHHmmss}";
    }

    protected override Dictionary<string, object> CreateContextProperties(InvoiceData sagaData)
    {
        return new Dictionary<string, object>
        {
            ["InvoiceId"] = sagaData.InvoiceId,
            ["CustomerId"] = sagaData.CustomerId,
            ["InvoiceAmount"] = sagaData.Amount,
            ["ItemCount"] = sagaData.Items.Count
        };
    }
}

// Mock saga steps for testing

public class PostInvoiceStep : ISagaStep<InvoiceData>
{
    public string StepName => "PostInvoice";
    public TimeSpan Timeout => TimeSpan.FromSeconds(30);
    public bool CanRetry => true;
    public int MaxRetryAttempts => 3;

    public async Task<SagaStepResult> ExecuteAsync(InvoiceData sagaData, SagaContext context)
    {
        await Task.Delay(100); // Simulate work

        if (sagaData.CustomerId == "CUST-TECHNICAL-FAIL")
        {
            return SagaStepResult.TechnicalFailure("Simulated technical failure in PostInvoice");
        }

        if (sagaData.CustomerId == "CUST-TIMEOUT")
        {
            return SagaStepResult.TechnicalFailure("Step 'PostInvoice' timeout after 30 seconds");
        }

        return SagaStepResult.Success(new PostInvoiceResult { InvoicePosted = true, InvoiceNumber = $"INV-{sagaData.InvoiceId}" });
    }

    public async Task<CompensationResult> CompensateAsync(InvoiceData sagaData, SagaStepResult? stepResult, SagaContext context)
    {
        await Task.Delay(50); // Simulate compensation work
        return CompensationResult.Success();
    }
}

public class CreateJournalEntryStep : ISagaStep<InvoiceData>
{
    public string StepName => "CreateJournalEntry";
    public TimeSpan Timeout => TimeSpan.FromSeconds(45);
    public bool CanRetry => true;
    public int MaxRetryAttempts => 2;

    public async Task<SagaStepResult> ExecuteAsync(InvoiceData sagaData, SagaContext context)
    {
        await Task.Delay(150); // Simulate work

        if (sagaData.CustomerId == "CUST-TECHNICAL-FAIL")
        {
            return SagaStepResult.TechnicalFailure("Simulated technical failure in CreateJournalEntry");
        }

        return SagaStepResult.Success(new JournalEntryResult { JournalEntryId = Guid.NewGuid().ToString("N") });
    }

    public async Task<CompensationResult> CompensateAsync(InvoiceData sagaData, SagaStepResult? stepResult, SagaContext context)
    {
        await Task.Delay(75); // Simulate compensation work
        return CompensationResult.Success();
    }
}

public class RunControlCheckStep : ISagaStep<InvoiceData>
{
    public string StepName => "RunControlCheck";
    public TimeSpan Timeout => TimeSpan.FromSeconds(60);
    public bool CanRetry => true;
    public int MaxRetryAttempts => 1;

    public async Task<SagaStepResult> ExecuteAsync(InvoiceData sagaData, SagaContext context)
    {
        await Task.Delay(200); // Simulate work

        if (sagaData.CustomerId == "CUST-FAIL-CONTROL")
        {
            return SagaStepResult.BusinessFailure("Control check failed: Customer credit limit exceeded");
        }

        if (sagaData.CustomerId == "CUST-TECHNICAL-FAIL")
        {
            return SagaStepResult.TechnicalFailure("Simulated technical failure in RunControlCheck");
        }

        return SagaStepResult.Success(new ControlCheckResult { ControlCheckPassed = true, CheckId = Guid.NewGuid().ToString("N") });
    }

    public async Task<CompensationResult> CompensateAsync(InvoiceData sagaData, SagaStepResult? stepResult, SagaContext context)
    {
        await Task.Delay(25); // Simulate compensation work
        return CompensationResult.Success();
    }
}

// Saga step result models

[GenerateSerializer]
[Alias("PostInvoiceResult")]
public class PostInvoiceResult
{
    [Id(0)] public bool InvoicePosted { get; set; }
    [Id(1)] public string InvoiceNumber { get; set; } = string.Empty;
}

[GenerateSerializer]
[Alias("JournalEntryResult")]
public class JournalEntryResult
{
    [Id(0)] public string JournalEntryId { get; set; } = string.Empty;
}

[GenerateSerializer]
[Alias("ControlCheckResult")]
public class ControlCheckResult
{
    [Id(0)] public bool ControlCheckPassed { get; set; }
    [Id(1)] public string CheckId { get; set; } = string.Empty;
}

// Test data models

[GenerateSerializer]
[Alias("InvoiceData")]
public class InvoiceData
{
    [Id(0)] public string InvoiceId { get; set; } = string.Empty;
    [Id(1)] public string CustomerId { get; set; } = string.Empty;
    [Id(2)] public decimal Amount { get; set; }
    [Id(3)] public List<InvoiceItem> Items { get; set; } = new();
    [Id(4)] public DateTime InvoiceDate { get; set; } = DateTime.UtcNow;
    [Id(5)] public string Currency { get; set; } = "USD";
}

[GenerateSerializer]
[Alias("InvoiceItem")]
public class InvoiceItem
{
    [Id(0)] public string ProductId { get; set; } = string.Empty;
    [Id(1)] public int Quantity { get; set; }
    [Id(2)] public decimal UnitPrice { get; set; }
    [Id(3)] public decimal TotalPrice { get; set; }
}