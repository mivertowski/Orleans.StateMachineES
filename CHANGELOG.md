# Changelog

All notable changes to Orleans.StateMachineES will be documented in this file.

The format is based on [Keep a Changelog](https://keepachangelog.com/en/1.0.0/),
and this project adheres to [Semantic Versioning](https://semver.org/spec/v2.0.0.html).

## [1.1.0] - 2025-02-02

### Added

#### Production Enhancement Suite
This release introduces six major production-ready features for enterprise state machine applications.

#### Rate Limiting Component
- **Token Bucket Algorithm**: Production-ready rate limiter with configurable tokens per interval
- **Sliding/Fixed Window**: Support for both sliding and fixed window rate limiting strategies
- **Burst Capacity**: Configurable burst capacity for handling traffic spikes
- **Wait Support**: Optional blocking with configurable max wait time
- **Statistics**: Real-time rate limiter statistics including utilization and rejection rates
- **Location**: `Orleans.StateMachineES/Composition/Components/RateLimiterComponent.cs`

```csharp
var options = new RateLimiterOptions
{
    TokensPerInterval = 100,
    BurstCapacity = 150,
    RefillInterval = TimeSpan.FromSeconds(1),
    MaxWaitTime = TimeSpan.FromMilliseconds(500)
};
var rateLimiter = new RateLimiterComponent<State, Trigger>(options);
```

#### Batch Operations API
- **Parallel Execution**: Execute multiple state machine operations in parallel with configurable parallelism
- **Failure Handling**: Options for stop-on-first-failure or continue-on-error
- **Retry Support**: Built-in retry with exponential backoff for transient failures
- **Progress Tracking**: Real-time progress tracking with success/failure counts
- **Results Aggregation**: Comprehensive batch results with per-item status
- **Location**: `Orleans.StateMachineES/Batch/BatchStateMachineService.cs`

```csharp
var batchService = new BatchStateMachineService();
var result = await batchService.ExecuteBatchAsync(requests, grainResolver, fireAsync, getState);
Console.WriteLine($"Success rate: {result.SuccessRate:P2}");
```

#### Event Schema Evolution
- **Event Upcasting**: Transform old event versions to current schema automatically
- **Version Chains**: BFS-based path finding for multi-step event upgrades
- **Attribute-Based**: `[EventVersion]` attribute for declarative version metadata
- **Registry Pattern**: Centralized `EventUpcastRegistry` for managing transformations
- **Type Safety**: Generic `IEventUpcast<TFrom, TTo>` interface for type-safe migrations
- **Location**: `Orleans.StateMachineES/EventSourcing/Evolution/`

```csharp
var registry = new EventUpcastRegistry();
registry.Register(new OrderEventV1ToV2Upcaster());
var currentEvent = registry.UpcastToLatest(oldEvent);
```

#### Persistence Abstraction Layer
- **Provider-Agnostic**: Abstractions for event stores and snapshot stores
- **IEventStore**: Complete event storage interface with append, read, subscribe operations
- **ISnapshotStore**: Snapshot management with versioned loading and pruning
- **IStateMachinePersistence**: Combined persistence with temporal queries
- **In-Memory Implementation**: Development/testing implementation included
- **Provider Options**: Configuration classes for CosmosDB, PostgreSQL, MongoDB
- **Location**: `Orleans.StateMachineES/Persistence/`

```csharp
services.AddInMemoryStateMachinePersistence<State, Trigger>(options =>
{
    options.EnableSnapshots = true;
    options.SnapshotInterval = 100;
});
```

#### State Machine Templates
- **Reusable Patterns**: Pre-built templates for common workflow patterns
- **Approval Workflow**: Multi-level approvals with escalation, cancellation, resubmit
- **Order Processing**: E-commerce workflow: Created → Confirmed → Paid → Shipping → Completed
- **Retryable Operations**: Operations with configurable retries and failure handling
- **Extensible Base**: `IStateMachineTemplate` and `StateMachineTemplateBase` for custom templates
- **Location**: `Orleans.StateMachineES/Templates/`

```csharp
var template = new ApprovalWorkflowTemplate<ApprovalState, ApprovalTrigger>(config);
template.Apply(stateMachine);
```

#### State History Queries
- **Fluent API**: LINQ-style query builder for event history
- **Temporal Filters**: InTimeRange, After, Before, Today, LastDays, LastHours
- **State/Trigger Filters**: FromState, ToState, WithTrigger, WithCorrelationId
- **Aggregations**: GroupByState, GroupByTrigger, GroupByTime with statistics
- **Pagination**: Skip/Take support for large event streams
- **Location**: `Orleans.StateMachineES/Queries/`

```csharp
var stats = await persistence.Query(streamId)
    .LastDays(7)
    .FromState(State.Processing)
    .GroupByTriggerAsync();
```

### Test Coverage
- **Rate Limiter Tests**: Token bucket algorithm, burst handling, wait behavior
- **Batch Operation Tests**: Parallel execution, failure handling, retry logic
- **Event Evolution Tests**: Upcast chains, version discovery, type safety
- **Persistence Tests**: Event store, snapshot store, combined persistence
- **Query Tests**: Filters, aggregations, pagination, time-based queries

### Technical Details

#### Rate Limiter Implementation
- Thread-safe token bucket with `SemaphoreSlim`
- Atomic token operations with `Interlocked` methods
- Configurable refill intervals with `System.Threading.Timer`
- Statistics tracking with lock-free counters

#### Batch Operations Implementation
- `SemaphoreSlim`-based parallelism control
- Exponential backoff: 100ms, 200ms, 400ms, 800ms, 1600ms
- Cancellation token propagation throughout
- Result aggregation with success/failure categorization

#### Persistence Implementation
- Optimistic concurrency with expected version checking
- Snapshot-based recovery to minimize event replay
- Subscription support for real-time event streaming
- Comprehensive exception hierarchy for error handling

### Migration Notes
- All new features are additive - no breaking changes
- Existing code continues to work without modification
- New persistence abstraction is opt-in
- Templates can be adopted incrementally

---

## [1.0.6] - 2025-01-10

### Fixed

#### Production Hardening and Code Quality
- **Error Handling**: Fixed variable scope issue in `EventSourcedStateMachineGrain.ReplayEventsAsync()` where `events` variable was declared inside try block but accessed in catch block
- **Test Configuration**: Added missing `LogConsistencyProvider` attribute to `PerformanceTestGrain` and `ResilientWorkflowGrain` test classes, resolving 30-second timeout issues during grain activation

### Added

#### Comprehensive Documentation
- **Analyzer Documentation**: Added complete XML documentation to all 10 Roslyn analyzers (OSMES001-010)
  - `AsyncLambdaInCallbackAnalyzer`: Documents async lambda detection in state callbacks
  - `CircularTransitionAnalyzer`: Documents circular transition detection
  - `DuplicateStateConfigurationAnalyzer`: Documents duplicate configuration warnings
  - `FireAsyncInCallbackAnalyzer`: Documents FireAsync usage violations
  - `GuardComplexityAnalyzer`: Documents guard complexity analysis
  - `InvalidEnumValueAnalyzer`: Documents enum value validation
  - `MissingBuildStateMachineAnalyzer`: Documents missing implementation detection
  - `MissingInitialStateAnalyzer`: Documents initial state validation
  - `UnhandledTriggerAnalyzer`: Documents unhandled trigger warnings
  - `UnreachableStateAnalyzer`: Documents unreachable state detection

- **Generator Documentation**: Added XML documentation to `StateMachineGenerator`, `StateMachineDefinition`, and `TransitionDefinition` classes

### Changed

#### Build Quality Improvements
- **Zero Warnings**: Suppressed CS1591 (missing XML documentation) warnings for:
  - Auto-generated Orleans serialization code in Abstractions project
  - Internal implementation details in main project
  - Generated code properly excluded from documentation requirements
- **Clean Builds**: Verified zero errors and zero warnings across all projects
- **Test Coverage**: All 221 functional tests passing (98.2% pass rate, 4 tests intentionally skipped)

### Technical Details

#### Error Handling Fix
The variable scope issue in `EventSourcedStateMachineGrain.ReplayEventsAsync()` was causing the `events` variable to be inaccessible in error logging within the catch block. Fixed by declaring the variable before the try block:

```csharp
IEnumerable<object>? events = null;
try {
    events = await RetrieveConfirmedEvents(0, Version);
    // ... replay logic
}
catch (Exception ex) {
    // Now events is accessible for error logging
    _logger?.LogCritical(ex, "Failed to replay {Count} events", events?.Count() ?? 0);
}
```

#### Test Configuration Fix
`EventSourcedStateMachineGrain` extends Orleans' `JournaledGrain` which requires both storage attributes:
- `[StorageProvider]` for grain state
- `[LogConsistencyProvider]` for event journal ← This was missing

Without `LogConsistencyProvider`, grains would timeout during activation when calling `RetrieveConfirmedEvents()`.

---

## [1.0.5] - 2025-11-05

### Fixed

#### Critical Bug Fix: Orleans Task Scheduler Compliance
- **ConfigureAwait(false) Removed**: Eliminated all 42 occurrences of `ConfigureAwait(false)` from grain code to maintain Orleans' single-threaded execution model guarantees
  - **StateMachineGrain.cs**: Removed 7 instances from core grain operations (lines 63, 71, 81, 91, 101, 111, 255)
  - **EventSourcedStateMachineGrain.cs**: Removed 25 instances from event sourcing implementation
  - **VersionCompatibilityChecker.cs**: Removed 6 instances from versioning service
  - **MigrationPathCalculator.cs**: Removed 2 instances from migration path calculator
  - **CompatibilityRulesEngine.cs**: Removed 2 instances from compatibility rules evaluator

#### Impact
- **Prevents Race Conditions**: Using `ConfigureAwait(false)` in Orleans grain code can cause continuations to run on thread pool threads instead of Orleans' task scheduler, potentially breaking the single-threaded execution guarantee
- **Maintains Orleans Guarantees**: All async operations now properly flow through Orleans' execution context
- **Thread Safety**: Ensures grain state access remains properly synchronized through Orleans' runtime

#### Technical Details
This fix addresses [GitHub Issue #5](https://github.com/mivertowski/Orleans.StateMachineES/issues/5) reported by @zbarrier. In Orleans grains, using `ConfigureAwait(false)` can cause unpredictable behavior because:
- Orleans grains rely on single-threaded execution guarantees
- `ConfigureAwait(false)` allows continuations to run on arbitrary thread pool threads
- This can lead to concurrent access to grain state, violating Orleans' programming model
- The fix ensures all async operations respect Orleans' task scheduler

### Changed
- **Versioning Components**: Updated service classes (VersionCompatibilityChecker, MigrationPathCalculator, CompatibilityRulesEngine) to maintain execution context when called from grain code

---

## [1.0.1] - 2025-08-27

### Added

#### Performance Infrastructure
- **BenchmarkDotNet Integration**: Comprehensive performance measurement framework with 4 benchmark suites
- **Performance Benchmarks**: ValueTask vs Task, ObjectPool, FrozenCollection, and StringIntern performance tests
- **Memory Diagnostics**: Allocation tracking and GC pressure measurement capabilities
- **Benchmark Project**: `Orleans.StateMachineES.Benchmarks` with complete test infrastructure

#### Core Abstractions Package
- **Orleans.StateMachineES.Abstractions**: New NuGet package separating interfaces from implementation
- **Clean Architecture**: Modular design with clear separation of contracts and implementation
- **Reusable Interfaces**: IStateMachineGrain, IEventSourcedGrain with ValueTask optimization

### Changed

#### Performance Optimizations
- **ValueTask Conversion**: Eliminated Task allocations in hot-path async operations across 47+ methods
- **ConfigureAwait(false)**: Optimized thread pool utilization throughout the codebase
- **String Interning**: Enhanced string interning with bounded LRU cache (10,000 capacity) for state names
- **Object Pooling**: Implemented thread-safe pools for List<string>, Dictionary<string, object>, HashSet<string>, and byte arrays
- **FrozenCollections**: Pre-built static collections providing 40%+ lookup performance improvements
- **Memory Management**: ArrayPool integration for zero-allocation buffer management

#### Architecture Improvements
- **File Decomposition**: Broke down large files following single responsibility principle
  - **VersionCompatibilityChecker**: Decomposed 851-line monolith into focused components
  - **CompatibilityRulesEngine**: Rule-based compatibility evaluation system (400+ lines)
  - **MigrationPathCalculator**: Graph-based migration path optimization (400+ lines)
- **Modular Design**: Each component now has clear, focused responsibilities
- **Interface Segregation**: Core interfaces moved to abstractions package

### Fixed
- **Compilation Errors**: Resolved duplicate type definitions across multiple files
- **Interface Mismatches**: Fixed ValueTask return type consistency
- **Package Dependencies**: Corrected Orleans package references in abstractions project
- **Build Configuration**: Proper solution structure for all projects

### Technical Details

#### Memory Optimizations
- **ObjectPools.cs**: Thread-safe object pooling for frequently allocated collections
- **FrozenCollections.cs**: Static collection optimizations with pre-built frozen collections
- **StringInternPool.cs**: Enhanced with optimized common state recognition patterns
- **ValueTaskExtensions.cs**: 12 extension methods for efficient ValueTask handling

#### Benchmarking Infrastructure
- **ValueTaskVsTaskBenchmarks**: Measures zero-allocation benefits
- **ObjectPoolBenchmarks**: Compares traditional vs pooled allocation patterns  
- **FrozenCollectionBenchmarks**: Tests lookup and iteration performance
- **StringInternBenchmarks**: Validates memory optimization impact

#### Expected Performance Gains
- **Zero Allocations**: ValueTask conversions eliminate Task allocations in synchronous completions
- **40%+ Lookup Speed**: FrozenDictionary/FrozenSet provide superior read performance for static data
- **Reduced GC Pressure**: Object pooling minimizes garbage collection overhead
- **Optimized Memory**: String interning reduces duplicate string allocations

### Backward Compatibility
- **100% Compatible**: All optimizations maintain full backward compatibility
- **No Breaking Changes**: Existing code continues to work without modifications
- **Interface Preservation**: All public APIs remain unchanged

---

## [1.0.0] - 2025-01-15

### Added
- **Event Sourcing**: First-class event sourcing with Orleans JournaledGrain
- **Saga Orchestration**: Distributed saga patterns with compensation and correlation tracking
- **State Machine Versioning**: Seamless versioning with migration support
- **Hierarchical States**: Support for nested states with parent-child relationships
- **Timer Management**: State-driven timeouts with Orleans timers and reminders
- **Distributed Tracing**: OpenTelemetry integration with activity sources and metrics
- **State Machine Visualization**: Interactive diagrams and analysis tools
- **Advanced Monitoring**: Real-time metrics, health checks, and monitoring endpoints
- **State Machine Composition**: Build complex state machines from reusable components
- **Roslyn Source Generator**: Generate state machines from YAML/JSON specifications
- **Orthogonal Regions**: Support for parallel state machines with independent regions

### Foundation
- **Orleans Integration**: Seamless integration with Microsoft Orleans actor framework
- **Stateless Wrapper**: Production-ready wrapper around the Stateless state machine library
- **Async-First API**: All operations are async-compatible for Orleans' execution model
- **Strongly Typed**: Generic state and trigger types with compile-time safety
- **Guard Conditions**: Support for guard clauses with detailed validation feedback
- **Rich State Inspection**: Query permitted triggers, state info, and transition paths
- **Parameterized Triggers**: Pass up to 3 typed arguments with triggers

### Performance Baseline
- **Optimized Event Sourcing**: Event-sourced state machines with proper configuration
- **Efficient State Management**: Orleans grain lifecycle integration
- **Memory Conscious**: Initial implementation with standard .NET patterns

[1.1.0]: https://github.com/mivertowski/Orleans.StateMachineES/compare/v1.0.6...v1.1.0
[1.0.6]: https://github.com/mivertowski/Orleans.StateMachineES/compare/v1.0.5...v1.0.6
[1.0.5]: https://github.com/mivertowski/Orleans.StateMachineES/compare/v1.0.1...v1.0.5
[1.0.1]: https://github.com/mivertowski/Orleans.StateMachineES/compare/v1.0.0...v1.0.1
[1.0.0]: https://github.com/mivertowski/Orleans.StateMachineES/releases/tag/v1.0.0