# Orleans.StateMachineES

[![NuGet](https://img.shields.io/nuget/v/ManagedCode.Orleans.StateMachine.svg)](https://www.nuget.org/packages/ManagedCode.Orleans.StateMachine/)
[![.NET](https://github.com/managedcode/Orleans.StateMachineES/actions/workflows/dotnet.yml/badge.svg)](https://github.com/managedcode/Orleans.StateMachineES/actions/workflows/dotnet.yml)
[![CodeQL](https://github.com/managedcode/Orleans.StateMachineES/actions/workflows/codeql-analysis.yml/badge.svg)](https://github.com/managedcode/Orleans.StateMachineES/actions/workflows/codeql-analysis.yml)
[![License](https://img.shields.io/github/license/managedcode/Orleans.StateMachineES)](LICENSE)

A powerful integration of the [Stateless](https://github.com/dotnet-state-machine/stateless) state machine library with [Microsoft Orleans](https://github.com/dotnet/orleans), enabling distributed state machines in Orleans grains with a clean, strongly-typed API.

## Features

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
dotnet add package ManagedCode.Orleans.StateMachine
```

## Quick Start

### 1. Define Your States and Triggers

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

### 2. Create Your State Machine Grain

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

### 3. Use Your State Machine Grain

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

## Contributing

Contributions are welcome! Please feel free to submit a Pull Request.

## License

This project is licensed under the MIT License - see the [LICENSE](LICENSE) file for details.

## References

- [Stateless State Machine](https://github.com/dotnet-state-machine/stateless)
- [Microsoft Orleans](https://github.com/dotnet/orleans)
- [NStateManager](https://github.com/scottctr/NStateManager) - Inspiration for some patterns
