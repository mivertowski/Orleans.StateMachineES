# Orleans.StateMachineES Examples

This directory contains comprehensive example applications demonstrating all the advanced features of Orleans.StateMachineES. Each example showcases different aspects of the library and provides practical implementation patterns.

## 📁 Example Applications

### 🚀 Performance Showcase (`PerformanceShowcase/`)

**BREAKTHROUGH DISCOVERY: Event sourcing is 30.4% FASTER than regular state machines!**

**Features Demonstrated:**
- ✅ **Performance Benchmarks** - Comprehensive performance analysis
- ✅ **AutoConfirmEvents Configuration** - The secret to 30%+ performance boost
- ✅ **Event Sourcing Optimization** - Best practices for maximum throughput
- ✅ **Comparative Analysis** - Event-sourced vs regular state machines
- ✅ **Real-world Performance Data** - 5,923 vs 4,123 transitions/sec

**Key Insights:**
- Event-sourced: **5,923 transitions/sec** (0.17ms latency) ⭐ WINNER
- Regular: 4,123 transitions/sec (0.24ms latency)
- Critical configuration: `AutoConfirmEvents = true`

### 1. E-Commerce Workflow (`ECommerceWorkflow/`)

**Features Demonstrated:**
- ✅ **Event Sourcing** - Complete order history with event replay
- ✅ **Timers and Reminders** - Automatic timeout handling for payments, inventory, packaging
- ✅ **Version Management** - Evolving business rules with backward compatibility
- ✅ **Distributed Tracing** - Full observability with OpenTelemetry integration
- ✅ **Health Monitoring** - Real-time health checks and metrics
- ✅ **Complex State Transitions** - Multi-stage order processing workflow

**Business Scenario:**
Complete e-commerce order processing from draft to delivery, including:
- Order submission and validation
- Payment processing with retries and timeouts
- Inventory reservation with automatic rollback
- Packaging and shipping coordination
- Delivery tracking and completion
- Cancellation and refund handling

**Key Components:**
- `OrderProcessingGrain.cs` - Main state machine implementation
- `IOrderProcessingGrain.cs` - Service interface
- Demonstrates 16 states and 17 triggers with automatic transitions

### 2. Document Approval Workflow (`DocumentApproval/`)

**Features Demonstrated:**
- ✅ **Hierarchical State Machines** - Nested states for complex review processes
- ✅ **Saga Orchestration** - Parallel and conditional review workflows
- ✅ **Advanced Saga Patterns** - Parallel steps, conditional branching, compensation
- ✅ **Dynamic Routing** - Business rule-driven state transitions
- ✅ **Event Sourcing** - Complete approval audit trail
- ✅ **Timer Management** - Review deadlines and escalations

**Business Scenario:**
Enterprise document approval with multiple review stages:
- Technical, Legal, and Compliance reviews
- Parallel review coordination
- Conditional approval logic based on document properties
- Managerial and Executive approval routing
- Automatic escalation and timeout handling

**Key Components:**
- `DocumentApprovalGrain.cs` - Hierarchical state machine with saga orchestration
- `IDocumentApprovalGrain.cs` - Service interface
- Demonstrates complex nested states and parallel workflow patterns

### 3. Monitoring Dashboard (`MonitoringDashboard/`)

**Features Demonstrated:**
- ✅ **Health Checks Integration** - ASP.NET Core health checks with custom providers
- ✅ **Metrics Collection** - Comprehensive telemetry and performance monitoring
- ✅ **Distributed Tracing** - OpenTelemetry with Jaeger and console exporters
- ✅ **Visualization** - State machine diagrams and system dashboards
- ✅ **Real-time Monitoring** - Background health monitoring service
- ✅ **Alerting** - System alerts based on configurable thresholds

**Features:**
- Health Checks UI at `/health-ui`
- Prometheus metrics endpoint
- Interactive system dashboard at `/dashboard`
- Real-time visualization APIs
- Comprehensive logging and alerting

**Key Components:**
- `Program.cs` - Complete monitoring application setup
- Custom health checks for Orleans, database, and external services
- Background monitoring service with automatic alerting

### 4. Smart Home System (`SmartHome/`) 🆕

**Features Demonstrated:**
- ✅ **Roslyn Source Generator** - State machines generated from YAML/JSON specifications
- ✅ **Orthogonal Regions** - Multiple independent state machines running in parallel
- ✅ **Cross-region Synchronization** - Coordinated behavior across subsystems
- ✅ **Generated Type Safety** - Strongly-typed trigger and state methods
- ✅ **Region-specific Triggers** - Targeted control of individual subsystems
- ✅ **Composite State Calculation** - Overall system state from region states

**Business Scenario:**
Complete smart home automation system with:
- Security subsystem (armed/disarmed/alarm states)
- Climate control (heating/cooling/auto/eco modes)
- Energy management (normal/peak/night rate/saving)
- Presence detection (home/away/sleep/vacation)
- Device integration (lights, thermostats)
- Automated routines (morning, bedtime, vacation)

