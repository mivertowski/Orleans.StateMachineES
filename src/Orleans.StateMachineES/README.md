# Orleans.StateMachineES

[![NuGet](https://img.shields.io/nuget/v/Orleans.StateMachineES.svg)](https://www.nuget.org/packages/Orleans.StateMachineES/)
[![Downloads](https://img.shields.io/nuget/dt/Orleans.StateMachineES.svg)](https://www.nuget.org/packages/Orleans.StateMachineES/)

Production-ready state machine implementation for Microsoft Orleans with comprehensive event sourcing, enterprise features, and performance optimizations.

## Installation

```bash
dotnet add package Orleans.StateMachineES
```

## Quick Start

### 1. Define Your States and Triggers

```csharp
public enum OrderState { Pending, Processing, Completed, Cancelled }
public enum OrderTrigger { Submit, Process, Complete, Cancel }
```

### 2. Create Your State Machine Grain

```csharp
public class OrderGrain : StateMachineGrain<OrderState, OrderTrigger>, IOrderGrain
{
    protected override StateMachine<OrderState, OrderTrigger> BuildStateMachine()
    {
        var machine = new StateMachine<OrderState, OrderTrigger>(OrderState.Pending);

        machine.Configure(OrderState.Pending)
            .Permit(OrderTrigger.Submit, OrderState.Processing)
            .Permit(OrderTrigger.Cancel, OrderState.Cancelled);

        machine.Configure(OrderState.Processing)
            .Permit(OrderTrigger.Complete, OrderState.Completed)
            .Permit(OrderTrigger.Cancel, OrderState.Cancelled);

        return machine;
    }

    public async Task SubmitOrderAsync()
    {
        await FireAsync(OrderTrigger.Submit);
    }
}
```

### 3. Use Your Grain

```csharp
var grain = client.GetGrain<IOrderGrain>(orderId);
await grain.SubmitOrderAsync();
var state = await grain.GetStateAsync();
Console.WriteLine($"Order is {state}");
```

## Key Features

- **Orleans Integration**: Seamless integration with Microsoft Orleans actor framework
- **Event Sourcing**: Complete state history and replay capabilities via `EventSourcedStateMachineGrain`
- **Performance Optimized**: TriggerParameterCache for ~100x speedup on parameterized triggers
- **Production Ready**: Circuit breaker pattern, object pooling, memory optimization
- **Type Safe**: Compile-time safety with generic state/trigger types
- **Comprehensive Analyzers**: 10 Roslyn analyzers catch state machine anti-patterns at compile time

## Advanced Features

### Event Sourcing

```csharp
public class EventSourcedOrderGrain : EventSourcedStateMachineGrain<OrderState, OrderTrigger, OrderState>, IOrderGrain
{
    protected override StateMachine<OrderState, OrderTrigger> BuildStateMachine()
    {
        // Same as above
    }

    protected override void ConfigureEventSourcing(EventSourcingOptions options)
    {
        options.AutoConfirmEvents = true;
        options.EnableIdempotency = true;
    }
}
```

### Parameterized Triggers

```csharp
machine.Configure(OrderState.Pending)
    .PermitIf(parameterizedTrigger, OrderState.Processing,
        (amount) => amount > 0);

await FireAsync(OrderTrigger.ProcessPayment, amount: 100.00m);
```

### Circuit Breaker

```csharp
var circuitBreaker = new CircuitBreakerComponent<OrderState, OrderTrigger>(
    new CircuitBreakerOptions
    {
        FailureThreshold = 5,
        SuccessThreshold = 2,
        OpenDuration = TimeSpan.FromSeconds(30)
    });
```

## Documentation

- [Main Documentation](https://github.com/mivertowski/Orleans.StateMachineES)
- [Async Patterns Guide](https://github.com/mivertowski/Orleans.StateMachineES/blob/main/docs/ASYNC_PATTERNS.md)
- [API Reference](https://github.com/mivertowski/Orleans.StateMachineES/blob/main/docs/API.md)

## Roslyn Analyzers

This package includes compile-time analyzers that catch common mistakes:

- **OSMES001**: Async lambda in state callback
- **OSMES002**: FireAsync called within state callback (Error)
- **OSMES003**: Missing BuildStateMachine implementation
- **OSMES004**: Unreachable state detected
- **OSMES009**: Missing initial state assignment (Error)
- And 5 more...

Install the analyzer package for full compile-time safety:
```bash
dotnet add package Orleans.StateMachineES.Generators
```

## Requirements

- .NET 9.0 or later
- Microsoft.Orleans.Sdk 9.1.2 or later
- Stateless 5.17.0

## License

MIT License - see LICENSE file for details

## Support

- [GitHub Issues](https://github.com/mivertowski/Orleans.StateMachineES/issues)
- [Discussions](https://github.com/mivertowski/Orleans.StateMachineES/discussions)
