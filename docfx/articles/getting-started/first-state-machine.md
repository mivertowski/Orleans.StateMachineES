# Your First State Machine

This tutorial walks you through creating your first state machine grain step by step.

## What We're Building

We'll create a simple **light switch** state machine with two states (On/Off) and one trigger (Toggle). This demonstrates the fundamentals without complexity.

## Step 1: Define States and Triggers

Create enums for your states and triggers:

```csharp
public enum LightState
{
    Off,
    On
}

public enum LightTrigger
{
    Toggle
}
```

> **Tip**: Use clear, descriptive names. States are typically nouns (Off, On), triggers are verbs (Toggle, Switch).

## Step 2: Define the Grain Interface

Create an interface that extends `IStateMachineGrain<TState, TTrigger>`:

```csharp
using Orleans.StateMachineES.Interfaces;

public interface ILightSwitchGrain : IStateMachineGrain<LightState, LightTrigger>
{
    // Optional: Add custom grain methods here
    Task<int> GetToggleCountAsync();
}
```

The base interface provides all standard state machine operations:
- `FireAsync(trigger)` - Execute a transition
- `GetStateAsync()` - Query current state
- `CanFireAsync(trigger)` - Check if transition is permitted
- `GetPermittedTriggersAsync()` - Get all valid triggers

## Step 3: Implement the Grain

Create a grain class that extends `StateMachineGrain<TState, TTrigger>`:

```csharp
using Orleans.StateMachineES;

public class LightSwitchGrain : StateMachineGrain<LightState, LightTrigger>, ILightSwitchGrain
{
    private int _toggleCount = 0;

    protected override void BuildStateMachine()
    {
        // Configure the Off state
        StateMachine.Configure(LightState.Off)
            .Permit(LightTrigger.Toggle, LightState.On)
            .OnEntry(() =>
            {
                Console.WriteLine("Light is now OFF");
            });

        // Configure the On state
        StateMachine.Configure(LightState.On)
            .Permit(LightTrigger.Toggle, LightState.Off)
            .OnEntry(() =>
            {
                Console.WriteLine("Light is now ON");
                _toggleCount++;
            });

        // Set the initial state
        StateMachine.State = LightState.Off;
    }

    public Task<int> GetToggleCountAsync()
    {
        return Task.FromResult(_toggleCount);
    }
}
```

### Understanding BuildStateMachine()

The `BuildStateMachine()` method is called during grain activation. Here you:

1. **Configure each state** using `StateMachine.Configure(state)`
2. **Define transitions** with `.Permit(trigger, destinationState)`
3. **Add callbacks** using `.OnEntry()` and `.OnExit()`
4. **Set initial state** with `StateMachine.State = ...`

> **Important**: Callbacks must be synchronous. See [Async Patterns](../guides/async-patterns.md) for handling async operations.

## Step 4: Use the Grain

In your client or another grain:

```csharp
// Get a reference to the grain
var lightSwitch = grainFactory.GetGrain<ILightSwitchGrain>(0);

// Check current state
var currentState = await lightSwitch.GetStateAsync();
Console.WriteLine($"Current state: {currentState}"); // Output: Off

// Toggle the light
await lightSwitch.FireAsync(LightTrigger.Toggle);
currentState = await lightSwitch.GetStateAsync();
Console.WriteLine($"Current state: {currentState}"); // Output: On

// Toggle again
await lightSwitch.FireAsync(LightTrigger.Toggle);
currentState = await lightSwitch.GetStateAsync();
Console.WriteLine($"Current state: {currentState}"); // Output: Off

// Check toggle count
var count = await lightSwitch.GetToggleCountAsync();
Console.WriteLine($"Toggled {count} times"); // Output: Toggled 1 times
```

## Step 5: Query State Machine Metadata

Orleans.StateMachineES provides rich metadata about your state machine:

```csharp
var lightSwitch = grainFactory.GetGrain<ILightSwitchGrain>(0);

// Check if a trigger can fire
bool canToggle = await lightSwitch.CanFireAsync(LightTrigger.Toggle);
Console.WriteLine($"Can toggle: {canToggle}"); // Output: true

// Get all permitted triggers
var permittedTriggers = await lightSwitch.GetPermittedTriggersAsync();
Console.WriteLine($"Permitted: {string.Join(", ", permittedTriggers)}");
// Output: Permitted: Toggle

// Get detailed state machine info
var info = await lightSwitch.GetStateMachineInfoAsync();
Console.WriteLine($"Current: {info.State}");
Console.WriteLine($"Initial: {info.InitialState}");
Console.WriteLine($"States: {string.Join(", ", info.States)}");
Console.WriteLine($"Triggers: {string.Join(", ", info.Triggers)}");
```

