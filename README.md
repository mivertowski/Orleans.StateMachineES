# Orleans.StateMachineES

[![NuGet](https://img.shields.io/nuget/v/ivlt.Orleans.StateMachineES.svg)](https://www.nuget.org/packages/ivlt.Orleans.StateMachineES/)
[![.NET](https://github.com/mivertowski/Orleans.StateMachineES/actions/workflows/dotnet.yml/badge.svg)](https://github.com/mivertowski/Orleans.StateMachineES/actions/workflows/dotnet.yml)
[![CodeQL](https://github.com/mivertowski/Orleans.StateMachineES/actions/workflows/codeql-analysis.yml/badge.svg)](https://github.com/mivertowski/Orleans.StateMachineES/actions/workflows/codeql-analysis.yml)
[![License](https://img.shields.io/github/license/mivertowski/Orleans.StateMachineES)](LICENSE)

> **Fork Notice**: This is an enhanced fork of the original [ManagedCode.Orleans.StateMachine](https://github.com/managedcode/Orleans.StateMachine) library by the ManagedCode team.

A powerful integration of the [Stateless](https://github.com/dotnet-state-machine/stateless) state machine library with [Microsoft Orleans](https://github.com/dotnet/orleans), now enhanced with **event sourcing capabilities** and advanced distributed state machine features.

## Fork Intention

This fork extends the original ManagedCode.Orleans.StateMachine library with enterprise-grade event sourcing and advanced distributed state machine features. The goal is to provide a comprehensive solution for building event-driven, distributed state machines in Orleans with full audit trails, replay capabilities, and production-ready features.

### New Features in this Fork

- 📚 **Event Sourcing** - First-class event sourcing with Orleans JournaledGrain
- 🔁 **Event Replay** - Automatic state reconstruction from event history
- 🎯 **Idempotency** - Built-in deduplication with LRU cache
- 📷 **Snapshots** - Configurable snapshot intervals for performance
- 🔄 **Correlation Tracking** - Track related events across distributed systems
- 🌊 **Orleans Streams Integration** - Publish state transitions to streams
- ⏰ **Timers & Reminders** - State-driven timeouts with Orleans timers and reminders
- 🔄 **Repeating Actions** - Support for repeating timers with heartbeat patterns
- 🏗️ **Enterprise-Grade** - Production-ready with comprehensive error handling

### Original Features

- 🎯 **Seamless Orleans Integration** - State machines as Orleans grains with full Orleans lifecycle support
- 🔄 **Async-First API** - All operations are async-compatible for Orleans' single-threaded execution model
- 💪 **Strongly Typed** - Generic state and trigger types with compile-time safety
- 🛡️ **Guard Conditions** - Support for guard clauses with detailed validation feedback
- 📊 **Rich State Inspection** - Query permitted triggers, state info, and transition paths
- 🎭 **Parameterized Triggers** - Pass up to 3 typed arguments with triggers
- 🔍 **Comprehensive Metadata** - Export state machine structure and configuration
- ⚡ **Orleans Context Extensions** - Special async extensions for Orleans grain context

## Installation

```bash
dotnet add package ivlt.Orleans.StateMachineES
```

## Quick Start

### Standard State Machine (Original Functionality)

#### 1. Define Your States and Triggers

```csharp
public enum DoorState
{
    Open,
    Closed,
    Locked
}

public enum DoorTrigger
{
    OpenDoor,
    CloseDoor,
    LockDoor,
    UnlockDoor
}
```

#### 2. Create Your State Machine Grain

```csharp
public interface IDoorGrain : IGrainWithStringKey
{
    Task<DoorState> GetCurrentStateAsync();
    Task OpenAsync();
    Task CloseAsync();
    Task LockAsync(string lockCode);
    Task UnlockAsync(string lockCode);
}

public class DoorGrain : StateMachineGrain<DoorState, DoorTrigger>, IDoorGrain
{
    private string? _lockCode;

    protected override StateMachine<DoorState, DoorTrigger> BuildStateMachine()
    {
        var machine = new StateMachine<DoorState, DoorTrigger>(DoorState.Closed);

        machine.Configure(DoorState.Open)
            .Permit(DoorTrigger.CloseDoor, DoorState.Closed);

        machine.Configure(DoorState.Closed)
            .Permit(DoorTrigger.OpenDoor, DoorState.Open)
            .Permit(DoorTrigger.LockDoor, DoorState.Locked);

        machine.Configure(DoorState.Locked)
            .PermitIf(DoorTrigger.UnlockDoor, DoorState.Closed, 
                () => !string.IsNullOrEmpty(_lockCode))
            .OnEntryFrom(DoorTrigger.LockDoor, (string code) => _lockCode = code);

        return machine;
    }

    public Task<DoorState> GetCurrentStateAsync() => GetStateAsync();
    
    public Task OpenAsync() => FireAsync(DoorTrigger.OpenDoor);
    
    public Task CloseAsync() => FireAsync(DoorTrigger.CloseDoor);
    
    public Task LockAsync(string lockCode) => FireAsync(DoorTrigger.LockDoor, lockCode);
    
    public Task UnlockAsync(string lockCode)
    {
        if (lockCode == _lockCode)
        {
            _lockCode = null;
            return FireAsync(DoorTrigger.UnlockDoor);
        }
        throw new InvalidOperationException("Invalid lock code");
    }
}
```

#### 3. Use Your State Machine Grain

```csharp
// In your Orleans client or another grain
var doorGrain = grainFactory.GetGrain<IDoorGrain>("front-door");

// Check current state
var state = await doorGrain.GetCurrentStateAsync();
Console.WriteLine($"Door is {state}");

// Open the door
await doorGrain.OpenAsync();

// Close and lock with a code
await doorGrain.CloseAsync();
await doorGrain.LockAsync("secret-code");

// Try to unlock
await doorGrain.UnlockAsync("secret-code");
```

### Event-Sourced State Machine (New in this Fork)

#### 1. Create an Event-Sourced State Machine Grain

```csharp
using ivlt.Orleans.StateMachineES.EventSourcing;

public class EventSourcedDoorGrain : EventSourcedStateMachineGrain<DoorState, DoorTrigger, DoorGrainState>, IDoorGrain
{
    private string? _lockCode;

    protected override StateMachine<DoorState, DoorTrigger> BuildStateMachine()
    {
        // Same configuration as before
        var machine = new StateMachine<DoorState, DoorTrigger>(DoorState.Closed);
        // ... configure states ...
        return machine;
    }

    protected override void ConfigureEventSourcing(EventSourcingOptions options)
    {
        options.AutoConfirmEvents = true;
        options.EnableIdempotency = true;
        options.EnableSnapshots = true;
        options.SnapshotInterval = 100;
        options.PublishToStream = true;
        options.StreamProvider = "SMS";
    }

    // Implementation methods...
}
```

### Timer-Enabled State Machine (Phase 3)

#### 1. Create a State Machine with Timers and Reminders

```csharp
using ivlt.Orleans.StateMachineES.Timers;

public enum ProcessingState { Idle, Processing, Monitoring, Completed, TimedOut, Failed }
public enum ProcessingTrigger { Start, Complete, Timeout, Heartbeat, Cancel }

public class TimerProcessingGrain : TimerEnabledStateMachineGrain<ProcessingState, ProcessingTrigger, ProcessingGrainState>, IProcessingGrain
{
    protected override StateMachine<ProcessingState, ProcessingTrigger> BuildStateMachine()
    {
        var machine = new StateMachine<ProcessingState, ProcessingTrigger>(ProcessingState.Idle);

        machine.Configure(ProcessingState.Processing)
            .Permit(ProcessingTrigger.Complete, ProcessingState.Completed)
            .Permit(ProcessingTrigger.Timeout, ProcessingState.TimedOut);

        machine.Configure(ProcessingState.Monitoring)
            .Permit(ProcessingTrigger.Cancel, ProcessingState.Idle)
            .Ignore(ProcessingTrigger.Heartbeat); // For repeating heartbeats

        return machine;
    }

    protected override void ConfigureTimeouts()
    {
        // Short timeout with Orleans Timer
        RegisterStateTimeout(ProcessingState.Processing,
            ConfigureTimeout(ProcessingState.Processing)
                .After(TimeSpan.FromSeconds(30))
                .TransitionTo(ProcessingTrigger.Timeout)
                .UseTimer()
                .WithName("ProcessingTimeout")
                .Build());

        // Repeating heartbeat timer
        RegisterStateTimeout(ProcessingState.Monitoring,
            ConfigureTimeout(ProcessingState.Monitoring)
                .After(TimeSpan.FromSeconds(10))
                .TransitionTo(ProcessingTrigger.Heartbeat)
                .UseTimer()
                .Repeat()
                .WithName("MonitoringHeartbeat")
                .Build());

        // Long-running timeout with Orleans Reminder (durable)
        RegisterStateTimeout(ProcessingState.LongRunning,
            ConfigureTimeout(ProcessingState.LongRunning)
                .After(TimeSpan.FromHours(2))
                .TransitionTo(ProcessingTrigger.Timeout)
                .UseDurableReminder() // Survives grain deactivation
                .WithName("LongRunningTimeout")
                .Build());
    }
}
```

#### 2. Configure Orleans Silo for Timers and Event Sourcing

```csharp
siloBuilder
    .AddLogStorageBasedLogConsistencyProvider()
    .AddStateStorageBasedLogConsistencyProvider()
    .AddMemoryGrainStorage("EventStore")
    .AddMemoryStreams("SMS")
    .UseInMemoryReminderService(); // For durable reminders
```

#### 3. Timer Features

- **Automatic Management** - Timers start/stop automatically on state transitions
- **Orleans Timers** - Fast, in-memory timers for short durations (< 5 minutes)
- **Orleans Reminders** - Durable, persistent reminders for long durations (> 5 minutes)
- **Repeating Timers** - Support for periodic actions like heartbeats
- **Fluent Configuration** - Intuitive API for timeout setup
- **Event Sourcing Integration** - Timer events are recorded in event history

#### 4. Benefits of Event Sourcing

- **Complete Audit Trail** - Every state transition is recorded as an event
- **Time Travel** - Replay events to reconstruct state at any point
- **Debugging** - See exactly what happened and when
- **Event Streaming** - Publish transitions to Orleans Streams for real-time processing
- **Idempotency** - Automatic deduplication of duplicate commands

## Advanced Features

### Guard Conditions with Detailed Feedback

```csharp
// Check if a trigger can be fired
var canFire = await grain.CanFireAsync(trigger);

// Get detailed information about why a trigger cannot be fired
var (canFire, unmetConditions) = await grain.CanFireWithUnmetGuardsAsync(trigger);
if (!canFire)
{
    foreach (var condition in unmetConditions)
    {
        Console.WriteLine($"Guard not met: {condition}");
    }
}
```

### Parameterized Triggers

```csharp
// Configure triggers with parameters
machine.Configure(States.Processing)
    .OnEntryFrom(Triggers.StartProcessing, (int jobId, string user) => 
    {
        // Handle parameters
        _currentJobId = jobId;
        _currentUser = user;
    });

// Fire trigger with parameters (up to 3 supported)
await grain.FireAsync(Triggers.StartProcessing, jobId, userName);
```

### State Machine Inspection

```csharp
// Get permitted triggers in current state
var triggers = await grain.GetPermittedTriggersAsync();

// Get detailed trigger information
var details = await grain.GetDetailedPermittedTriggersAsync();

// Check if in a specific state
var isProcessing = await grain.IsInStateAsync(States.Processing);

// Get complete state machine structure
var info = await grain.GetInfoAsync();
```

### State-Based Timeouts and Scheduling

Configure automatic timeouts and periodic actions based on state:

```csharp
protected override void ConfigureTimeouts()
{
    // Basic timeout - transition after specified time
    RegisterStateTimeout(States.Processing,
        ConfigureTimeout(States.Processing)
            .After(TimeSpan.FromMinutes(5))
            .TransitionTo(Triggers.Timeout)
            .Build());

    // Repeating heartbeat timer
    RegisterStateTimeout(States.Active,
        ConfigureTimeout(States.Active)
            .After(TimeSpan.FromSeconds(30))
            .TransitionTo(Triggers.Heartbeat)
            .Repeat()
            .WithName("HealthCheck")
            .Build());

    // Long-running durable reminder
    RegisterStateTimeout(States.LongProcess,
        ConfigureTimeout(States.LongProcess)
            .After(TimeSpan.FromHours(24))
            .TransitionTo(Triggers.Expire)
            .UseDurableReminder() // Survives grain restarts
            .WithName("DailyCleanup")
            .Build());
}

// Timer events are automatically recorded in event history
// Timers are automatically managed on state transitions
```

### Orleans Context Extensions

Use special async extensions for Orleans grain context:

```csharp
protected override StateMachine<State, Trigger> BuildStateMachine()
{
    var machine = new StateMachine<State, Trigger>(State.Initial);
    
    machine.Configure(State.Active)
        .OnEntryOrleansContextAsync(async () => 
        {
            // Async operations with grain context
            await WriteStateAsync();
        })
        .OnExitOrleansContextAsync(async () => 
        {
            await LogTransitionAsync();
        })
        .OnActivateOrleansContextAsync(async () => 
        {
            await InitializeAsync();
        });
        
    return machine;
}
```

## Architecture

The library provides a base `StateMachineGrain<TState, TTrigger>` class that:

- Wraps the Stateless `StateMachine` with async-friendly operations
- Integrates with Orleans grain lifecycle
- Provides thread-safe state management through Orleans' single-threaded execution model
- Supports comprehensive state inspection and metadata export

## Requirements

- .NET 9.0 or higher
- Microsoft Orleans 9.1.2 or higher
- Stateless 5.17.0 or higher

## Roadmap

This fork implements a phased approach to enhance Orleans state machines:

- ✅ **Phase 1 & 2**: Event Sourcing with JournaledGrain (Complete)
- ✅ **Phase 3**: Timers and Reminders (Complete)
- 📋 **Phase 4**: Hierarchical/Nested States
- 📋 **Phase 5**: Distributed Sagas
- 📋 **Phase 6**: State Machine Versioning
- 📋 **Phase 7**: Advanced Observability
- 📋 **Phase 8**: Workflow Orchestration

See [docs/plan.md](docs/plan.md) for detailed roadmap.

## Author

**Michael Ivertowski**  
This fork is maintained by Michael Ivertowski under the MIT License.

## Acknowledgements

This project is a fork of the excellent [ManagedCode.Orleans.StateMachine](https://github.com/managedcode/Orleans.StateMachine) library. Deep respect and thanks to the ManagedCode team for creating the original foundation that made this enhanced version possible.

Special thanks to:
- The **ManagedCode** team for the original Orleans.StateMachine implementation
- The **Stateless** team for the powerful state machine library
- The **Microsoft Orleans** team for the actor framework

## Contributing

Contributions are welcome! Please feel free to submit a Pull Request.

## License

This project is licensed under the MIT License - see the [LICENSE](LICENSE) file for details.

## References

- [Original ManagedCode.Orleans.StateMachine](https://github.com/managedcode/Orleans.StateMachine) - The foundation for this fork
- [Stateless State Machine](https://github.com/dotnet-state-machine/stateless)
- [Microsoft Orleans](https://github.com/dotnet/orleans)
- [Orleans Event Sourcing](https://learn.microsoft.com/en-us/dotnet/orleans/grains/event-sourcing/)
- [NStateManager](https://github.com/scottctr/NStateManager) - Inspiration for some patterns
