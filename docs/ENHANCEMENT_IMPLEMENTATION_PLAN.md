# Orleans.StateMachineES Enhancement Implementation Plan

**Version**: 2.0.0 (Target)
**Created**: February 2026
**Status**: In Progress

---

## Overview

This document outlines the implementation plan for enhancements identified in the Production Readiness Assessment. Enhancements are prioritized based on user value, complexity, and dependencies.

---

## Phase 1: High Priority Enhancements

### 1.1 Rate Limiting Component

**Priority**: High | **Complexity**: Low | **Estimated Effort**: 1-2 days

**Description**: Add a rate limiter component similar to circuit breaker to protect against burst traffic and ensure fair resource allocation.

**Implementation Details**:

```
Location: src/Orleans.StateMachineES/Composition/Components/
Files:
  - RateLimiterComponent.cs
  - RateLimiterOptions.cs
  - RateLimiterStats.cs
  - RateLimitExceededException.cs
```

**API Design**:
```csharp
public class RateLimiterComponent<TState, TTrigger>
{
    // Token bucket algorithm
    public RateLimiterOptions Options { get; }
    public int AvailableTokens { get; }
    public Task<bool> TryAcquireAsync(TTrigger trigger);
    public RateLimiterStats GetStatistics();
}

public class RateLimiterOptions
{
    public int MaxTransitionsPerSecond { get; set; } = 100;
    public int BurstCapacity { get; set; } = 150;
    public TimeSpan RefillInterval { get; set; } = TimeSpan.FromSeconds(1);
    public bool ThrowWhenExceeded { get; set; } = false;
    public object[]? MonitoredTriggers { get; set; }
    public Action<TTrigger, int>? OnRateLimitExceeded { get; set; }
}
```

**Dependencies**: None

**Tests Required**:
- Token bucket refill logic
- Burst capacity handling
- Concurrent access safety
- Monitored triggers filtering

---

### 1.2 Batch Operations API

**Priority**: High | **Complexity**: Medium | **Estimated Effort**: 2-3 days

**Description**: Enable bulk state transitions for improved performance in high-throughput scenarios.

**Implementation Details**:

```
Location: src/Orleans.StateMachineES/Batch/
Files:
  - IBatchStateMachineService.cs
  - BatchStateMachineService.cs
  - BatchOperationRequest.cs
  - BatchOperationResult.cs
  - BatchOperationOptions.cs
```

**API Design**:
```csharp
public interface IBatchStateMachineService
{
    Task<BatchOperationResult<TState>> FireBatchAsync<TState, TTrigger>(
        IEnumerable<BatchOperationRequest<TTrigger>> requests,
        BatchOperationOptions? options = null);

    Task<BatchOperationResult<TState>> FireBatchAsync<TState, TTrigger>(
        string grainType,
        IEnumerable<(string grainId, TTrigger trigger, object[]? args)> operations,
        BatchOperationOptions? options = null);
}

public class BatchOperationRequest<TTrigger>
{
    public string GrainId { get; set; }
    public TTrigger Trigger { get; set; }
    public object[]? Arguments { get; set; }
    public string? CorrelationId { get; set; }
}

public class BatchOperationResult<TState>
{
    public int TotalOperations { get; }
    public int SuccessCount { get; }
    public int FailureCount { get; }
    public TimeSpan Duration { get; }
    public IReadOnlyList<BatchItemResult<TState>> Results { get; }
}

public class BatchOperationOptions
{
    public int MaxParallelism { get; set; } = 10;
    public bool StopOnFirstFailure { get; set; } = false;
    public TimeSpan Timeout { get; set; } = TimeSpan.FromMinutes(5);
    public bool ContinueOnError { get; set; } = true;
}
```

**Dependencies**: None

**Tests Required**:
- Parallel execution
- Error handling and continuation
- Timeout behavior
- Result aggregation

---

### 1.3 Event Schema Evolution

**Priority**: High | **Complexity**: High | **Estimated Effort**: 3-5 days

**Description**: Handle breaking event changes with automatic upcasting and versioned event schemas.

**Implementation Details**:

```
Location: src/Orleans.StateMachineES/EventSourcing/Evolution/
Files:
  - IEventUpcast.cs
  - EventUpcastRegistry.cs
  - EventVersionAttribute.cs
  - EventEvolutionOptions.cs
  - EventMigrationContext.cs
```

