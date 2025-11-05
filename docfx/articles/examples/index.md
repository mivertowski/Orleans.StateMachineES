# Examples

Production-ready example applications demonstrating Orleans.StateMachineES features.

## Available Examples

### [E-Commerce Workflow](ecommerce.md)
Complete order processing system with event sourcing, timers, and monitoring.

**Features:**
- Event-sourced order state machine
- Payment integration
- Inventory management
- Automatic timeouts for abandoned orders
- Email notifications
- Distributed tracing

**Complexity:** Intermediate
**Source:** `examples/ECommerceWorkflow/`

### [Document Approval](document-approval.md)
Multi-level approval workflow with hierarchical states and distributed sagas.

**Features:**
- Hierarchical state machine (Draft → Review → Approval)
- Multi-stakeholder approval process
- Saga orchestration for cross-system updates
- Compensation logic for rollbacks
- Audit trail

**Complexity:** Advanced
**Source:** `examples/DocumentApproval/`

### [Monitoring Dashboard](monitoring.md)
Health checks, metrics collection, and observability patterns.

**Features:**
- ASP.NET Core health checks integration
- OpenTelemetry distributed tracing
- State machine visualization
- Real-time metrics with Prometheus
- Dashboard UI with state diagrams

**Complexity:** Intermediate
**Source:** `examples/MonitoringDashboard/`

### [Smart Home Automation](smart-home.md)
Source generator demonstration with orthogonal regions.

**Features:**
- YAML/JSON state machine definitions
- Roslyn source generator usage
- Orthogonal regions (Security, Climate, Energy, Presence)
- Cross-region synchronization
- Vacation mode automation

**Complexity:** Advanced
**Source:** `examples/SmartHome/`

### [Performance Showcase](performance-showcase.md)
Benchmarks and performance optimization demonstrations.

**Features:**
- TriggerParameterCache benchmarks (~100x speedup)
- Event sourcing vs regular grain performance
- ObjectPool optimization
- FrozenCollections usage
- ValueTask patterns

**Complexity:** Beginner
**Source:** `examples/PerformanceShowcase/`

## Running the Examples

Each example is a complete, runnable application. To run any example:

```bash
cd examples/<ExampleName>
dotnet restore
dotnet run
```

## Example Structure

All examples follow a consistent structure:

```
ExampleName/
├── ExampleName.csproj          # Project file
├── README.md                   # Example-specific documentation
├── Program.cs                  # Silo configuration
├── Grains/                     # State machine grain implementations
├── Interfaces/                 # Grain interfaces
├── Models/                     # State, trigger, and data models
├── Configuration/              # Example-specific configuration
└── Tests/                      # Unit and integration tests
```

## Learning Path

### Beginner: Start Here
1. **[Performance Showcase](performance-showcase.md)** - Basic patterns and performance benefits
2. **[E-Commerce Workflow](ecommerce.md)** - Event sourcing and timers

### Intermediate: Level Up
3. **[Monitoring Dashboard](monitoring.md)** - Observability and health checks
4. **[Document Approval](document-approval.md)** - Hierarchical states

### Advanced: Master the Library
5. **[Smart Home Automation](smart-home.md)** - Source generator and orthogonal regions

## Common Patterns Demonstrated

### Event Sourcing
See: [E-Commerce Workflow](ecommerce.md), [Document Approval](document-approval.md)

```csharp
public class OrderGrain : EventSourcedStateMachineGrain<OrderState, OrderTrigger, OrderGrainState>
{
    protected override void ConfigureEventSourcing(EventSourcingOptions options)
    {
        options.AutoConfirmEvents = true;  // Essential!
        options.EnableSnapshots = true;
        options.SnapshotInterval = 100;
    }
}
```

### Timers & Timeouts
See: [E-Commerce Workflow](ecommerce.md)

```csharp
RegisterStateTimeout(OrderState.Pending,
    ConfigureTimeout(OrderState.Pending)
        .After(TimeSpan.FromHours(24))
        .TransitionTo(OrderTrigger.Cancel)
        .UseReminder()
        .Build());
```

### Hierarchical States
See: [Document Approval](document-approval.md)

```csharp
machine.Configure(DocumentState.InReview)
    .SubstateOf(DocumentState.Active)
    .Permit(DocumentTrigger.Approve, DocumentState.Approved);
```

### Distributed Sagas
See: [Document Approval](document-approval.md)

```csharp
public class DocumentPublishingSaga : SagaOrchestratorGrain<DocumentData>
{
    protected override void ConfigureSagaSteps()
    {
        AddStep(new PublishToWebStep()).WithRetry(3);
        AddStep(new NotifySubscribersStep()).WithRetry(2);
        AddStep(new ArchiveOriginalStep()).WithRetry(1);
    }
}
```

### Orthogonal Regions
See: [Smart Home Automation](smart-home.md)

```csharp
DefineOrthogonalRegion("Security", SecurityState.Disarmed, machine => { ... });
DefineOrthogonalRegion("Climate", ClimateState.Off, machine => { ... });
DefineOrthogonalRegion("Energy", EnergyState.Normal, machine => { ... });
```

### Source Generator
See: [Smart Home Automation](smart-home.md)

**SmartLight.statemachine.yaml:**
```yaml
name: SmartLight
states: [Off, On, Dimmed]
triggers: [TurnOn, TurnOff, Dim]
transitions:
  - { from: Off, to: On, trigger: TurnOn }
  - { from: On, to: Dimmed, trigger: Dim }
```

## Testing Examples

Each example includes comprehensive tests. Run tests with:

```bash
cd examples/<ExampleName>
dotnet test
```

### Test Coverage
- Unit tests for state transitions
- Integration tests with Orleans TestCluster
- Guard condition validation
- Timeout and reminder behavior
- Event sourcing replay scenarios

## Configuration Patterns

### Storage Configuration
```csharp
siloBuilder
    .AddMemoryGrainStorage("Default")
    .AddLogStorageBasedLogConsistencyProvider("LogStorage")
    .AddMemoryGrainStorage("PubSubStore");
```

### Stream Configuration
```csharp
siloBuilder
    .AddStreams("SMS")
    .AddMemoryStreams("SMS");
```

### Tracing Configuration
```csharp
services.AddOpenTelemetry()
    .WithTracing(builder => builder
        .AddSource("Orleans.StateMachineES")
        .AddJaegerExporter());
```

## Troubleshooting Examples

### Build Issues
```bash
# Clean and restore
dotnet clean
dotnet restore
dotnet build
```

### Runtime Issues
Check the example README for:
- Required dependencies
- Configuration settings
- Known limitations
- Common errors

## Contributing Examples

Have a great example? Contributions are welcome!

1. Follow the standard example structure
2. Include comprehensive README
3. Add unit and integration tests
4. Document key concepts used
5. Submit a pull request

See [Contributing Guide](../reference/contributing.md) for details.

## Additional Resources

- [Getting Started Tutorial](../getting-started/index.md)
- [Guides](../guides/index.md)
- [API Reference](../../api/index.md)
- [Troubleshooting](../reference/troubleshooting.md)
