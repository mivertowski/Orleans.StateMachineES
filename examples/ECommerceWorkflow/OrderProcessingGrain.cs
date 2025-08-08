using Orleans.StateMachineES.Timers;
using Orleans.StateMachineES.Tracing;
using Stateless;

namespace Orleans.StateMachineES.Examples.ECommerceWorkflow;

/// <summary>
/// Comprehensive order processing grain demonstrating all state machine features:
/// - Event sourcing for state persistence
/// - Timers for automatic state transitions
/// - Version management for evolving business rules
/// - Distributed tracing for observability
/// - Health monitoring integration
/// </summary>
public class OrderProcessingGrain : TimerEnabledStateMachineGrain<OrderState, OrderTrigger>, IOrderProcessingGrain
{
    /// <summary>
    /// Order states representing the complete e-commerce workflow
    /// </summary>
    public enum OrderState
    {
        Draft,
        PendingPayment,
        PaymentProcessing,
        PaymentConfirmed,
        InventoryReserved,
        Packaging,
        Shipped,
        InTransit,
        Delivered,
        Completed,
        
        // Error/cancellation states
        PaymentFailed,
        PaymentDeclined,
        InventoryUnavailable,
        ShippingFailed,
        Cancelled,
        Refunded
    }

    /// <summary>
    /// Order triggers representing business events and actions
    /// </summary>
    public enum OrderTrigger
    {
        SubmitOrder,
        ProcessPayment,
        PaymentSucceeded,
        PaymentFailed,
        PaymentDeclined,
        ReserveInventory,
        InventoryReserved,
        InventoryUnavailable,
        StartPackaging,
        PackageComplete,
        Ship,
        UpdateTracking,
        MarkDelivered,
        CompleteOrder,
        CancelOrder,
        InitiateRefund,
        RefundProcessed,
        
        // Timer triggers
        PaymentTimeout,
        InventoryTimeout,
        PackagingTimeout,
        ShippingTimeout
    }

    private readonly ILogger<OrderProcessingGrain> _logger;
    private readonly IOrderProcessingService _orderService;
    private readonly IPaymentService _paymentService;
    private readonly IInventoryService _inventoryService;
    private readonly IShippingService _shippingService;

    public OrderProcessingGrain(
        ILogger<OrderProcessingGrain> logger,
        IOrderProcessingService orderService,
        IPaymentService paymentService,
        IInventoryService inventoryService,
        IShippingService shippingService)
    {
        _logger = logger;
        _orderService = orderService;
        _paymentService = paymentService;
        _inventoryService = inventoryService;
        _shippingService = shippingService;
    }

    /// <summary>
    /// Configure the complete order processing state machine with all transitions, guards, and actions
    /// </summary>
    protected override StateMachine<OrderState, OrderTrigger> BuildStateMachine()
    {
        var config = new StateMachine<OrderState, OrderTrigger>(OrderState.Draft);

        // Configure main order flow
        ConfigureOrderSubmissionFlow(config);
        ConfigurePaymentFlow(config);
        ConfigureInventoryFlow(config);
        ConfigureFulfillmentFlow(config);
        ConfigureDeliveryFlow(config);
        ConfigureCancellationFlow(config);
        ConfigureRefundFlow(config);

        // Configure timers for automatic transitions
        ConfigureTimers();

        return config;
    }

    private void ConfigureOrderSubmissionFlow(StateMachine<OrderState, OrderTrigger> config)
    {
        config.Configure(OrderState.Draft)
            .Permit(OrderTrigger.SubmitOrder, OrderState.PendingPayment)
            .OnEntry(() => _logger.LogInformation("Order draft created"))
            .OnExit(() => _logger.LogInformation("Order submitted"));

        config.Configure(OrderState.PendingPayment)
            .Permit(OrderTrigger.ProcessPayment, OrderState.PaymentProcessing)
            .Permit(OrderTrigger.CancelOrder, OrderState.Cancelled)
            .Permit(OrderTrigger.PaymentTimeout, OrderState.Cancelled)
            .OnEntry(async () =>
            {
                _logger.LogInformation("Order pending payment");
                // Set payment timeout timer (15 minutes)  
                await SetTimerAsync("PaymentTimeout", TimeSpan.FromMinutes(15), OrderTrigger.PaymentTimeout);
            })
            .OnExit(() => ClearTimer("PaymentTimeout"));
    }

