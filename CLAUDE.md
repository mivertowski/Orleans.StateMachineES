# CLAUDE.md

This file provides guidance to Claude Code (claude.ai/code) when working with code in this repository.

## Project Overview

Orleans.StateMachineES is a production-ready .NET library that provides state machine functionality integrated with Microsoft Orleans actor framework. It wraps the Stateless state machine library to work seamlessly with Orleans grains, enabling distributed state machine patterns in Orleans applications.

**Current Version**: 1.0.2 (Released September 2025)

### Key Features (v1.0.2)
- **Compile-time Safety**: Roslyn analyzers detect async issues in state callbacks
- **Runtime Validation**: Prevents dangerous operations like FireAsync within callbacks
- **Enhanced Error Messages**: Detailed context for debugging state machine issues
- **Event Sourcing**: Complete state history and replay capabilities
- **Performance Optimized**: ValueTask support, object pooling, memory optimizations

## Build and Development Commands

### Build Commands
```bash
# Restore dependencies
dotnet restore

# Build the solution
dotnet build

# Build without restore
dotnet build --no-restore

# Build in Release mode
dotnet build -c Release
```

### Test Commands
```bash
# Run all tests
dotnet test

# Run tests with verbose output
dotnet test --verbosity normal

# Run tests with code coverage
dotnet test /p:CollectCoverage=true /p:CoverletOutputFormat=lcov /p:CoverletOutput=ManagedCode.Orleans.StateMachine.Tests/lcov.info

# Run a specific test
dotnet test --filter "FullyQualifiedName~StateMachineGrainTests"
```

### Package Commands
```bash
# Create NuGet package
dotnet pack -c Release

# Push to NuGet (requires API key)
dotnet nuget push bin/Release/*.nupkg --source https://api.nuget.org/v3/index.json
```

## Architecture and Core Components

### Project Structure
- **Orleans.StateMachineES**: Main library project containing state machine grain implementation
- **Orleans.StateMachineES.Abstractions**: Core interfaces and models (v1.0.2)
- **Orleans.StateMachineES.Generators**: Roslyn analyzers for compile-time safety (v1.0.2)
- **Orleans.StateMachineES.Tests**: Test project with comprehensive unit and integration tests
- **Orleans.StateMachineES.Benchmarks**: Performance benchmarking suite

### Core Abstractions

1. **StateMachineGrain<TState, TTrigger>** (`Orleans.StateMachineES/StateMachineGrain.cs`): 
   - Base abstract grain class that all state machine grains inherit from
   - Wraps Stateless `StateMachine<TState, TTrigger>` to provide Orleans-compatible async methods
   - Handles grain activation lifecycle and state machine initialization
   - **v1.0.2**: Added runtime validation to prevent FireAsync within callbacks
   - **v1.0.2**: Thread-local state tracking for callback execution context
   - Provides comprehensive async API for state queries, trigger firing, and metadata retrieval

2. **IStateMachineGrain<TState, TTrigger>** (`Orleans.StateMachineES.Abstractions/Interfaces/IStateMachineGrain.cs`):
   - Core interface defining all state machine operations
   - Supports parameterized triggers (up to 3 arguments)
   - Provides state inspection, guard validation, and permitted trigger queries
   - Includes activation/deactivation lifecycle methods

3. **EventSourcedStateMachineGrain<TState, TTrigger>** (`Orleans.StateMachineES/EventSourcing/EventSourcedStateMachineGrain.cs`):
   - Extends StateMachineGrain with event sourcing capabilities
   - **v1.0.2**: Enhanced error messages with detailed replay context
   - Supports state replay from event history

### Key Implementation Patterns

1. **State Machine Creation**: Derived grains must implement `BuildStateMachine()` to configure states, transitions, and guards
2. **Async Wrapper Pattern**: All Stateless operations are wrapped in async methods for Orleans compatibility
3. **Parameterized Triggers**: Support for passing up to 3 typed arguments with triggers using `SetTriggerParameters`
4. **Guard Conditions**: Methods like `CanFireWithUnmetGuardsAsync` provide detailed guard validation feedback

### Testing Infrastructure

The test project uses:
- **TestClusterApplication**: Orleans test cluster setup for integration testing
- **xUnit with FluentAssertions**: Test framework and assertion library
- **Test Grain Examples**: `TestGrain`, `TestStatelessGrain`, `TestOrleansContextGrain` demonstrate different usage patterns

### Dependencies
- **Microsoft.Orleans.Sdk** (9.1.2): Orleans actor framework
- **Stateless** (5.17.0): Core state machine library
- **.NET 9.0**: Target framework with nullable reference types enabled

## Key Development Considerations

1. **Grain Activation**: State machines are built during grain activation via `OnActivateAsync`
2. **Thread Safety**: Orleans single-threaded execution model ensures state machine operations are thread-safe
3. **Serialization**: Uses Orleans serialization for state machine metadata (OrleansStateMachineInfo, OrleansStateInfo)
4. **Error Handling**: Invalid state transitions throw `InvalidOperationException` with descriptive messages

## Async Operation Limitations (Critical)

### ⚠️ Important: Stateless Library Limitations
The underlying Stateless library does **NOT** support async operations in state callbacks (OnEntry, OnExit, OnEntryFrom, OnExitFrom). This is a fundamental limitation of the library.

### Compile-Time Protection (v1.0.2)
Two Roslyn analyzers help prevent async issues:
- **OSMES001**: Warning when async lambdas are used in callbacks
- **OSMES002**: Error when FireAsync is called within callbacks

### Runtime Protection (v1.0.2)
- Thread-local state tracking prevents FireAsync calls from within callbacks
- Clear error messages guide developers to correct patterns

### Correct Pattern
```csharp
// ✅ CORRECT: Use grain methods for async operations
public async Task StartProcessingAsync()
{
    await FireAsync(Trigger.Start);
    // Async work happens here, AFTER the transition
    await PerformAsyncWork();
}

// State configuration
Configure(State.Processing)
    .OnEntry(() => Console.WriteLine("Entered Processing"))  // Sync only
    .Permit(Trigger.Start, State.Active);
```

For detailed guidance, see: `docs/ASYNC_PATTERNS.md`

## NuGet Packages

- **Orleans.StateMachineES** (v1.0.2): Main library
- **Orleans.StateMachineES.Abstractions** (v1.0.2): Core interfaces and models
- **Orleans.StateMachineES.Generators** (v1.0.2): Roslyn analyzers

All packages maintain synchronized versioning.