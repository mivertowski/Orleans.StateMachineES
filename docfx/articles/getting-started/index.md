# Getting Started with Orleans.StateMachineES

Welcome to Orleans.StateMachineES! This guide will help you get up and running with state machines in your Orleans applications.

## What You'll Learn

This getting started series covers:

1. **[Installation](installation.md)** - Setting up Orleans.StateMachineES in your project
2. **[First State Machine](first-state-machine.md)** - Building your first state machine grain
3. **[Core Concepts](core-concepts.md)** - Understanding states, triggers, and transitions
4. **[Parameterized Triggers](parameterized-triggers.md)** - Passing data with transitions
5. **[Guard Conditions](guard-conditions.md)** - Validating transitions with business logic
6. **[Next Steps](next-steps.md)** - Exploring advanced features

## Prerequisites

Before you begin, ensure you have:

- **.NET 9.0 SDK** or later installed
- **Basic Orleans knowledge** - Understanding of grains and the Orleans runtime
- **C# familiarity** - Knowledge of async/await, generics, and enums
- **An Orleans project** - Either existing or newly created

If you're new to Orleans, check out the [official Orleans documentation](https://learn.microsoft.com/en-us/dotnet/orleans/) first.

## Quick Overview

Orleans.StateMachineES provides state machine functionality for Orleans grains through:

### Core Components

- **StateMachineGrain&lt;TState, TTrigger&gt;** - Base class for state machine grains
- **EventSourcedStateMachineGrain&lt;TState, TTrigger&gt;** - Event sourcing support
- **IStateMachineGrain&lt;TState, TTrigger&gt;** - Standard grain interface

### Key Features

- Orleans-native async APIs
- Compile-time safety with 10 Roslyn analyzers
- Event sourcing with state replay
- Production components (circuit breakers, retries)
- Performance optimizations (TriggerParameterCache)

## Learning Path

### Beginner (Start Here)
1. [Install the packages](installation.md)
2. [Create your first state machine](first-state-machine.md)
3. [Understand core concepts](core-concepts.md)

### Intermediate
4. [Use parameterized triggers](parameterized-triggers.md)
5. [Add guard conditions](guard-conditions.md)
6. [Explore async patterns](../guides/async-patterns.md)

### Advanced
7. [Enable event sourcing](../guides/event-sourcing.md)
8. [Build hierarchical states](../guides/hierarchical-states.md)
9. [Implement distributed sagas](../guides/distributed-sagas.md)

## Example: Order Processing

Here's a preview of what you'll be able to build:

```csharp
public enum OrderState
{
    Draft,
    Submitted,
    PaymentPending,
    PaymentConfirmed,
    Processing,
    Shipped,
    Delivered,
    Cancelled
}

public enum OrderTrigger
{
    Submit,
    ConfirmPayment,
    Process,
    Ship,
    Deliver,
    Cancel
}

public class OrderGrain : StateMachineGrain<OrderState, OrderTrigger>, IOrderGrain
{
    protected override void BuildStateMachine()
    {
        StateMachine.Configure(OrderState.Draft)
            .Permit(OrderTrigger.Submit, OrderState.Submitted)
            .Permit(OrderTrigger.Cancel, OrderState.Cancelled);

        StateMachine.Configure(OrderState.Submitted)
            .OnEntry(() => StartPaymentProcessing())
            .Permit(OrderTrigger.ConfirmPayment, OrderState.PaymentConfirmed)
            .Permit(OrderTrigger.Cancel, OrderState.Cancelled);

        StateMachine.Configure(OrderState.PaymentConfirmed)
            .Permit(OrderTrigger.Process, OrderState.Processing);

        StateMachine.Configure(OrderState.Processing)
            .Permit(OrderTrigger.Ship, OrderState.Shipped);

        StateMachine.Configure(OrderState.Shipped)
            .Permit(OrderTrigger.Deliver, OrderState.Delivered);
    }

    private void StartPaymentProcessing()
    {
        // Payment gateway integration
        var paymentGrain = GrainFactory.GetGrain<IPaymentGrain>(this.GetPrimaryKeyLong());
        RegisterTimer(
            _ => paymentGrain.CheckPaymentStatusAsync(),
            null,
            TimeSpan.FromSeconds(30),
            TimeSpan.FromSeconds(30)
        );
    }
}
```

This state machine handles the complete order lifecycle with transitions, entry callbacks, and integration with other grains.

## Need Help?

- **Examples**: Check out [complete examples](../examples/index.md)
- **API Reference**: Browse the [API documentation](../../api/index.md)
- **Troubleshooting**: See [common issues](../reference/troubleshooting.md)
- **Community**: Ask questions on [GitHub Discussions](https://github.com/mivertowski/Orleans.StateMachineES/discussions)

## Ready to Begin?

[Start with Installation →](installation.md)
