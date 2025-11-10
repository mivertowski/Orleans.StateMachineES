# Orleans.StateMachineES Examples

This directory contains example applications demonstrating key features of Orleans.StateMachineES. Each example provides practical implementation patterns for different use cases.

## Example Applications

### 1. E-Commerce Workflow

**Location**: `ECommerceWorkflow/`

**Features Demonstrated:**
- Timer-enabled state machines with automatic timeouts
- Event sourcing for complete transaction history
- Distributed tracing with OpenTelemetry
- Complex multi-stage order processing workflow

**Business Scenario:**

Complete e-commerce order processing from creation to delivery:
- Order submission and validation
- Payment processing with automatic timeout handling
- Fulfillment workflow coordination
- Delivery tracking and completion

**Key Components:**
- `SimpleOrderProcessingGrain.cs` - Timer-enabled state machine implementation
- `IOrderProcessingGrain.cs` - Grain interface

### 2. Document Approval Workflow

**Location**: `DocumentApproval/`

**Features Demonstrated:**
- Timer-enabled state machines with review deadlines
- Event sourcing for audit trail compliance
- Multi-stage approval workflow
- Distributed tracing integration

**Business Scenario:**

Enterprise document approval with sequential review stages:
- Multi-level review process (Technical, Legal, Managerial)
- Automatic timeout handling for review deadlines
- Complete approval audit trail
- Role-based approval routing

**Key Components:**
- `SimpleDocumentApprovalGrain.cs` - Timer-enabled state machine with approval workflow
- `IDocumentApprovalGrain.cs` - Grain interface

### 3. Monitoring Dashboard

**Location**: `MonitoringDashboard/`

**Features Demonstrated:**
- ASP.NET Core health checks integration
- Metrics collection and telemetry
- Distributed tracing with OpenTelemetry
- State machine visualization
- Real-time system monitoring

**Features:**
- Health checks UI endpoint
- Prometheus metrics support
- Interactive system dashboard
- Real-time visualization APIs
- Comprehensive logging and monitoring

**Key Components:**
- `Program.cs` - Complete monitoring application setup
- Custom health checks for Orleans grains
- Background monitoring service with alerting

### 4. Smart Home System (Advanced - Standalone)

**Location**: `SmartHome/` ⚠️ **Requires Additional Setup**

This advanced example demonstrates source-generated state machines and orthogonal regions. It requires the Roslyn source generator to be properly configured and is provided as a standalone example.

**Features:**
- State machines generated from YAML/JSON specifications
- Orthogonal regions (parallel independent state machines)
- Cross-region synchronization
- Device integration patterns

**Note**: This example is not included in the Examples.sln due to source generator dependencies. Refer to the SmartHome directory's documentation for setup instructions.

## Getting Started

### Prerequisites

- .NET 9.0 SDK or later
- Orleans 9.1.2 or later
- Visual Studio 2022, VS Code, or Rider

### Building the Examples

Build all examples using the solution file:

```bash
cd examples
dotnet build Examples.sln
```

All examples are configured as library projects demonstrating grain implementations. To run them, integrate the grains into an Orleans host application (see MonitoringDashboard for a complete runnable example).

### Running the Monitoring Dashboard Example

The MonitoringDashboard project is a complete runnable application:

```bash
cd examples/MonitoringDashboard
dotnet run

# Access the monitoring dashboard
# Open http://localhost:5000/dashboard in your browser

# View health checks
# Open http://localhost:5000/health-ui
```

## Architecture Patterns

### Timer-Enabled State Machines

```csharp
public class SimpleOrderProcessingGrain :
    TimerEnabledStateMachineGrain<OrderState, OrderTrigger, OrderProcessingState>
{
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
}
```

### Event Sourcing

All timer-enabled grains extend `EventSourcedStateMachineGrain`, providing automatic event sourcing capabilities:

```csharp
// State transitions are automatically recorded as events
await FireAsync(OrderTrigger.ProcessPayment);

// Complete event history is maintained
var events = await RetrieveConfirmedEvents(0, Version);
```

### Distributed Tracing

```csharp
public async Task<string> ProcessPaymentAsync(PaymentRequest request)
{
    return await TracingHelper.TraceStateTransition(
        nameof(SimpleOrderProcessingGrain),
        this.GetPrimaryKeyString(),
        State.CurrentState.ToString(),
        OrderTrigger.ProcessPayment.ToString(),
        async () =>
        {
            await FireAsync(OrderTrigger.ProcessPayment);
            await FireAsync(OrderTrigger.PaymentSucceeded);
            return "Payment processing completed";
        });
}
```

### Health Monitoring

```csharp
builder.Services
    .AddHealthChecks()
    .AddStateMachineHealthCheck("statemachine-system", options =>
    {
        options.DefaultTimeout = TimeSpan.FromSeconds(10);
        options.EnableCaching = true;
        options.CacheDuration = TimeSpan.FromMinutes(1);
    });
```

## Testing

The examples include integration with Orleans' testing infrastructure. To test state machine implementations:

```csharp
[Fact]
public async Task OrderProcessing_PaymentTimeout_TransitionsCorrectly()
{
    var grain = cluster.GrainFactory.GetGrain<IOrderProcessingGrain>("test-order");

    await grain.SubmitOrderAsync(new OrderSubmissionRequest { /* ... */ });

    // Timeout will automatically trigger after configured duration
    await Task.Delay(TimeSpan.FromMinutes(16));

    var state = await grain.GetCurrentStateAsync();
    Assert.Equal(OrderState.Cancelled, state);
}
```

## Additional Resources

- [Orleans.StateMachineES Documentation](../README.md)
- [Orleans Documentation](https://learn.microsoft.com/en-us/dotnet/orleans/)
- [Stateless State Machine Library](https://github.com/dotnet-state-machine/stateless)
- [OpenTelemetry .NET](https://github.com/open-telemetry/opentelemetry-dotnet)

## Contributing

Contributions and improvements to these examples are welcome. Please follow the project's contribution guidelines.

## License

This project is licensed under the MIT License. See the [LICENSE](../LICENSE) file for details.