## Complete Example

Here's the complete code in one place:

```csharp
// States and Triggers
public enum LightState { Off, On }
public enum LightTrigger { Toggle }

// Interface
public interface ILightSwitchGrain : IStateMachineGrain<LightState, LightTrigger>
{
    Task<int> GetToggleCountAsync();
}

// Implementation
public class LightSwitchGrain : StateMachineGrain<LightState, LightTrigger>, ILightSwitchGrain
{
    private int _toggleCount = 0;

    protected override void BuildStateMachine()
    {
        StateMachine.Configure(LightState.Off)
            .Permit(LightTrigger.Toggle, LightState.On)
            .OnEntry(() => Console.WriteLine("Light OFF"));

        StateMachine.Configure(LightState.On)
            .Permit(LightTrigger.Toggle, LightState.Off)
            .OnEntry(() =>
            {
                Console.WriteLine("Light ON");
                _toggleCount++;
            });

        StateMachine.State = LightState.Off;
    }

    public Task<int> GetToggleCountAsync() => Task.FromResult(_toggleCount);
}

// Usage
var light = grainFactory.GetGrain<ILightSwitchGrain>(0);
await light.FireAsync(LightTrigger.Toggle); // Turn on
await light.FireAsync(LightTrigger.Toggle); // Turn off
var count = await light.GetToggleCountAsync(); // 1
```

## What You've Learned

- How to define states and triggers using enums
- Creating a grain interface extending `IStateMachineGrain<,>`
- Implementing `StateMachineGrain<,>` and `BuildStateMachine()`
- Configuring states with transitions and callbacks
- Using `FireAsync()` to execute transitions
- Querying state machine metadata

## Common Mistakes

### 1. Forgetting to Set Initial State

```csharp
// ❌ Wrong: No initial state
protected override void BuildStateMachine()
{
    StateMachine.Configure(LightState.Off)
        .Permit(LightTrigger.Toggle, LightState.On);
}

// ✅ Correct: Set initial state
protected override void BuildStateMachine()
{
    StateMachine.Configure(LightState.Off)
        .Permit(LightTrigger.Toggle, LightState.On);

    StateMachine.State = LightState.Off; // Required!
}
```

The **OSMES009** analyzer will catch this at compile time.

### 2. Using Async Lambdas in Callbacks

```csharp
// ❌ Wrong: Async lambda not supported
StateMachine.Configure(LightState.On)
    .OnEntry(async () =>
    {
        await SomeAsyncOperation();
    });

// ✅ Correct: Keep callbacks synchronous
StateMachine.Configure(LightState.On)
    .OnEntry(() =>
    {
        RegisterTimer(_ => SomeAsyncOperation(), null, TimeSpan.Zero, TimeSpan.FromSeconds(5));
    });
```

The **OSMES001** analyzer will warn you about this pattern.

### 3. Calling FireAsync in Callbacks

```csharp
// ❌ Wrong: FireAsync in callback causes runtime error
StateMachine.Configure(LightState.On)
    .OnEntry(() =>
    {
        _ = FireAsync(LightTrigger.SomeOtherTrigger); // Runtime error!
    });

// ✅ Correct: Fire triggers from grain methods
public async Task TurnOnAndDoSomethingAsync()
{
    await FireAsync(LightTrigger.Toggle);
    // Now do async work after the transition
    await PerformAsyncOperation();
}
```

The **OSMES002** analyzer prevents this at compile time.

## Next Steps

Now that you've built your first state machine:

1. [Learn core concepts](core-concepts.md) - Understand states, triggers, and transitions in depth
2. [Add parameterized triggers](parameterized-triggers.md) - Pass data with transitions
3. [Implement guard conditions](guard-conditions.md) - Validate transitions with business logic

## Additional Resources

- [Async Patterns Guide](../guides/async-patterns.md) - Critical reading for production code
- [Analyzer Reference](../guides/analyzers.md) - Understanding all 10 analyzers
- [API Reference](../../api/Orleans.StateMachineES.StateMachineGrain-2.yml) - Complete API documentation
- [Examples](../examples/index.md) - Real-world implementations