**API Design**:
```csharp
[AttributeUsage(AttributeTargets.Class)]
public class EventVersionAttribute : Attribute
{
    public int Version { get; }
    public Type? PreviousVersionType { get; }
    public EventVersionAttribute(int version, Type? previousVersionType = null);
}

public interface IEventUpcast<TFrom, TTo>
    where TFrom : class
    where TTo : class
{
    TTo Upcast(TFrom oldEvent, EventMigrationContext context);
}

public class EventUpcastRegistry
{
    public void Register<TFrom, TTo>(IEventUpcast<TFrom, TTo> upcaster);
    public void RegisterAutoUpcast<TFrom, TTo>() where TTo : TFrom, new();
    public object? TryUpcast(object oldEvent, Type targetType);
    public object UpcastToLatest(object oldEvent);
}

public class EventEvolutionOptions
{
    public bool AutoRegisterUpcasters { get; set; } = true;
    public bool ThrowOnMissingUpcast { get; set; } = false;
    public bool EnableEventVersionTracking { get; set; } = true;
}
```

**Dependencies**: None

**Tests Required**:
- Single-step upcasting
- Multi-step chain upcasting
- Missing upcast handling
- Concurrent event loading
- Schema validation

---

### 1.4 Persistence Abstraction Layer

**Priority**: High | **Complexity**: High | **Estimated Effort**: 5-7 days

**Description**: Create a pluggable persistence layer supporting multiple storage backends.

**Implementation Details**:

```
Location: src/Orleans.StateMachineES/Persistence/
Files:
  - IStateMachinePersistence.cs
  - IEventStore.cs
  - ISnapshotStore.cs
  - PersistenceOptions.cs
  - InMemoryEventStore.cs (default)
  - Providers/
    - CosmosDbEventStore.cs
    - PostgreSqlEventStore.cs
    - MongoDbEventStore.cs
```

**API Design**:
```csharp
public interface IEventStore<TEvent>
{
    Task AppendAsync(string streamId, IEnumerable<TEvent> events, long expectedVersion);
    Task<IReadOnlyList<TEvent>> ReadAsync(string streamId, long fromVersion, int maxCount);
    Task<long> GetCurrentVersionAsync(string streamId);
    Task DeleteStreamAsync(string streamId);
}

public interface ISnapshotStore<TState>
{
    Task SaveSnapshotAsync(string streamId, TState state, long version);
    Task<(TState? State, long Version)?> LoadSnapshotAsync(string streamId);
    Task DeleteSnapshotsAsync(string streamId);
}

public interface IStateMachinePersistence<TState, TEvent>
{
    IEventStore<TEvent> Events { get; }
    ISnapshotStore<TState> Snapshots { get; }
    Task<TState> LoadAsync(string streamId, Func<TState> factory);
    Task SaveAsync(string streamId, TState state, IEnumerable<TEvent> newEvents);
}

// Extension method for DI
public static class PersistenceExtensions
{
    public static ISiloBuilder UseCosmosDbPersistence(this ISiloBuilder builder, Action<CosmosDbOptions> configure);
    public static ISiloBuilder UsePostgreSqlPersistence(this ISiloBuilder builder, Action<PostgreSqlOptions> configure);
    public static ISiloBuilder UseMongoDbPersistence(this ISiloBuilder builder, Action<MongoDbOptions> configure);
}
```

**Dependencies**:
- Microsoft.Azure.Cosmos (for CosmosDB)
- Npgsql (for PostgreSQL)
- MongoDB.Driver (for MongoDB)

**Tests Required**:
- CRUD operations per provider
- Optimistic concurrency
- Snapshot loading/saving
- Stream deletion
- Provider switching

---

## Phase 2: Medium Priority Enhancements

### 2.1 State Machine Templates

**Priority**: Medium | **Complexity**: Medium | **Estimated Effort**: 2-3 days

**Description**: Pre-built patterns for common workflows.

**Implementation Details**:

```
Location: src/Orleans.StateMachineES/Templates/
Files:
  - ApprovalWorkflowTemplate.cs
  - OrderProcessingTemplate.cs
  - DocumentLifecycleTemplate.cs
  - RetryableOperationTemplate.cs
  - IStateMachineTemplate.cs
```

**API Design**:
```csharp
public interface IStateMachineTemplate<TState, TTrigger>
{
    string TemplateName { get; }
    string Description { get; }
    TState InitialState { get; }
    void Configure(StateMachine<TState, TTrigger>.StateConfiguration config);
    IReadOnlyDictionary<string, object> GetDefaultMetadata();
}

public class ApprovalWorkflowTemplate<TState, TTrigger> : IStateMachineTemplate<TState, TTrigger>
{
    public ApprovalWorkflowTemplate(
        TState pendingState,
        TState approvedState,
        TState rejectedState,
        TTrigger approveTrigger,
        TTrigger rejectTrigger,
        ApprovalWorkflowOptions? options = null);
}
```

