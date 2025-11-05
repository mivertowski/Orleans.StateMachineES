# Core Concepts

Understanding the fundamental concepts of Orleans.StateMachineES is essential for building robust state machines.

## State Machines Fundamentals

A **state machine** models a system that can be in exactly one state at a time, with defined transitions between states triggered by events.

### Components

1. **States** - Possible conditions the system can be in
2. **Triggers** - Events that cause transitions
3. **Transitions** - Rules for moving between states
4. **Guards** - Conditions that control whether transitions are allowed
5. **Callbacks** - Actions executed during transitions

## States

States represent distinct conditions or phases in your system's lifecycle.

### Defining States

Use enums for type-safe state definitions:

```csharp
public enum OrderState
{
    Draft,        // Initial state
    Submitted,    // Awaiting payment
    Processing,   // Being fulfilled
    Shipped,      // In transit
    Delivered,    // Complete
    Cancelled     // Terminated
}
```

### State Properties

**Mutually Exclusive**: A state machine can only be in one state at a time (except with [orthogonal regions](../guides/orthogonal-regions.md)).

**Persistent**: The current state persists across grain activations.

**Observable**: You can always query the current state with `GetStateAsync()`.

### State Configuration

Configure each state in `BuildStateMachine()`:

```csharp
StateMachine.Configure(OrderState.Processing)
    .Permit(OrderTrigger.Ship, OrderState.Shipped)
    .Permit(OrderTrigger.Cancel, OrderState.Cancelled)
    .OnEntry(() => StartFulfillmentProcess())
    .OnExit(() => CleanupResources());
```

## Triggers

Triggers are events that cause state transitions.

### Defining Triggers

Use enums for type-safe trigger definitions:

```csharp
public enum OrderTrigger
{
    Submit,
    ConfirmPayment,
    Ship,
    Deliver,
    Cancel
}
```

### Firing Triggers

Execute transitions with `FireAsync()`:

```csharp
await orderGrain.FireAsync(OrderTrigger.Submit);
```

### Trigger Types

**Simple Triggers**: No additional data
```csharp
await grain.FireAsync(OrderTrigger.Cancel);
```

**Parameterized Triggers**: Pass data with the transition
```csharp
var trigger = StateMachine.SetTriggerParameters<string>(OrderTrigger.Ship);
await grain.FireAsync(trigger, "UPS123456789");
```

See [Parameterized Triggers](parameterized-triggers.md) for details.

## Transitions

Transitions define how the state machine moves from one state to another.

### Permit Transitions

Use `.Permit()` to allow a transition:

```csharp
StateMachine.Configure(OrderState.Submitted)
    .Permit(OrderTrigger.ConfirmPayment, OrderState.Processing);
```

### Conditional Transitions

Use `.PermitIf()` with guard conditions:

```csharp
StateMachine.Configure(OrderState.Submitted)
    .PermitIf(
        OrderTrigger.ConfirmPayment,
        OrderState.Processing,
        () => _paymentReceived && _inventoryAvailable
    );
```

### Self-Transitions

Allow a state to transition to itself:

```csharp
StateMachine.Configure(OrderState.Processing)
    .PermitReentry(OrderTrigger.UpdateStatus);
```

### Ignore Triggers

Ignore triggers without error:

```csharp
StateMachine.Configure(OrderState.Cancelled)
    .Ignore(OrderTrigger.Ship);  // Silently ignored
```

## Guard Conditions

Guards control whether a transition is allowed based on runtime conditions.

### Simple Guards

```csharp
StateMachine.Configure(OrderState.Draft)
    .PermitIf(
        OrderTrigger.Submit,
        OrderState.Submitted,
        () => _items.Count > 0  // Guard: must have items
    );
```

### Multiple Guards

All guards must pass for the transition to succeed:

```csharp
StateMachine.Configure(OrderState.Submitted)
    .PermitIf(
        OrderTrigger.ConfirmPayment,
        OrderState.Processing,
        () => _paymentReceived,
        () => _inventoryAvailable,
        () => _shippingAddressValid
    );
```

### Unmet Guards

Check guard status before firing:

```csharp
// Returns list of unmet guard descriptions
var unmetGuards = await grain.CanFireWithUnmetGuardsAsync(
    OrderTrigger.ConfirmPayment
);

if (unmetGuards.UnmetGuards.Any())
{
    Console.WriteLine("Cannot confirm payment:");
    foreach (var guard in unmetGuards.UnmetGuards)
    {
        Console.WriteLine($"  - {guard}");
    }
}
```

See [Guard Conditions](guard-conditions.md) for advanced patterns.

## Callbacks

Callbacks execute custom logic during state transitions.

### OnEntry Callbacks

Execute when entering a state:

```csharp
StateMachine.Configure(OrderState.Processing)
    .OnEntry(() =>
    {
        _processingStartTime = DateTime.UtcNow;
        NotifyWarehouse();
    });
```

### OnExit Callbacks

Execute when leaving a state:

```csharp
StateMachine.Configure(OrderState.Processing)
    .OnExit(() =>
    {
        _processingDuration = DateTime.UtcNow - _processingStartTime;
        RecordMetrics();
    });
```

### OnEntryFrom Callbacks

Execute when entering from a specific trigger:

```csharp
StateMachine.Configure(OrderState.Cancelled)
    .OnEntryFrom(OrderTrigger.CancelByCustomer, () =>
    {
        _cancellationReason = "Customer requested";
        RefundPayment();
    })
    .OnEntryFrom(OrderTrigger.CancelBySystem, () =>
    {
        _cancellationReason = "Fraud detected";
        BlockCustomer();
    });
```

### OnExitFrom Callbacks

Execute when leaving to a specific state:

```csharp
StateMachine.Configure(OrderState.Processing)
    .OnExitFrom(OrderState.Shipped, () =>
    {
        GenerateTrackingNumber();
    });
```

### Callback Constraints

> **Critical**: Callbacks must be synchronous. Async operations are not supported.

```csharp
// ❌ Wrong: Async not supported
.OnEntry(async () => await SendEmailAsync())

// ✅ Correct: Use timers for async work
.OnEntry(() => RegisterTimer(_ => SendEmailAsync(), null, TimeSpan.Zero, TimeSpan.MaxValue))
```

See [Async Patterns](../guides/async-patterns.md) for handling async operations correctly.

## State Machine Lifecycle

### Activation

When a grain activates, `BuildStateMachine()` is called to construct the state machine:

```csharp
public override async Task OnActivateAsync(CancellationToken cancellationToken)
{
    await base.OnActivateAsync(cancellationToken);
    // State machine is now ready
}
```

### State Persistence

The current state is persisted automatically:

- **Automatic**: State survives grain deactivation
- **Consistent**: State is part of grain state storage
- **Event-Sourced**: Optional full history with `EventSourcedStateMachineGrain`

### Deactivation

On deactivation, state is saved:

```csharp
public override async Task OnDeactivateAsync(DeactivationReason reason, CancellationToken cancellationToken)
{
    // State is automatically persisted
    await base.OnDeactivateAsync(reason, cancellationToken);
}
```

## State Machine Metadata

Query state machine information at runtime:

### Current State

```csharp
var state = await grain.GetStateAsync();
```

### Permitted Triggers

```csharp
var triggers = await grain.GetPermittedTriggersAsync();
// Returns: [OrderTrigger.Ship, OrderTrigger.Cancel]
```

### Can Fire Check

```csharp
bool canShip = await grain.CanFireAsync(OrderTrigger.Ship);
```

### Complete Info

```csharp
var info = await grain.GetStateMachineInfoAsync();
Console.WriteLine($"Current: {info.State}");
Console.WriteLine($"Initial: {info.InitialState}");
Console.WriteLine($"All states: {string.Join(", ", info.States)}");
Console.WriteLine($"All triggers: {string.Join(", ", info.Triggers)}");
```

## State Diagrams

Visualize your state machine:

```csharp
var dot = await grain.GetDotGraphAsync();
var mermaid = await grain.GetMermaidDiagramAsync();
var plantUml = await grain.GetPlantUmlDiagramAsync();
```

See [Visualization Guide](../guides/visualization.md) for details.

## Best Practices

### 1. Use Descriptive Names

```csharp
// ✅ Good: Clear intent
public enum OrderState { Draft, Submitted, Processing, Shipped }
public enum OrderTrigger { Submit, ConfirmPayment, Ship }

// ❌ Bad: Unclear meaning
public enum State { S1, S2, S3 }
public enum Event { E1, E2 }
```

### 2. Define All Transitions

```csharp
// ✅ Good: All states configured
StateMachine.Configure(OrderState.Draft)
    .Permit(OrderTrigger.Submit, OrderState.Submitted)
    .Permit(OrderTrigger.Cancel, OrderState.Cancelled);

// ❌ Bad: Unreachable states
StateMachine.Configure(OrderState.Draft)
    .Permit(OrderTrigger.Submit, OrderState.Submitted);
// OrderState.Cancelled is unreachable!
```

The **OSMES004** analyzer detects unreachable states.

### 3. Handle All Triggers

```csharp
// ✅ Good: Define what happens with unexpected triggers
StateMachine.OnUnhandledTrigger((state, trigger) =>
{
    _logger.LogWarning("Unexpected trigger {Trigger} in state {State}", trigger, state);
});
```

### 4. Keep Guards Simple

```csharp
// ✅ Good: Simple, testable guard
.PermitIf(trigger, nextState, () => _isValid)

// ❌ Bad: Complex logic in guard
.PermitIf(trigger, nextState, () =>
{
    // 50 lines of complex validation
    // Cyclomatic complexity > 10
})
```

The **OSMES008** analyzer warns about complex guards.

### 5. Avoid Side Effects in Guards

```csharp
// ❌ Bad: Guard modifies state
.PermitIf(trigger, nextState, () =>
{
    _counter++;  // Side effect!
    return _counter > 5;
})

// ✅ Good: Guard is pure
.PermitIf(trigger, nextState, () => _counter > 5)
```

## Next Steps

- [Parameterized Triggers](parameterized-triggers.md) - Pass data with transitions
- [Guard Conditions](guard-conditions.md) - Advanced validation patterns
- [Async Patterns](../guides/async-patterns.md) - Handling async operations correctly
- [Event Sourcing](../guides/event-sourcing.md) - Complete state history

## Additional Resources

- [Stateless Library Docs](https://github.com/dotnet-state-machine/stateless) - Underlying state machine library
- [API Reference](../../api/Orleans.StateMachineES.StateMachineGrain-2.yml)
- [Examples](../examples/index.md)
