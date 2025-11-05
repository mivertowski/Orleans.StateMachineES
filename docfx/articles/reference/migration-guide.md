# Migration Guide: From ManagedCode.Orleans.StateMachine to Orleans.StateMachineES

## Overview

This guide helps you migrate from the original `ManagedCode.Orleans.StateMachine` library to the enhanced `Orleans.StateMachineES` library with event sourcing capabilities.

## Key Differences

### 1. Namespace Changes
- **Old**: `ManagedCode.Orleans.StateMachine`
- **New**: `Orleans.StateMachineES`

### 2. New Features
- **Event Sourcing**: Automatic persistence of state transitions as events
- **JournaledGrain Integration**: Built on Orleans' event sourcing foundation
- **Idempotency**: Built-in deduplication for repeated triggers
- **Stream Publishing**: Optional publishing of events to Orleans Streams
- **Correlation Tracking**: Built-in correlation ID support for distributed tracing

## Migration Steps

### Step 1: Update NuGet Package

Remove the old package:
```bash
dotnet remove package ManagedCode.Orleans.StateMachine
```

Add the new package:
```bash
dotnet add package Orleans.StateMachineES
```

### Step 2: Update Namespace Imports

Update all your using statements:

```csharp
// Old
using ManagedCode.Orleans.StateMachine;
using ManagedCode.Orleans.StateMachine.Interfaces;
using ManagedCode.Orleans.StateMachine.Models;
using ManagedCode.Orleans.StateMachine.Extensions;

// New
using Orleans.StateMachineES;
using Orleans.StateMachineES.Interfaces;
using Orleans.StateMachineES.Models;
using Orleans.StateMachineES.Extensions;
```

### Step 3: Choose Your Base Class

You have two options:

#### Option A: Keep Using Non-Event-Sourced (Minimal Changes)

Continue using `StateMachineGrain` for backward compatibility:

```csharp
public class MyGrain : StateMachineGrain<MyState, MyTrigger>, IMyGrain
{
    // No changes needed to your existing code
}
```

#### Option B: Upgrade to Event Sourcing (Recommended)

Migrate to `EventSourcedStateMachineGrain` for event sourcing benefits:

```csharp
// Define your grain state
public class MyGrainState : EventSourcedStateMachineState<MyState>
{
    // Add any additional state properties if needed
}

// Update your grain class
public class MyGrain : EventSourcedStateMachineGrain<MyState, MyTrigger, MyGrainState>, IMyGrain
{
    protected override StateMachine<MyState, MyTrigger> BuildStateMachine()
    {
        // Your existing state machine configuration
    }

    // Optional: Configure event sourcing
    protected override void ConfigureEventSourcing(EventSourcingOptions options)
    {
        options.AutoConfirmEvents = true;
        options.PublishToStream = true;
        options.StreamProvider = "SMS";
        options.EnableIdempotency = true;
    }

    // Optional: Customize version for migration tracking
    protected override string GetStateMachineVersion()
    {
        return "2.0.0"; // Your version
    }
}
```

### Step 4: Update Silo Configuration

Add event sourcing storage provider to your silo configuration:

```csharp
siloBuilder
    .AddLogStorageBasedLogConsistencyProvider()
    .AddStateStorageBasedLogConsistencyProvider()
    .AddMemoryGrainStorage("EventStore") // Or use Azure/ADO.NET storage
    .AddMemoryStreams("SMS"); // Optional: for stream publishing
```

### Step 5: Handle Breaking Changes

#### Interface Changes
The core `IStateMachineGrain<TState, TTrigger>` interface remains the same, so no changes are needed to your grain interfaces.

#### Extension Methods
The Orleans context extension methods remain compatible:
```csharp
// Still works the same
machine.Configure(State.Active)
    .OnEntryOrleansContextAsync(async () => { /* ... */ })
    .OnExitOrleansContextAsync(async () => { /* ... */ });
```

## Event Sourcing Benefits

When you migrate to `EventSourcedStateMachineGrain`, you automatically get:

### 1. Event Persistence
Every state transition is automatically persisted as an event:
```csharp
// This automatically creates a StateTransitionEvent
await grain.FireAsync(MyTrigger.Start);
```

### 2. Event Replay
State is automatically restored from events on grain activation.

### 3. Idempotency
Duplicate triggers are automatically ignored:
```csharp
// Second call with same parameters is ignored (idempotent)
await grain.FireAsync(MyTrigger.Process, "job-123");
await grain.FireAsync(MyTrigger.Process, "job-123"); // No-op
```

### 4. Correlation Tracking
Track related operations across grains:
```csharp
grain.SetCorrelationId("request-123");
await grain.FireAsync(MyTrigger.Process); // Event includes correlation ID
```

### 5. Stream Publishing
Events can be automatically published to Orleans Streams:
```csharp
protected override void ConfigureEventSourcing(EventSourcingOptions options)
{
    options.PublishToStream = true;
    options.StreamProvider = "SMS";
}
```

## Testing Your Migration

### 1. Unit Tests
Your existing unit tests should continue to work. For event-sourced grains, add tests for:
- Event persistence
- Idempotency
- Event replay

### 2. Integration Tests
Test the full event sourcing flow:
```csharp
[Fact]
public async Task EventSourcedGrain_PersistsTransitions()
{
    var grain = cluster.GrainFactory.GetGrain<IMyGrain>("test");
    
    // Fire trigger
    await grain.FireAsync(MyTrigger.Start);
    
    // Deactivate and reactivate grain
    // ... grain deactivation logic ...
    
    // State should be restored from events
    var state = await grain.GetStateAsync();
    Assert.Equal(MyState.Started, state);
}
```

## Rollback Plan

If you need to rollback to the original library:

1. Keep the namespace as `Orleans.StateMachineES` but use `StateMachineGrain` base class
2. Or create type aliases:
```csharp
global using StateMachineGrain = Orleans.StateMachineES.StateMachineGrain;
```

## Common Issues and Solutions

### Issue 1: State Not Persisting
**Solution**: Ensure you've configured a log consistency provider and storage provider.

### Issue 2: Duplicate Events
**Solution**: Enable idempotency in options and ensure dedupe keys are unique.

### Issue 3: Performance Impact
**Solution**: Tune snapshot intervals:
```csharp
options.EnableSnapshots = true;
options.SnapshotInterval = 50; // Snapshot every 50 events
```

## Getting Help

- **Documentation**: See [README.md](../README.md)
- **Examples**: Check the test project for usage examples
- **Issues**: Report issues on [GitHub](https://github.com/mivertowski/Orleans.StateMachineES/issues)

## Acknowledgments

This library is based on the excellent work of the ManagedCode team. We've extended it with event sourcing capabilities while maintaining backward compatibility where possible.