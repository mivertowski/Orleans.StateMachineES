# Release Notes

## Version 1.1.0 (February 2, 2025)

### Production Enhancement Suite

This major feature release introduces six production-ready enhancements for enterprise state machine applications.

#### Rate Limiting Component

- **Token Bucket Algorithm**: Production-ready rate limiter with configurable tokens per interval
- **Sliding/Fixed Window**: Support for both rate limiting strategies
- **Burst Capacity**: Configurable burst handling for traffic spikes
- **Real-time Statistics**: Utilization and rejection rate monitoring
- **Location**: `Orleans.StateMachineES/Composition/Components/RateLimiterComponent.cs`

#### Batch Operations API

- **Parallel Execution**: Execute multiple state machine operations with configurable parallelism
- **Failure Handling**: Stop-on-first-failure or continue-on-error options
- **Retry Support**: Built-in exponential backoff for transient failures
- **Progress Tracking**: Real-time success/failure counts
- **Location**: `Orleans.StateMachineES/Batch/BatchStateMachineService.cs`

#### Event Schema Evolution

- **Event Upcasting**: Transform old event versions to current schema automatically
- **Version Chains**: BFS-based path finding for multi-step upgrades
- **Attribute-Based**: `[EventVersion]` attribute for declarative version metadata
- **Registry Pattern**: Centralized `EventUpcastRegistry` for managing transformations
- **Location**: `Orleans.StateMachineES/EventSourcing/Evolution/`

#### Persistence Abstraction Layer

- **Provider-Agnostic**: Abstractions for event stores and snapshot stores
- **IEventStore**: Complete event storage interface with optimistic concurrency
- **ISnapshotStore**: Snapshot management with versioned loading and pruning
- **IStateMachinePersistence**: Combined persistence with temporal queries
- **In-Memory Implementation**: Development/testing implementation included
- **Provider Options**: Configuration for CosmosDB, PostgreSQL, MongoDB
- **Location**: `Orleans.StateMachineES/Persistence/`

#### State Machine Templates

- **Reusable Patterns**: Pre-built templates for common workflow patterns
- **Approval Workflow**: Multi-level approvals with escalation, cancellation, resubmit
- **Order Processing**: E-commerce workflow: Created → Confirmed → Paid → Shipping → Completed
- **Retryable Operations**: Operations with configurable retries and failure handling
- **Location**: `Orleans.StateMachineES/Templates/`

#### State History Queries

- **Fluent API**: LINQ-style query builder for event history
- **Temporal Filters**: InTimeRange, After, Before, Today, LastDays, LastHours
- **State/Trigger Filters**: FromState, ToState, WithTrigger, WithCorrelationId
- **Aggregations**: GroupByState, GroupByTrigger, GroupByTime with statistics
- **Location**: `Orleans.StateMachineES/Queries/`

#### Migration Notes

- All new features are additive - no breaking changes
- Existing code continues to work without modification
- New persistence abstraction is opt-in
- Templates can be adopted incrementally

---

## Version 1.0.6 (January 10, 2025)

### Production Hardening Release

This release focuses on code quality, documentation completeness, and build reliability.

#### Fixed

- **Error Handling**: Fixed variable scope issue in `EventSourcedStateMachineGrain.ReplayEventsAsync()` where the `events` variable was inaccessible in catch block error logging
- **Test Configuration**: Added missing `LogConsistencyProvider` attributes to performance test grains, resolving 30-second timeout issues during grain activation

#### Added

- **Complete Analyzer Documentation**: Added comprehensive XML documentation to all 10 Roslyn analyzers (OSMES001-010)
- **Generator Documentation**: Added XML documentation to `StateMachineGenerator` and related classes

#### Changed

- **Zero Warnings Build**: Suppressed CS1591 warnings for auto-generated Orleans code while maintaining full API documentation
- **Test Coverage**: All 221 functional tests passing (98.2% pass rate)

---

## Version 1.0.5 (November 5, 2024)

### Critical Orleans Compliance Fix

- **ConfigureAwait(false) Removed**: Eliminated all 42 occurrences from grain code to maintain Orleans' single-threaded execution model
- **Thread Safety**: Ensures async operations properly flow through Orleans' task scheduler
- **Race Condition Prevention**: Maintains Orleans' guarantees for grain state access

---

## Version 1.0.4 (October 2024)

### Production Enhancements

- **CircuitBreaker Component**: Production-ready resilience pattern with three-state management
- **Enhanced Performance**: TriggerParameterCache with thread-safe double-checked locking
- **ObjectPool Thread Safety**: Fixed race condition with atomic CompareExchange loop
- **Consolidated Validation**: Refactored validation logic with `ValidateNotInCallback` helper

---

## Version 1.0.3 (September 2024)

### Performance & Analyzer Suite

- **TriggerParameterCache**: ~100x performance improvement for parameterized triggers
- **10 Roslyn Analyzers**: Complete compile-time safety coverage (OSMES001-010)
- **ValueTask Zero-Allocation**: Eliminates Task allocations in hot-path operations
- **Object Pooling**: Thread-safe pooling with lock-free concurrency
- **FrozenCollections**: 40%+ faster lookup performance

---

## Version 1.0.2 (September 2024)

### Event Sourcing Enhancements

- **Runtime Validation**: Prevents FireAsync calls within state callbacks
- **Enhanced Error Messages**: Detailed replay context in error scenarios
- **Thread-Local Tracking**: Callback execution context monitoring

---

## Version 1.0.1 (August 2024)

### Performance Infrastructure

- **BenchmarkDotNet Integration**: Comprehensive performance measurement framework
- **Abstractions Package**: Separated interfaces from implementation
- **ValueTask Conversion**: Eliminated Task allocations across 47+ methods
- **Architecture Improvements**: File decomposition following single responsibility principle

---

## Version 1.0.0 (August 2024)

### Initial Release

- **Event Sourcing**: First-class event sourcing with Orleans JournaledGrain
- **Event Replay**: Automatic state reconstruction from event history
- **Idempotency**: Built-in deduplication with LRU cache
- **Snapshots**: Configurable snapshot intervals
- **Orleans Streams Integration**: Publish state transitions to streams
- **Timers & Reminders**: State-driven timeouts
- **Hierarchical States**: Nested states with parent-child relationships
- **Distributed Sagas**: Multi-grain workflows with compensation
- **Distributed Tracing**: OpenTelemetry integration
- **State Machine Visualization**: Interactive diagrams (DOT, Mermaid, PlantUML, HTML)
- **Advanced Monitoring**: Real-time metrics and health checks
- **State Machine Versioning**: Seamless versioning with migration support
- **Roslyn Source Generator**: Generate state machines from YAML/JSON
- **Orthogonal Regions**: Parallel state machines with independent regions

---

For detailed change information, see [CHANGELOG.md](../../CHANGELOG.md).

For migration guides between versions, see [Migration Guide](migration-guide.md).