    private void ConfigurePaymentFlow(StateMachine<OrderState, OrderTrigger> config)
    {
        config.Configure(OrderState.PaymentProcessing)
            .Permit(OrderTrigger.PaymentSucceeded, OrderState.PaymentConfirmed)
            .Permit(OrderTrigger.PaymentFailed, OrderState.PaymentFailed)
            .Permit(OrderTrigger.PaymentDeclined, OrderState.PaymentDeclined)
            .OnEntry(async () =>
            {
                await RecordEventAsync("PaymentProcessingStarted");
                await ProcessPaymentAsync();
            });

        config.Configure(OrderState.PaymentConfirmed)
            .Permit(OrderTrigger.ReserveInventory, OrderState.InventoryReserved)
            .OnEntry(async () =>
            {
                await RecordEventAsync("PaymentConfirmed");
                // Automatically move to inventory reservation
                await FireAsync(OrderTrigger.ReserveInventory);
            });

        config.Configure(OrderState.PaymentFailed)
            .Permit(OrderTrigger.ProcessPayment, OrderState.PaymentProcessing)
            .Permit(OrderTrigger.CancelOrder, OrderState.Cancelled)
            .OnEntry(() => RecordEvent("PaymentFailed"));

        config.Configure(OrderState.PaymentDeclined)
            .Permit(OrderTrigger.CancelOrder, OrderState.Cancelled)
            .OnEntry(() => RecordEvent("PaymentDeclined"));
    }

    private void ConfigureInventoryFlow(StateMachine<OrderState, OrderTrigger> config)
    {
        config.Configure(OrderState.InventoryReserved)
            .PermitIf(OrderTrigger.InventoryReserved, OrderState.Packaging, () => IsInventoryAvailable())
            .Permit(OrderTrigger.InventoryUnavailable, OrderState.InventoryUnavailable)
            .Permit(OrderTrigger.InventoryTimeout, OrderState.InventoryUnavailable)
            .OnEntry(async () =>
            {
                await RecordEventAsync("InventoryReservationStarted");
                // Set inventory timeout (5 minutes)
                await SetTimerAsync("InventoryTimeout", TimeSpan.FromMinutes(5), OrderTrigger.InventoryTimeout);
                await ReserveInventoryAsync();
            })
            .OnExit(() => ClearTimer("InventoryTimeout"));

        config.Configure(OrderState.InventoryUnavailable)
            .Permit(OrderTrigger.InitiateRefund, OrderState.Refunded)
            .OnEntry(async () =>
            {
                await RecordEventAsync("InventoryUnavailable");
                // Automatically initiate refund
                await FireAsync(OrderTrigger.InitiateRefund);
            });
    }

    private void ConfigureFulfillmentFlow(StateMachine<OrderState, OrderTrigger> config)
    {
        config.Configure(OrderState.Packaging)
            .Permit(OrderTrigger.PackageComplete, OrderState.Shipped)
            .Permit(OrderTrigger.PackagingTimeout, OrderState.ShippingFailed)
            .OnEntry(async () =>
            {
                await RecordEventAsync("PackagingStarted");
                // Set packaging timeout (2 hours)
                await SetTimerAsync("PackagingTimeout", TimeSpan.FromHours(2), OrderTrigger.PackagingTimeout);
                await StartPackagingAsync();
            })
            .OnExit(() => ClearTimer("PackagingTimeout"));

        config.Configure(OrderState.Shipped)
            .Permit(OrderTrigger.UpdateTracking, OrderState.InTransit)
            .OnEntry(async () =>
            {
                await RecordEventAsync("OrderShipped");
                await CreateShippingLabelAsync();
                // Automatically update to in-transit
                await FireAsync(OrderTrigger.UpdateTracking);
            });
    }

