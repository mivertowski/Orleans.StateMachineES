using Microsoft.Extensions.Logging;
using Orleans.StateMachineES.EventSourcing;
using Orleans.StateMachineES.Timers;
using Orleans.StateMachineES.Tracing;
using Stateless;

namespace Orleans.StateMachineES.Examples.ECommerceWorkflow;

/// <summary>
/// Simplified order processing grain demonstrating core state machine features.
/// </summary>
public class SimpleOrderProcessingGrain : TimerEnabledStateMachineGrain<SimpleOrderProcessingGrain.OrderState, SimpleOrderProcessingGrain.OrderTrigger, OrderProcessingState>, IOrderProcessingGrain
{
    public enum OrderState
    {
        Draft,
        PendingPayment,
        PaymentProcessing,
        PaymentConfirmed,
        Shipped,
        Delivered,
        Completed,
        Cancelled
    }

    public enum OrderTrigger
    {
        SubmitOrder,
        ProcessPayment,
        PaymentSucceeded,
        PaymentFailed,
        Ship,
        MarkDelivered,
        CompleteOrder,
        CancelOrder,
        PaymentTimeout
    }

    private readonly ILogger<SimpleOrderProcessingGrain> _logger;

    public SimpleOrderProcessingGrain(ILogger<SimpleOrderProcessingGrain> logger)
    {
        _logger = logger;
    }

    protected override StateMachine<OrderState, OrderTrigger> BuildStateMachine()
    {
        var config = new StateMachine<OrderState, OrderTrigger>(OrderState.Draft);

        config.Configure(OrderState.Draft)
            .Permit(OrderTrigger.SubmitOrder, OrderState.PendingPayment)
            .OnEntry(() => _logger.LogInformation("Order draft created"));

        config.Configure(OrderState.PendingPayment)
            .Permit(OrderTrigger.ProcessPayment, OrderState.PaymentProcessing)
            .Permit(OrderTrigger.CancelOrder, OrderState.Cancelled)
            .Permit(OrderTrigger.PaymentTimeout, OrderState.Cancelled)
            .OnEntry(() => _logger.LogInformation("Order pending payment"));

        config.Configure(OrderState.PaymentProcessing)
            .Permit(OrderTrigger.PaymentSucceeded, OrderState.PaymentConfirmed)
            .Permit(OrderTrigger.PaymentFailed, OrderState.Cancelled)
            .OnEntry(() => _logger.LogInformation("Processing payment"));

        config.Configure(OrderState.PaymentConfirmed)
            .Permit(OrderTrigger.Ship, OrderState.Shipped)
            .OnEntry(() => _logger.LogInformation("Payment confirmed"));

        config.Configure(OrderState.Shipped)
            .Permit(OrderTrigger.MarkDelivered, OrderState.Delivered)
            .OnEntry(() => _logger.LogInformation("Order shipped"));

        config.Configure(OrderState.Delivered)
            .Permit(OrderTrigger.CompleteOrder, OrderState.Completed)
            .OnEntry(() => _logger.LogInformation("Order delivered"));

        config.Configure(OrderState.Completed)
            .OnEntry(() => _logger.LogInformation("Order completed"));

        config.Configure(OrderState.Cancelled)
            .OnEntry(() => _logger.LogInformation("Order cancelled"));

        return config;
    }

    protected override void ConfigureTimeouts()
    {
        base.ConfigureTimeouts();

        // Configure payment timeout for PendingPayment state
        RegisterStateTimeout(OrderState.PendingPayment,
            ConfigureTimeout(OrderState.PendingPayment)
                .After(TimeSpan.FromMinutes(15))
                .TransitionTo(OrderTrigger.PaymentTimeout)
                .Build());
    }

    // Public interface methods

    public async Task<string> SubmitOrderAsync(OrderSubmissionRequest request)
    {
        return await TracingHelper.TraceStateTransition(
            nameof(SimpleOrderProcessingGrain),
            this.GetPrimaryKeyString(),
            State.CurrentState.ToString(),
            OrderTrigger.SubmitOrder.ToString(),
            async () =>
            {
                await FireAsync(OrderTrigger.SubmitOrder);
                return "Order submitted successfully";
            });
    }

