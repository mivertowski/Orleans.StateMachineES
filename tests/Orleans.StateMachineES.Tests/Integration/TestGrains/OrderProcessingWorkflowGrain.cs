using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Orleans;
using Orleans.StateMachineES.Tests.Integration;
using Stateless;

namespace Orleans.StateMachineES.Tests.Integration.TestGrains;

/// <summary>
/// Grain for testing complete order processing workflow.
/// </summary>
public class OrderProcessingWorkflowGrain : StateMachineGrain<OrderState, string>, IOrderProcessingWorkflowGrain
{
    private ILogger<OrderProcessingWorkflowGrain>? _logger;
    private OrderData? _orderData;
    private readonly List<OrderHistoryEntry> _orderHistory = new();
    
    protected override StateMachine<OrderState, string> BuildStateMachine()
    {
        var machine = new StateMachine<OrderState, string>(OrderState.Created);

        machine.Configure(OrderState.Created)
            .Permit("ProcessPayment", OrderState.PaymentPending)
            .OnEntry(() => LogOrderTransition(OrderState.Created, "Order created"));

        machine.Configure(OrderState.PaymentPending)
            .Permit("PaymentProcessed", OrderState.PaymentProcessed)
            .Permit("PaymentFailed", OrderState.Cancelled)
            .OnEntry(() => LogOrderTransition(OrderState.PaymentPending, "Payment processing started"));

        machine.Configure(OrderState.PaymentProcessed)
            .Permit("StartFulfillment", OrderState.Fulfilling)
            .OnEntry(() => LogOrderTransition(OrderState.PaymentProcessed, "Payment completed successfully"));

        machine.Configure(OrderState.Fulfilling)
            .Permit("ItemsShipped", OrderState.Shipped)
            .Permit("FulfillmentFailed", OrderState.Cancelled)
            .OnEntry(() => LogOrderTransition(OrderState.Fulfilling, "Order fulfillment started"));

        machine.Configure(OrderState.Shipped)
            .Permit("DeliveryConfirmed", OrderState.Completed)
            .OnEntry(() => LogOrderTransition(OrderState.Shipped, "Order shipped"));

        machine.Configure(OrderState.Completed)
            .OnEntry(() => LogOrderTransition(OrderState.Completed, "Order completed successfully"));

        machine.Configure(OrderState.Cancelled)
            .OnEntry(() => LogOrderTransition(OrderState.Cancelled, "Order was cancelled"));

        return machine;
    }

    public override async Task OnActivateAsync(CancellationToken cancellationToken)
    {
        await base.OnActivateAsync(cancellationToken);
        _logger = ServiceProvider.GetService(typeof(ILogger<OrderProcessingWorkflowGrain>)) as ILogger<OrderProcessingWorkflowGrain>;
    }

    public async Task CreateOrderAsync(OrderData data)
    {
        _orderData = data;
        await Task.CompletedTask;
        
        LogOrderTransition(OrderState.Created, $"Order {data.OrderId} created for customer {data.CustomerId}");
    }

    public async Task ProcessPaymentAsync(PaymentInfo payment)
    {
        if (await GetStateAsync() == OrderState.Created)
        {
            await FireAsync("ProcessPayment");
            
            // Simulate payment processing
            await Task.Delay(100);
            
            // For test purposes, always succeed unless payment method is "FailingCard"
            if (payment.PaymentMethod == "FailingCard")
            {
                await FireAsync("PaymentFailed");
                LogOrderTransition(await GetStateAsync(), $"Payment failed for transaction {payment.TransactionId}");
            }
            else
            {
                await FireAsync("PaymentProcessed");
                LogOrderTransition(await GetStateAsync(), $"Payment processed successfully: {payment.TransactionId}");
            }
        }
    }

    public async Task FulfillOrderAsync()
    {
        if (await GetStateAsync() == OrderState.PaymentProcessed)
        {
            await FireAsync("StartFulfillment");
            
            // Simulate fulfillment process
            await Task.Delay(50);
            
            LogOrderTransition(await GetStateAsync(), "Order fulfillment initiated");
        }
    }

    public async Task ShipOrderAsync(ShippingInfo shipping)
    {
        if (await GetStateAsync() == OrderState.Fulfilling)
        {
            await FireAsync("ItemsShipped");
            
            LogOrderTransition(await GetStateAsync(), 
                $"Order shipped via {shipping.Carrier}, tracking: {shipping.TrackingNumber}");
        }
    }

    public async Task CompleteOrderAsync()
    {
        if (await GetStateAsync() == OrderState.Shipped)
        {
            await FireAsync("DeliveryConfirmed");
            
            LogOrderTransition(await GetStateAsync(), "Order delivery confirmed and completed");
        }
    }

    public Task<OrderState> GetOrderStateAsync()
    {
        return GetStateAsync().AsTask();
    }

    public Task<List<OrderHistoryEntry>> GetOrderHistoryAsync()
    {
        return Task.FromResult(new List<OrderHistoryEntry>(_orderHistory));
    }

    private void LogOrderTransition(OrderState newState, string message)
    {
        var entry = new OrderHistoryEntry
        {
            Timestamp = DateTime.UtcNow,
            FromState = _orderHistory.Count > 0 ? _orderHistory[^1].ToState : OrderState.Created,
            ToState = newState,
            Trigger = message
        };
        
        _orderHistory.Add(entry);
        _logger?.LogInformation("Order {OrderId}: {Message}", _orderData?.OrderId ?? "Unknown", message);
    }
}