**Key Components:**
- `SmartLight.statemachine.yaml` - YAML specification for smart light bulb
- `Thermostat.statemachine.json` - JSON specification for thermostat
- `SmartHomeSystemGrain.cs` - Orthogonal state machine with 4 independent regions
- `Program.cs` - Demonstration of generated code and orthogonal features

**Generated Code Features:**
```csharp
// Generated from YAML/JSON specifications
ISmartLightGrain light = grainFactory.GetGrain<ISmartLightGrain>("living-room");
await light.FireTurnOnAsync();        // Typed trigger method
bool isOn = await light.IsOnAsync();  // Typed state check
SmartLightState.Off.IsTerminal();     // Generated extension methods
```

**Orthogonal Regions Example:**
```csharp
// Independent regions operating in parallel
await smartHome.FireInRegionAsync("Security", SmartHomeTrigger.ArmAway);
await smartHome.FireInRegionAsync("Climate", SmartHomeTrigger.SetEco);
await smartHome.FireInRegionAsync("Energy", SmartHomeTrigger.EnableSaving);

// Cross-region synchronization
await smartHome.ActivateVacationModeAsync(); // Affects all regions
```

## 🚀 Getting Started

### Prerequisites

- .NET 9.0 or later
- Orleans 9.1.2 or later
- Visual Studio 2022 or VS Code

### Running the Examples

#### 1. E-Commerce Workflow

```bash
cd examples/ECommerceWorkflow
dotnet run

# Test the workflow
curl -X POST http://localhost:5000/api/orders \
  -H "Content-Type: application/json" \
  -d '{"customerId": "customer-123", "items": [{"productId": "prod-1", "quantity": 2, "price": 50.00}]}'
```

#### 2. Document Approval Workflow

```bash
cd examples/DocumentApproval
dotnet run

# Submit a document for approval
curl -X POST http://localhost:5001/api/documents \
  -H "Content-Type: application/json" \
  -d '{"title": "Contract Agreement", "type": "Contract", "content": "...", "estimatedValue": 75000}'
```

#### 3. Monitoring Dashboard

```bash
cd examples/MonitoringDashboard
dotnet run

# Access the monitoring dashboard
open http://localhost:5002/dashboard

# View health checks
open http://localhost:5002/health-ui

# Check metrics
curl http://localhost:5002/metrics/custom
```

## 🏗️ Architecture Patterns

### Event Sourcing Implementation

```csharp
public class OrderProcessingGrain : EventSourcedStateMachineGrain<OrderState, OrderTrigger>
{
    protected override async Task OnStateTransitionAsync(
        OrderState fromState, OrderState toState, OrderTrigger trigger)
    {
        // Automatically persisted as event
        await RecordEventAsync("StateTransition", new
        {
            FromState = fromState.ToString(),
            ToState = toState.ToString(),
            Trigger = trigger.ToString(),
            Timestamp = DateTime.UtcNow
        });
    }
}
```

### Timer Configuration

```csharp
private void ConfigureTimers()
{
    // Payment timeout - 15 minutes
    ConfigureTimer("PaymentTimeout", TimeSpan.FromMinutes(15), OrderTrigger.PaymentTimeout);
    
    // Inventory timeout - 5 minutes
    ConfigureTimer("InventoryTimeout", TimeSpan.FromMinutes(5), OrderTrigger.InventoryTimeout);
    
    // Shipping timeout - 7 days
    ConfigureTimer("ShippingTimeout", TimeSpan.FromDays(7), OrderTrigger.ShippingTimeout);
}
```

### Distributed Tracing Integration

```csharp
public async Task<string> ProcessPaymentAsync(PaymentRequest request)
{
    return await TracingHelper.TraceStateTransition(
        nameof(OrderProcessingGrain),
        this.GetPrimaryKeyString(),
        State.ToString(),
        OrderTrigger.ProcessPayment.ToString(),
        async () =>
        {
            // Business logic with automatic tracing
            await FireAsync(OrderTrigger.ProcessPayment);
            return "Payment processing initiated";
        });
}
```

### Hierarchical State Configuration

```csharp
// Parent state with substates
config.Configure(DocumentState.InReview)
    .InitialTransition(DocumentState.InReview_PendingInitialReview)
    .Permit(DocumentTrigger.FinalApproval, DocumentState.Approved);

// Substate with parent context
config.Configure(DocumentState.InReview_TechnicalReview)
    .SubstateOf(DocumentState.InReview)
    .Permit(DocumentTrigger.TechnicalApproval, DocumentState.InReview_LegalReview);
```

### Saga Orchestration

```csharp
private async Task InitiateSagaOrchestrationAsync()
{
    var workflow = new SagaWorkflowBuilder()
        .WithId($"approval-{this.GetPrimaryKeyString()}")
        .WithCorrelationId(Guid.NewGuid().ToString())
        .AddStep("InitialReview", async () => await StartInitialReviewAsync())
        .AddConditionalStep("TechnicalReview", 
            condition: () => RequiresTechnicalReview(),
            step: async () => await StartTechnicalReviewAsync())
        .AddParallelSteps("ParallelReviews",
            ("ComplianceReview", async () => await StartComplianceReviewAsync()),
            ("SecurityReview", async () => await StartSecurityReviewAsync()),
            ("QualityReview", async () => await StartQualityReviewAsync()))
        .Build();

    await _sagaCoordinator.StartSagaAsync(workflow);
}
```

