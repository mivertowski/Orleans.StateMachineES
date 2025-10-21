# CLAUDE.md

This file provides guidance to Claude Code (claude.ai/code) when working with code in this repository.

## Project Overview

Orleans.StateMachineES is a production-ready .NET library that provides state machine functionality integrated with Microsoft Orleans actor framework. It wraps the Stateless state machine library to work seamlessly with Orleans grains, enabling distributed state machine patterns in Orleans applications.

**Current Version**: 1.0.3 (Development)
**Previous Stable**: 1.0.2 (Released September 2025)

### Key Features (v1.0.3)
- **Comprehensive Analyzer Suite**: 10 Roslyn analyzers covering async safety, state configuration, and design patterns
- **Circuit Breaker Pattern**: Built-in resilience component for production scenarios
- **Enhanced Performance**: Trigger parameter caching in base class, optimized object pooling
- **Code Quality**: Consolidated validation logic, thread-safe concurrency patterns
- **Compile-time Safety**: Complete coverage of state machine anti-patterns
- **Runtime Validation**: Prevents dangerous operations like FireAsync within callbacks
- **Event Sourcing**: Complete state history and replay capabilities

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
   - **v1.0.3**: Integrated TriggerParameterCache for ~100x performance improvement on parameterized triggers
   - **v1.0.3**: Consolidated validation logic with `ValidateNotInCallback()` helper
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

### Performance Components (v1.0.3)

#### TriggerParameterCache
**Location**: `Orleans.StateMachineES/Memory/TriggerParameterCache.cs`

High-performance cache for trigger parameters to avoid repeated Stateless configuration:
```csharp
// Caches TriggerWithParameters objects for reuse
public sealed class TriggerParameterCache<TState, TTrigger>
{
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public StateMachine<TState, TTrigger>.TriggerWithParameters<TArg0>
        GetOrCreate<TArg0>(TTrigger trigger);
}
```

**Benefits**:
- ~100x performance improvement for parameterized trigger operations
- Automatic caching in both StateMachineGrain and EventSourcedStateMachineGrain
- AggressiveInlining for minimal overhead

#### ObjectPool Improvements
**Location**: `Orleans.StateMachineES/Memory/ObjectPools.cs`

Fixed critical race condition in pool management:
- **Issue**: Non-atomic check-then-increment could violate maxPoolSize under concurrency
- **Fix**: CompareExchange loop ensures atomic slot reservation
- **Impact**: Thread-safe pool management, no memory leaks

### Roslyn Analyzer Suite (v1.0.3)

The library includes 10 comprehensive analyzers for compile-time safety:

| Diagnostic ID | Severity | Description |
|--------------|----------|-------------|
| **OSMES001** | Warning | Async lambda in state callback (OnEntry, OnExit, etc.) |
| **OSMES002** | Error | FireAsync called within state callback |
| **OSMES003** | Warning | Missing initial state configuration |
| **OSMES004** | Warning | Unreachable state detected |
| **OSMES005** | Info | Multiple entry callbacks on same state |
| **OSMES006** | Warning | State has no trigger handlers (consider OnUnhandledTrigger) |
| **OSMES007** | Warning | Circular state transitions with no exit path |
| **OSMES008** | Warning | Guard condition complexity too high (cyclomatic > 10) |
| **OSMES009** | Error | State machine missing initial state assignment |
| **OSMES010** | Warning | Invalid enum value cast (unsafe numeric cast) |

**AnalyzerHelpers**: Shared utility class (`Analyzers/AnalyzerHelpers.cs`) provides 15+ helper methods for consistent analyzer behavior across all diagnostic rules.

### Circuit Breaker Component (v1.0.3)

**Location**: `Orleans.StateMachineES/Composition/Components/CircuitBreakerComponent.cs`

Production-ready resilience pattern for state machines:

```csharp
// Three-state circuit breaker: Closed → Open → HalfOpen
public class CircuitBreakerComponent<TState, TTrigger> : IComposableStateMachine<TState, TTrigger>
{
    public CircuitBreakerOptions Options { get; }
    public CircuitState State { get; } // Closed, Open, HalfOpen
}

// Configuration
var options = new CircuitBreakerOptions
{
    FailureThreshold = 5,           // Open after 5 consecutive failures
    SuccessThreshold = 2,           // Close after 2 successes in HalfOpen
    OpenDuration = TimeSpan.FromSeconds(30),
    ThrowWhenOpen = true            // Throw CircuitBreakerOpenException
};

var circuitBreaker = new CircuitBreakerComponent<State, Trigger>(options);
```

**Features**:
- Prevents cascading failures in production
- Automatic recovery via HalfOpen state
- Thread-safe state transitions with SemaphoreSlim
- Configurable thresholds and timing
- Exception handling with detailed error messages

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

### Compile-Time Protection (v1.0.3)
The library includes 10 Roslyn analyzers that detect anti-patterns at compile time:

**Async Safety**:
- **OSMES001**: Warning when async lambdas are used in callbacks
- **OSMES002**: Error when FireAsync is called within callbacks

**State Configuration**:
- **OSMES003**: Warning for missing initial state configuration
- **OSMES004**: Warning for unreachable states
- **OSMES006**: Warning for states with no trigger handlers
- **OSMES009**: Error when state machine is missing initial state

**Design Quality**:
- **OSMES005**: Info when multiple entry callbacks exist on same state
- **OSMES007**: Warning for circular transitions with no exit path
- **OSMES008**: Warning for overly complex guard conditions (cyclomatic complexity > 10)
- **OSMES010**: Warning for unsafe enum value casts

See the **Roslyn Analyzer Suite** section for complete details.

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

- **Orleans.StateMachineES** (v1.0.3 dev / v1.0.2 stable): Main library with state machine grain implementations
- **Orleans.StateMachineES.Abstractions** (v1.0.3 dev / v1.0.2 stable): Core interfaces and models
- **Orleans.StateMachineES.Generators** (v1.0.3 dev / v1.0.2 stable): Complete suite of 10 Roslyn analyzers

All packages maintain synchronized versioning.

## Recent Improvements (v1.0.3)

### Phase 1: Code Consolidation & Performance
- **Validation Consolidation**: Extracted `ValidateNotInCallback()` helper, eliminating 60+ lines of duplication
- **ObjectPool Fix**: Fixed race condition using CompareExchange loop for atomic slot reservation
- **TriggerParameterCache**: New high-performance cache component with ~100x speedup
- **Base Class Enhancement**: Moved trigger caching to StateMachineGrain base class for universal performance benefits
- **Analyzer Infrastructure**: Created AnalyzerHelpers utility with 15+ shared methods

### Phase 2: Advanced Analyzers & Resilience
- **Five New Analyzers**: Added OSMES006-010 for comprehensive compile-time safety
- **Circuit Breaker**: Production-ready CircuitBreakerComponent with three-state pattern
- **Complete Coverage**: 10 analyzers covering async safety, state configuration, and design quality
- **Enhanced Diagnostics**: Detailed error messages with actionable guidance

**Total Impact**: +1,784 lines added, -206 lines removed across 13 files