---

### 2.2 State History Queries

**Priority**: Medium | **Complexity**: Medium | **Estimated Effort**: 2-3 days

**Description**: Query past states at specific timestamps.

**Implementation Details**:

```
Location: src/Orleans.StateMachineES/EventSourcing/Queries/
Files:
  - IStateHistoryQuery.cs
  - StateHistoryQueryService.cs
  - StateAtTimestamp.cs
  - StateTransitionHistory.cs
```

**API Design**:
```csharp
public interface IStateHistoryQuery<TState, TTrigger>
{
    Task<TState?> GetStateAtAsync(DateTime timestamp);
    Task<IReadOnlyList<StateTransitionHistory<TState, TTrigger>>> GetTransitionHistoryAsync(
        DateTime? from = null, DateTime? to = null, int? limit = null);
    Task<TimeSpan> GetTimeInStateAsync(TState state, DateTime? since = null);
    Task<IReadOnlyDictionary<TState, TimeSpan>> GetStateDistributionAsync(DateTime? from = null, DateTime? to = null);
}
```

---

### 2.3 Multi-Tenancy Support

**Priority**: Medium | **Complexity**: Medium | **Estimated Effort**: 3-4 days

**Description**: Isolated state machines per tenant.

**Implementation Details**:

```
Location: src/Orleans.StateMachineES/MultiTenancy/
Files:
  - ITenantContext.cs
  - TenantStateMachineGrain.cs
  - TenantIsolationOptions.cs
  - TenantEventStore.cs
```

---

### 2.4 Event Replay UI / Admin Dashboard

**Priority**: Medium | **Complexity**: High | **Estimated Effort**: 5-7 days

**Description**: Visual state machine management and event replay debugging tool.

**Implementation Details**:

```
Location: src/Orleans.StateMachineES/Admin/
Files:
  - StateMachineAdminController.cs
  - EventReplayService.cs
  - AdminDashboardOptions.cs
  - wwwroot/ (static files for UI)
```

---

## Phase 3: Low Priority Enhancements

### 3.1 WebSocket Real-time Updates

**Priority**: Low | **Complexity**: Low | **Estimated Effort**: 1-2 days

**Description**: Real-time state change streaming via WebSocket.

---

### 3.2 GraphQL API

**Priority**: Low | **Complexity**: Medium | **Estimated Effort**: 2-3 days

**Description**: Alternative query interface for state machine data.

---

### 3.3 gRPC Support

**Priority**: Low | **Complexity**: Medium | **Estimated Effort**: 2-3 days

**Description**: High-performance client access via gRPC.

---

## Implementation Order

Based on dependencies and value delivery:

```
Week 1:
├── 1.1 Rate Limiting Component (1-2 days) ✓ Start here
├── 1.2 Batch Operations API (2-3 days)
└── Tests & Documentation

Week 2:
├── 1.3 Event Schema Evolution (3-5 days)
└── Tests & Documentation

Week 3:
├── 1.4 Persistence Abstraction - Core (2-3 days)
├── 1.4 Persistence Abstraction - Providers (2-3 days)
└── Tests & Documentation

Week 4:
├── 2.1 State Machine Templates (2-3 days)
├── 2.2 State History Queries (2-3 days)
└── Tests & Documentation

Week 5+:
├── 2.3 Multi-Tenancy Support
├── 2.4 Admin Dashboard
└── Phase 3 items as time permits
```

---

## Version Planning

| Version | Features | Target |
|---------|----------|--------|
| **1.1.0** | Rate Limiter, Batch Operations | Week 1 |
| **1.2.0** | Event Schema Evolution | Week 2 |
| **1.3.0** | Persistence Abstraction | Week 3 |
| **2.0.0** | Templates, History, Multi-tenancy | Week 4+ |

---

## Success Criteria

Each enhancement must meet:

1. **Code Quality**: Zero warnings, follows existing patterns
2. **Test Coverage**: Unit tests for all public APIs
3. **Documentation**: XML docs, usage examples, README updates
4. **Backward Compatibility**: No breaking changes in minor versions
5. **Performance**: No regression in existing benchmarks

---

## Getting Started

Implementation begins with **Rate Limiting Component** as it:
- Has lowest complexity among high-priority items
- Follows established CircuitBreaker pattern
- Has no dependencies on other enhancements
- Provides immediate production value