    public async Task<string> ProcessPaymentAsync(PaymentRequest paymentRequest)
    {
        return await TracingHelper.TraceStateTransition(
            nameof(SimpleOrderProcessingGrain),
            this.GetPrimaryKeyString(),
            State.CurrentState.ToString(),
            OrderTrigger.ProcessPayment.ToString(),
            async () =>
            {
                await FireAsync(OrderTrigger.ProcessPayment);
                // Simulate payment processing result
                await Task.Delay(100);
                await FireAsync(OrderTrigger.PaymentSucceeded);
                return "Payment processing completed";
            });
    }

    public async Task<string> MarkPackageCompleteAsync()
    {
        return await TracingHelper.TraceStateTransition(
            nameof(SimpleOrderProcessingGrain),
            this.GetPrimaryKeyString(),
            State.CurrentState.ToString(),
            OrderTrigger.Ship.ToString(),
            async () =>
            {
                await FireAsync(OrderTrigger.Ship);
                return "Order shipped";
            });
    }

    public async Task<string> MarkDeliveredAsync()
    {
        return await TracingHelper.TraceStateTransition(
            nameof(SimpleOrderProcessingGrain),
            this.GetPrimaryKeyString(),
            State.CurrentState.ToString(),
            OrderTrigger.MarkDelivered.ToString(),
            async () =>
            {
                await FireAsync(OrderTrigger.MarkDelivered);
                return "Order delivered";
            });
    }

    public async Task<string> CancelOrderAsync(string reason)
    {
        return await TracingHelper.TraceStateTransition(
            nameof(SimpleOrderProcessingGrain),
            this.GetPrimaryKeyString(),
            State.CurrentState.ToString(),
            OrderTrigger.CancelOrder.ToString(),
            async () =>
            {
                await FireAsync(OrderTrigger.CancelOrder);
                return "Order cancelled";
            });
    }

    public Task<OrderState> GetCurrentStateAsync() => Task.FromResult(State.CurrentState);

    public async Task<List<string>> GetValidTransitionsAsync()
    {
        var permittedTriggers = await GetPermittedTriggersAsync();
        return permittedTriggers.Select(t => t.ToString()).ToList();
    }

    public async Task<OrderStatusInfo> GetOrderStatusAsync()
    {
        return new OrderStatusInfo
        {
            OrderId = this.GetPrimaryKeyString(),
            CurrentState = State.CurrentState,
            ValidTransitions = await GetValidTransitionsAsync(),
            EventHistory = new List<string> { "Order created" },
            LastUpdated = DateTime.UtcNow
        };
    }
}

// Supporting data models
public class OrderSubmissionRequest
{
    public string CustomerId { get; set; } = string.Empty;
    public List<OrderItem> Items { get; set; } = new();
    public string ShippingAddress { get; set; } = string.Empty;
    public string BillingAddress { get; set; } = string.Empty;
}

public class OrderItem
{
    public string ProductId { get; set; } = string.Empty;
    public int Quantity { get; set; }
    public decimal Price { get; set; }
}

public class PaymentRequest
{
    public string PaymentMethodId { get; set; } = string.Empty;
    public decimal Amount { get; set; }
    public string Currency { get; set; } = "USD";
}

public class OrderStatusInfo
{
    public string OrderId { get; set; } = string.Empty;
    public SimpleOrderProcessingGrain.OrderState CurrentState { get; set; }
    public List<string> ValidTransitions { get; set; } = new();
    public List<string> EventHistory { get; set; } = new();
    public DateTime LastUpdated { get; set; }
}

[GenerateSerializer]
[Alias("Orleans.StateMachineES.Examples.ECommerceWorkflow.OrderProcessingState")]
public class OrderProcessingState : TimerEnabledStateMachineState<SimpleOrderProcessingGrain.OrderState>
{
}