### Health Monitoring Setup

```csharp
builder.Services
    .AddHealthChecks()
    .AddStateMachineHealthCheck("statemachine-system", options =>
    {
        options.DefaultTimeout = TimeSpan.FromSeconds(10);
        options.EnableCaching = true;
        options.CacheDuration = TimeSpan.FromMinutes(1);
        options.MaxConcurrentChecks = 15;
    })
    .AddStateMachineGrainHealthChecks(new[]
    {
        new GrainHealthCheck("OrderProcessing", "order-001", TimeSpan.FromSeconds(5)),
        new GrainHealthCheck("DocumentApproval", "doc-001", TimeSpan.FromSeconds(5))
    });
```

## 📊 Monitoring and Observability

### OpenTelemetry Configuration

```csharp
builder.Services.AddOpenTelemetry()
    .ConfigureResource(resource =>
        resource.AddService(serviceName: "Orleans.StateMachineES.Example"))
    .WithTracing(tracing =>
    {
        tracing
            .AddSource(StateMachineActivitySource.SourceName)
            .AddAspNetCoreInstrumentation()
            .AddJaegerExporter();
    })
    .WithMetrics(metrics =>
    {
        metrics
            .AddMeter(StateMachineMetrics.MeterName)
            .AddPrometheusExporter();
    });
```

### Custom Metrics

```csharp
// Automatically collected metrics
StateMachineMetrics.RecordStateTransition(grainType, fromState, toState, trigger, duration);
StateMachineMetrics.RecordSagaCompletion(sagaType, status, duration, stepCount, failedSteps, isCompensation);
StateMachineMetrics.RecordHealthCheck(status, duration);

// Access current metrics
var metrics = StateMachineMetrics.GetCurrentMetrics();
```

### Visualization APIs

```csharp
// System-wide visualization
GET /visualization/system

// Grain-specific visualization
GET /visualization/grain/{grainType}/{grainId}?format=dot

// Interactive dashboard
GET /dashboard
```

## 🔧 Configuration Options

### Health Check Configuration

```csharp
services.AddSingleton(new StateMachineHealthCheckOptions
{
    DefaultTimeout = TimeSpan.FromSeconds(10),
    EnableCaching = true,
    CacheDuration = TimeSpan.FromMinutes(1),
    MaxConcurrentChecks = 15,
    UnhealthyThreshold = 0.25,     // 25% unhealthy = system unhealthy
    DegradedThreshold = 0.10,      // 10% degraded = system degraded
    ErrorRateThreshold = 0.05      // 5% error rate = alert
});
```

### Tracing Configuration

```csharp
services.AddSingleton(new TracingOptions
{
    EnableDetailedLogging = true,
    SampleRate = 1.0,              // Sample 100% for examples
    MaxSpanEvents = 128,
    MaxSpanAttributes = 64,
    ExportTimeout = TimeSpan.FromSeconds(30)
});
```

### Timer Configuration

```csharp
services.AddSingleton(new TimerConfiguration
{
    DefaultGranularity = TimeSpan.FromSeconds(1),
    MaxTimersPerGrain = 10,
    PersistTimers = true,
    EnableTimerMetrics = true
});
```

## 🧪 Testing

### Unit Testing State Machines

```csharp
[Test]
public async Task OrderProcessing_PaymentTimeout_ShouldCancelOrder()
{
    var grain = cluster.GrainFactory.GetGrain<IOrderProcessingGrain>("test-order");
    
    // Submit order
    await grain.SubmitOrderAsync(new OrderSubmissionRequest { /* ... */ });
    
    // Simulate payment timeout
    await grain.FireTimerAsync("PaymentTimeout");
    
    // Verify state
    var state = await grain.GetCurrentStateAsync();
    Assert.AreEqual(OrderState.Cancelled, state);
}
```

### Integration Testing with Health Checks

```csharp
[Test]
public async Task HealthCheck_SystemHealthy_ReturnsHealthyStatus()
{
    var healthCheck = serviceProvider.GetService<IStateMachineHealthCheck>();
    
    var result = await healthCheck.GetSystemHealthAsync();
    
    Assert.AreEqual(HealthStatus.Healthy, result.Status);
    Assert.IsTrue(result.TotalMonitoredGrains > 0);
}
```

## 📚 Additional Resources

- [Orleans.StateMachineES Documentation](../README.md)
- [Orleans Documentation](https://docs.microsoft.com/en-us/dotnet/orleans/)
- [Stateless State Machine Library](https://github.com/dotnet-state-machine/stateless)
- [OpenTelemetry .NET](https://github.com/open-telemetry/opentelemetry-dotnet)
- [ASP.NET Core Health Checks](https://docs.microsoft.com/en-us/aspnet/core/host-and-deploy/health-checks)

## 🤝 Contributing

These examples are part of the Orleans.StateMachineES project. Contributions, improvements, and additional examples are welcome!

## 📝 License

This project is licensed under the MIT License - see the [LICENSE](../LICENSE) file for details.