# Orleans.StateMachineES.Abstractions

[![NuGet](https://img.shields.io/nuget/v/Orleans.StateMachineES.Abstractions.svg)](https://www.nuget.org/packages/Orleans.StateMachineES.Abstractions/)

Core interfaces and models for Orleans.StateMachineES.

## Installation

```bash
dotnet add package Orleans.StateMachineES.Abstractions
```

## What's Included

This package contains the core abstractions used by Orleans.StateMachineES:

- **IStateMachineGrain<TState, TTrigger>**: Core interface for state machine grains
- **OrleansStateMachineInfo**: Metadata about the state machine
- **OrleansStateInfo**: Information about individual states
- **Exception Types**: `InvalidStateTransitionException`, etc.

## When to Use

Reference this package directly when you:

1. Define shared interfaces for your state machine grains
2. Need to reference state machine types without pulling in the full implementation
3. Build libraries that consume state machine grains

## Example Usage

```csharp
// Define your grain interface
public interface IOrderGrain : IStateMachineGrain<OrderState, OrderTrigger>
{
    Task SubmitOrderAsync();
    Task CancelOrderAsync();
}

// Reference the grain from other projects
public class OrderService
{
    private readonly IGrainFactory _grainFactory;

    public async Task<OrderState> GetOrderStateAsync(Guid orderId)
    {
        var grain = _grainFactory.GetGrain<IOrderGrain>(orderId);
        return await grain.GetStateAsync();
    }
}
```

## Dependencies

- Orleans.Core.Abstractions 9.1.2

## Main Package

For the full implementation, install:
```bash
dotnet add package Orleans.StateMachineES
```

## Documentation

- [Main Documentation](https://github.com/mivertowski/Orleans.StateMachineES)
- [API Reference](https://github.com/mivertowski/Orleans.StateMachineES/blob/main/docs/API.md)

## License

MIT License - see LICENSE file for details