    private void ConfigureDeliveryFlow(StateMachine<OrderState, OrderTrigger> config)
    {
        config.Configure(OrderState.InTransit)
            .Permit(OrderTrigger.MarkDelivered, OrderState.Delivered)
            .Permit(OrderTrigger.ShippingTimeout, OrderState.ShippingFailed)
            .OnEntry(async () =>
            {
                await RecordEventAsync("OrderInTransit");
                // Set shipping timeout (7 days)
                await SetTimerAsync("ShippingTimeout", TimeSpan.FromDays(7), OrderTrigger.ShippingTimeout);
            })
            .OnExit(() => ClearTimer("ShippingTimeout"));

        config.Configure(OrderState.Delivered)
            .Permit(OrderTrigger.CompleteOrder, OrderState.Completed)
            .OnEntry(async () =>
            {
                await RecordEventAsync("OrderDelivered");
                // Set completion timer (24 hours for customer feedback)
                await SetTimerAsync("CompletionTimeout", TimeSpan.FromHours(24), OrderTrigger.CompleteOrder);
            });

        config.Configure(OrderState.Completed)
            .OnEntry(() => RecordEvent("OrderCompleted"));
    }

    private void ConfigureCancellationFlow(StateMachine<OrderState, OrderTrigger> config)
    {
        // Allow cancellation from early states
        config.Configure(OrderState.Cancelled)
            .OnEntry(() => RecordEvent("OrderCancelled"));
    }

    private void ConfigureRefundFlow(StateMachine<OrderState, OrderTrigger> config)
    {
        config.Configure(OrderState.Refunded)
            .OnEntry(async () =>
            {
                await RecordEventAsync("RefundProcessed");
                await ProcessRefundAsync();
            });
    }

    private void ConfigureTimers()
    {
        // Configure timer definitions for automatic state transitions
        ConfigureTimer("PaymentTimeout", TimeSpan.FromMinutes(15), OrderTrigger.PaymentTimeout);
        ConfigureTimer("InventoryTimeout", TimeSpan.FromMinutes(5), OrderTrigger.InventoryTimeout);
        ConfigureTimer("PackagingTimeout", TimeSpan.FromHours(2), OrderTrigger.PackagingTimeout);
        ConfigureTimer("ShippingTimeout", TimeSpan.FromDays(7), OrderTrigger.ShippingTimeout);
        ConfigureTimer("CompletionTimeout", TimeSpan.FromHours(24), OrderTrigger.CompleteOrder);
    }

    // Business logic methods with distributed tracing

