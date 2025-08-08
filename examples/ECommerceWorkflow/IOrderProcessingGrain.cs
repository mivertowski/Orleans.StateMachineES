using Orleans;
using Orleans.StateMachineES.Interfaces;
using static Orleans.StateMachineES.Examples.ECommerceWorkflow.SimpleOrderProcessingGrain;

namespace Orleans.StateMachineES.Examples.ECommerceWorkflow;

/// <summary>
/// Interface for the order processing grain that manages the complete e-commerce workflow.
/// Demonstrates integration with Orleans.StateMachineES features.
/// </summary>
public interface IOrderProcessingGrain : IStateMachineGrain<OrderState, OrderTrigger>
{
    /// <summary>
    /// Submits a new order and initiates the order processing workflow.
    /// </summary>
    /// <param name="request">The order submission details.</param>
    /// <returns>Confirmation message.</returns>
    Task<string> SubmitOrderAsync(OrderSubmissionRequest request);

    /// <summary>
    /// Initiates payment processing for the order.
    /// </summary>
    /// <param name="paymentRequest">Payment processing details.</param>
    /// <returns>Confirmation message.</returns>
    Task<string> ProcessPaymentAsync(PaymentRequest paymentRequest);

    /// <summary>
    /// Marks the order package as complete, ready for shipping.
    /// </summary>
    /// <returns>Confirmation message.</returns>
    Task<string> MarkPackageCompleteAsync();

    /// <summary>
    /// Marks the order as delivered to the customer.
    /// </summary>
    /// <returns>Confirmation message.</returns>
    Task<string> MarkDeliveredAsync();

    /// <summary>
    /// Cancels the order with a specified reason.
    /// </summary>
    /// <param name="reason">The reason for cancellation.</param>
    /// <returns>Confirmation message.</returns>
    Task<string> CancelOrderAsync(string reason);

    /// <summary>
    /// Gets the current state of the order.
    /// </summary>
    /// <returns>The current order state.</returns>
    Task<OrderState> GetCurrentStateAsync();

    /// <summary>
    /// Gets the list of valid transitions from the current state.
    /// </summary>
    /// <returns>List of valid trigger names.</returns>
    Task<List<string>> GetValidTransitionsAsync();

    /// <summary>
    /// Gets comprehensive status information about the order.
    /// </summary>
    /// <returns>Complete order status including history and available actions.</returns>
    Task<OrderStatusInfo> GetOrderStatusAsync();
}