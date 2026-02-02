# Orleans.StateMachineES

**Production-ready state machines for Microsoft Orleans**

Orleans.StateMachineES brings the power of state machines to your Orleans applications, wrapping the battle-tested [Stateless](https://github.com/dotnet-state-machine/stateless) library with Orleans-native async APIs, event sourcing, and advanced distributed patterns.

## Why Orleans.StateMachineES?

Build reliable, maintainable distributed systems with:

- **Compile-time Safety**: 10 Roslyn analyzers catch state machine anti-patterns before they reach production
- **Event Sourcing Built-in**: Complete state history with replay capabilities - and it's 30% faster than regular state machines
- **Production Resilience**: Circuit breakers, retries, validation components, and distributed tracing out of the box
- **Advanced Patterns**: Hierarchical states, distributed sagas, orthogonal regions, and versioning
- **High Performance**: TriggerParameterCache provides ~100x speedup for parameterized triggers, optimized object pooling

## Quick Start

### Installation

```bash
dotnet add package Orleans.StateMachineES
dotnet add package Orleans.StateMachineES.Generators
```

### Your First State Machine

```csharp
using Orleans.StateMachineES;

public enum OrderState { Pending, Confirmed, Shipped, Delivered }
public enum OrderTrigger { Confirm, Ship, Deliver }

public interface IOrderGrain : IStateMachineGrain<OrderState, OrderTrigger>
{
}

public class OrderGrain : StateMachineGrain<OrderState, OrderTrigger>, IOrderGrain
{
    protected override void BuildStateMachine()
    {
        StateMachine.Configure(OrderState.Pending)
            .Permit(OrderTrigger.Confirm, OrderState.Confirmed)
            .OnEntry(() => GrainFactory.GetGrain<INotificationGrain>(0)
                .SendEmailAsync("Order received!"));

        StateMachine.Configure(OrderState.Confirmed)
            .Permit(OrderTrigger.Ship, OrderState.Shipped);

        StateMachine.Configure(OrderState.Shipped)
            .Permit(OrderTrigger.Deliver, OrderState.Delivered);
    }
}
```

### Usage

```csharp
var order = grainFactory.GetGrain<IOrderGrain>(orderId);

// Fire transitions
await order.FireAsync(OrderTrigger.Confirm);
await order.FireAsync(OrderTrigger.Ship);

// Query state
var currentState = await order.GetStateAsync(); // OrderState.Shipped
var canDeliver = await order.CanFireAsync(OrderTrigger.Deliver); // true
var permitted = await order.GetPermittedTriggersAsync(); // [OrderTrigger.Deliver]
```

[Learn more in the Getting Started guide →](articles/getting-started/index.md)

## Key Features

### Core State Machines
The foundation of distributed state management with Orleans.

- **StateMachineGrain Base Class**: Orleans-native async state machine grain
- **Parameterized Triggers**: Pass up to 3 typed arguments with transitions
- **Guard Conditions**: Validate transitions with business logic
- **State Inspection**: Query current state, permitted triggers, and guard status

[Explore Core Concepts →](articles/getting-started/core-concepts.md)

### Event Sourcing
Complete state history and replay capabilities - **30.4% faster** than regular state machines.

```csharp
public class OrderGrain : EventSourcedStateMachineGrain<OrderState, OrderTrigger>
{
    // Automatic event persistence and replay
    // Use AutoConfirmEvents = true for maximum performance
}
```

- **Automatic Event Persistence**: All transitions saved as events
- **State Replay**: Rebuild grain state from event history
- **Idempotency**: Safe re-execution of transitions
- **Stream Publishing**: Orleans streams integration

[Learn about Event Sourcing →](articles/guides/event-sourcing.md)

### Advanced Patterns

**Hierarchical States**: Parent-child state relationships with substates
```csharp
StateMachine.Configure(OrderState.InTransit)
    .SubstateOf(OrderState.Active)
    .Permit(OrderTrigger.Delay, OrderState.Delayed);
```

**Distributed Sagas**: Multi-grain workflows with compensation
```csharp
var saga = new OrderSaga();
await saga.ExecuteAsync(context);
// Automatic compensation on failure
```

**Orthogonal Regions**: Parallel independent state machines
```csharp
var regions = new OrthogonalRegions<SmartHomeState, SmartHomeTrigger>();
regions.AddRegion("Lighting", lightingStateMachine);
regions.AddRegion("Climate", climateStateMachine);
```

[Explore Advanced Patterns →](articles/guides/index.md)

### Production Features

**Circuit Breaker**: Prevent cascading failures
```csharp
var circuitBreaker = new CircuitBreakerComponent<State, Trigger>(new()
{
    FailureThreshold = 5,
    SuccessThreshold = 2,
    OpenDuration = TimeSpan.FromSeconds(30)
});
```

**Distributed Tracing**: OpenTelemetry integration for observability

**Health Checks**: ASP.NET Core health monitoring

**Versioning**: Manage state machine evolution across deployments

[Read Production Guide →](articles/architecture/production.md)

### Compile-time Safety

10 Roslyn analyzers catch issues before deployment:

| Analyzer | Description |
|----------|-------------|
| **OSMES001** | Prevents async lambdas in callbacks |
| **OSMES002** | Blocks FireAsync in state callbacks |
| **OSMES003** | Requires BuildStateMachine implementation |
| **OSMES004** | Detects unreachable states |
| **OSMES009** | Ensures initial state is set |
| *...and 5 more* | [See all analyzers →](articles/guides/analyzers.md) |

### Visualization

Generate diagrams from your state machines:

```csharp
var visualization = new StateMachineVisualization<State, Trigger>(stateMachine);
var mermaid = visualization.GenerateMermaidDiagram();
var dot = visualization.GenerateDotGraph();
var plantuml = visualization.GeneratePlantUmlDiagram();
var html = visualization.GenerateHtmlDiagram();
```

[Learn about Visualization →](articles/guides/visualization.md)

## Performance

Optimized for production workloads:

- **Event Sourcing**: 5,923 transitions/sec (30% faster than regular grains)
- **TriggerParameterCache**: ~100x performance improvement for parameterized triggers
- **ValueTask**: Zero-allocation paths for synchronous operations
- **FrozenCollections**: 40%+ faster lookups in .NET 8+
- **Object Pooling**: Thread-safe, high-performance resource management

[Read Performance Guide →](articles/architecture/performance.md)

## Complete Examples

Explore production-ready applications:

- **[E-Commerce Workflow](articles/examples/ecommerce.md)**: Event sourcing, timers, sagas
- **[Document Approval](articles/examples/document-approval.md)**: Hierarchical states, multi-level approval
- **[Monitoring Dashboard](articles/examples/monitoring.md)**: Health checks, metrics, observability
- **[Smart Home Automation](articles/examples/smart-home.md)**: Orthogonal regions, source generator

[Browse all examples →](articles/examples/index.md)

## Documentation

### For Developers
- [Getting Started](articles/getting-started/index.md) - Installation and first state machine
- [Core Concepts](articles/getting-started/core-concepts.md) - Fundamental patterns
- [Async Patterns](articles/guides/async-patterns.md) - Critical async operation guidelines
- [Cheat Sheet](articles/reference/cheat-sheet.md) - Quick reference guide

### For Architects
- [Architecture Overview](articles/architecture/index.md) - Design decisions and patterns
- [Performance Tuning](articles/architecture/performance.md) - Optimization strategies
- [Production Deployment](articles/architecture/production.md) - Production best practices

### For Contributors
- [Contributing Guide](articles/reference/contributing.md) - How to contribute
- [Analyzer Development](articles/guides/analyzers.md) - Writing analyzers
- [Testing Strategy](articles/architecture/testing.md) - Testing approach

## API Reference

Browse the complete API documentation for all packages:

- [Orleans.StateMachineES](api/Orleans.StateMachineES.yml) - Main library
- [Orleans.StateMachineES.Abstractions](api/Orleans.StateMachineES.Abstractions.yml) - Core interfaces
- [Orleans.StateMachineES.Generators](api/Orleans.StateMachineES.Generators.yml) - Analyzers and generators

[Explore API Reference →](api/index.md)

## Version Information

**Current Version**: 1.1.0 (Released February 2025)

**Latest Improvements**:
- **Rate Limiting**: Token bucket algorithm with burst capacity and wait support
- **Batch Operations**: Parallel execution API with retry and progress tracking
- **Event Schema Evolution**: Automatic event upcasting with version chains
- **Persistence Abstraction**: Provider-agnostic IEventStore/ISnapshotStore interfaces
- **State Machine Templates**: Reusable patterns for approval, order, retry workflows
- **State History Queries**: Fluent API for temporal queries with analytics

[View Changelog](https://github.com/mivertowski/Orleans.StateMachineES/blob/main/CHANGELOG.md) | [Migration Guide](articles/reference/migration-guide.md)

## Community & Support

- **GitHub**: [Orleans.StateMachineES Repository](https://github.com/mivertowski/Orleans.StateMachineES)
- **Issues**: [Report a bug](https://github.com/mivertowski/Orleans.StateMachineES/issues)
- **Discussions**: [Ask questions](https://github.com/mivertowski/Orleans.StateMachineES/discussions)
- **NuGet**: [Package Downloads](https://www.nuget.org/packages/Orleans.StateMachineES)

## License

Orleans.StateMachineES is licensed under the [MIT License](https://github.com/mivertowski/Orleans.StateMachineES/blob/main/LICENSE).

---

**Ready to get started?** [Install Orleans.StateMachineES →](articles/getting-started/installation.md)