    private async Task ProcessPaymentAsync()
    {
        using var activity = TracingHelper.StartChildActivity("ProcessPayment", 
            nameof(OrderProcessingGrain), this.GetPrimaryKeyString());

        try
        {
            var paymentResult = await _paymentService.ProcessPaymentAsync(GetOrderDetails());
            
            if (paymentResult.IsSuccess)
            {
                await FireAsync(OrderTrigger.PaymentSucceeded);
                activity?.SetTag("payment.success", true);
            }
            else if (paymentResult.IsDeclined)
            {
                await FireAsync(OrderTrigger.PaymentDeclined);
                activity?.SetTag("payment.declined", true);
            }
            else
            {
                await FireAsync(OrderTrigger.PaymentFailed);
                activity?.SetTag("payment.failed", true);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Payment processing failed for order {OrderId}", this.GetPrimaryKeyString());
            await FireAsync(OrderTrigger.PaymentFailed);
            throw;
        }
    }

    private async Task ReserveInventoryAsync()
    {
        using var activity = TracingHelper.StartChildActivity("ReserveInventory", 
            nameof(OrderProcessingGrain), this.GetPrimaryKeyString());

        try
        {
            var inventoryResult = await _inventoryService.ReserveItemsAsync(GetOrderDetails());
            
            if (inventoryResult.IsAvailable)
            {
                await FireAsync(OrderTrigger.InventoryReserved);
                activity?.SetTag("inventory.reserved", true);
            }
            else
            {
                await FireAsync(OrderTrigger.InventoryUnavailable);
                activity?.SetTag("inventory.unavailable", true);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Inventory reservation failed for order {OrderId}", this.GetPrimaryKeyString());
            await FireAsync(OrderTrigger.InventoryUnavailable);
            throw;
        }
    }

    private async Task StartPackagingAsync()
    {
        using var activity = TracingHelper.StartChildActivity("StartPackaging", 
            nameof(OrderProcessingGrain), this.GetPrimaryKeyString());

        try
        {
            await _orderService.StartPackagingAsync(GetOrderDetails());
            // Packaging completion would be triggered externally
            activity?.SetTag("packaging.started", true);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Packaging initiation failed for order {OrderId}", this.GetPrimaryKeyString());
            throw;
        }
    }

    private async Task CreateShippingLabelAsync()
    {
        using var activity = TracingHelper.StartChildActivity("CreateShippingLabel", 
            nameof(OrderProcessingGrain), this.GetPrimaryKeyString());

        try
        {
            var trackingInfo = await _shippingService.CreateShippingLabelAsync(GetOrderDetails());
            await RecordEventAsync("ShippingLabelCreated", new { TrackingNumber = trackingInfo.TrackingNumber });
            activity?.SetTag("tracking.number", trackingInfo.TrackingNumber);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Shipping label creation failed for order {OrderId}", this.GetPrimaryKeyString());
            throw;
        }
    }

    private async Task ProcessRefundAsync()
    {
        using var activity = TracingHelper.StartChildActivity("ProcessRefund", 
            nameof(OrderProcessingGrain), this.GetPrimaryKeyString());

        try
        {
            await _paymentService.ProcessRefundAsync(GetOrderDetails());
            activity?.SetTag("refund.processed", true);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Refund processing failed for order {OrderId}", this.GetPrimaryKeyString());
            throw;
        }
    }

    // Helper methods

    private bool IsInventoryAvailable()
    {
        // This would typically check current inventory levels
        return true; // Simplified for example
    }

    private OrderDetails GetOrderDetails()
    {
        // This would retrieve full order details from state/storage
        return new OrderDetails
        {
            OrderId = this.GetPrimaryKeyString(),
            CustomerId = "customer-123", // Would be extracted from order data
            Items = new List<OrderItem>(), // Would be populated from order state
            Amount = 100.00m // Would be calculated from items
        };
    }

    // Public interface methods

    public async Task<string> SubmitOrderAsync(OrderSubmissionRequest request)
    {
        return await TracingHelper.TraceStateTransition(
            nameof(OrderProcessingGrain),
            this.GetPrimaryKeyString(),
            State.ToString(),
            OrderTrigger.SubmitOrder.ToString(),
            async () =>
            {
                // Store order details in state
                await RecordEventAsync("OrderSubmissionRequested", request);
                await FireAsync(OrderTrigger.SubmitOrder);
                return "Order submitted successfully";
            });
    }

    public async Task<string> ProcessPaymentAsync(PaymentRequest paymentRequest)
    {
        return await TracingHelper.TraceStateTransition(
            nameof(OrderProcessingGrain),
            this.GetPrimaryKeyString(),
            State.ToString(),
            OrderTrigger.ProcessPayment.ToString(),
            async () =>
            {
                await RecordEventAsync("PaymentProcessingRequested", paymentRequest);
                await FireAsync(OrderTrigger.ProcessPayment);
                return "Payment processing initiated";
            });
    }

    public async Task<string> MarkPackageCompleteAsync()
    {
        return await TracingHelper.TraceStateTransition(
            nameof(OrderProcessingGrain),
            this.GetPrimaryKeyString(),
            State.ToString(),
            OrderTrigger.PackageComplete.ToString(),
            async () =>
            {
                await FireAsync(OrderTrigger.PackageComplete);
                return "Package marked as complete";
            });
    }

    public async Task<string> MarkDeliveredAsync()
    {
        return await TracingHelper.TraceStateTransition(
            nameof(OrderProcessingGrain),
            this.GetPrimaryKeyString(),
            State.ToString(),
            OrderTrigger.MarkDelivered.ToString(),
            async () =>
            {
                await FireAsync(OrderTrigger.MarkDelivered);
                return "Order marked as delivered";
            });
    }

    public async Task<string> CancelOrderAsync(string reason)
    {
        return await TracingHelper.TraceStateTransition(
            nameof(OrderProcessingGrain),
            this.GetPrimaryKeyString(),
            State.ToString(),
            OrderTrigger.CancelOrder.ToString(),
            async () =>
            {
                await RecordEventAsync("OrderCancellationRequested", new { Reason = reason });
                await FireAsync(OrderTrigger.CancelOrder);
                return "Order cancellation initiated";
            });
    }

    public Task<OrderState> GetCurrentStateAsync() => Task.FromResult(State);

    public async Task<List<string>> GetValidTransitionsAsync()
    {
        var permittedTriggers = await GetPermittedTriggersAsync();
        return permittedTriggers.Select(t => t.ToString()).ToList();
    }

    public async Task<OrderStatusInfo> GetOrderStatusAsync()
    {
        var events = await GetEventsAsync();
        return new OrderStatusInfo
        {
            OrderId = this.GetPrimaryKeyString(),
            CurrentState = State,
            ValidTransitions = await GetValidTransitionsAsync(),
            EventHistory = events.Select(e => e.EventType).ToList(),
            LastUpdated = DateTime.UtcNow
        };
    }

    protected override string GetVersion() => "1.0.0";
}

// Supporting data models
public class OrderDetails
{
    public string OrderId { get; set; } = string.Empty;
    public string CustomerId { get; set; } = string.Empty;
    public List<OrderItem> Items { get; set; } = new();
    public decimal Amount { get; set; }
}

public class OrderItem
{
    public string ProductId { get; set; } = string.Empty;
    public int Quantity { get; set; }
    public decimal Price { get; set; }
}

public class OrderSubmissionRequest
{
    public string CustomerId { get; set; } = string.Empty;
    public List<OrderItem> Items { get; set; } = new();
    public string ShippingAddress { get; set; } = string.Empty;
    public string BillingAddress { get; set; } = string.Empty;
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
    public OrderProcessingGrain.OrderState CurrentState { get; set; }
    public List<string> ValidTransitions { get; set; } = new();
    public List<string> EventHistory { get; set; } = new();
    public DateTime LastUpdated { get; set; }
}

// Service interfaces (would be implemented by actual business logic)
public interface IOrderProcessingService
{
    Task StartPackagingAsync(OrderDetails orderDetails);
}

public interface IPaymentService
{
    Task<PaymentResult> ProcessPaymentAsync(OrderDetails orderDetails);
    Task ProcessRefundAsync(OrderDetails orderDetails);
}

public interface IInventoryService
{
    Task<InventoryResult> ReserveItemsAsync(OrderDetails orderDetails);
}

public interface IShippingService
{
    Task<TrackingInfo> CreateShippingLabelAsync(OrderDetails orderDetails);
}

public class PaymentResult
{
    public bool IsSuccess { get; set; }
    public bool IsDeclined { get; set; }
    public string Message { get; set; } = string.Empty;
}

public class InventoryResult
{
    public bool IsAvailable { get; set; }
    public string Message { get; set; } = string.Empty;
}

public class TrackingInfo
{
    public string TrackingNumber { get; set; } = string.Empty;
    public string Carrier { get; set; } = string.Empty;
}