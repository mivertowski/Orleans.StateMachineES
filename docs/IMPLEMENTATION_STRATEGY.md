# Orleans.StateMachineES Implementation Strategy

## Overview
This document outlines the phased implementation strategy for extending the Orleans.StateMachine library with event sourcing and advanced features as specified in plan.md.

**Author**: Michael Ivertowski  
**License**: MIT  
**Based on**: ManagedCode.Orleans.StateMachine (with acknowledgments to original authors)

## Design Principles
1. **Event-First Architecture**: Every state transition is an event
2. **Backward Compatibility**: Maintain compatibility with existing StateMachine usage where possible
3. **Orleans Native**: Leverage Orleans patterns (JournaledGrain, Streams, Reminders)
4. **Type Safety**: Strongly-typed APIs with compile-time safety
5. **Testability**: Comprehensive testing including replay testing
6. **Observability**: Built-in telemetry and audit trails

## Implementation Phases

### Phase 1: Foundation & Namespace Migration (Week 1)
**Goal**: Establish new project structure and basic event sourcing infrastructure

**Tasks**:
1. Rename namespace from `ManagedCode.Orleans.StateMachine` to `Orleans.StateMachineES`
2. Update package metadata (author, version 1.0.0-alpha)
3. Create base classes:
   - `EventSourcedStateMachineGrain<TState, TTrigger, TEvent>` (extends JournaledGrain)
   - `StateTransitionEvent<TState, TTrigger>` base event class
4. Implement core event sourcing:
   - Auto-emit events on transitions
   - Event replay on grain activation
   - Deduplication keys for idempotency
5. Create comprehensive unit tests for event sourcing

**Deliverables**:
- New NuGet package: `Orleans.StateMachineES`
- Basic event-sourced state machine working
- Migration guide from original library

### Phase 2: Event Sourcing Integration (Week 2-3)
**Goal**: Full JournaledGrain integration with outbox pattern

**Tasks**:
1. Implement transition event types:
   ```csharp
   public record StateTransitionEvent<TState, TTrigger>(
       TState FromState,
       TState ToState,
       TTrigger Trigger,
       DateTime Timestamp,
       string? CorrelationId,
       string? DedupeKey
   );
   ```
2. Add automatic event confirmation:
   - Hook into state transitions
   - Call `RaiseEvent` and `ConfirmEvents` automatically
3. Implement outbox pattern:
   - Queue events after confirmation
   - Publish to Orleans Streams
   - Handle failures with retry
4. Create EventSourcedStateMachineGrainState for snapshots
5. Add configuration options:
   ```csharp
   .ConfigureEventSourcing(options => {
       options.AutoConfirmEvents = true;
       options.PublishToStream = true;
       options.StreamProvider = "SMS";
   })
   ```

**Testing**:
- Event replay tests
- Idempotency tests
- Outbox pattern integration tests

### Phase 3: Timers & Temporal Guards (Week 4-5)
**Goal**: Add time-based state management

**Tasks**:
1. Implement state timeouts:
   ```csharp
   .Configure(State.Processing)
       .WithTimeout(TimeSpan.FromMinutes(5), State.Failed)
       .WithRetryTimer(TimeSpan.FromSeconds(30))
   ```
2. Add reminder-based durable timeouts (for >1 minute)
3. Add timer-based non-durable retries (for <1 minute)
4. Implement temporal guards:
   ```csharp
   .PermitIf(Trigger.Expire, State.Expired, 
       () => DateTime.UtcNow > expirationDate)
   ```
5. Create timeout events for audit trail
6. Handle timer/reminder cleanup on state exit

**Testing**:
- Timeout transition tests
- Reminder persistence tests
- Timer cancellation tests

### Phase 4: Hierarchical & Orthogonal States (Week 6-7)
**Goal**: Support complex state compositions

**Tasks**:
1. Implement hierarchical states:
   ```csharp
   machine.Configure(State.Operating)
       .SubstateOf(State.Active)
       .InitialSubstate(State.Operating.Monitoring);
   ```
2. Add orthogonal regions:
   ```csharp
   machine.DefineOrthogonalRegion("EvidenceRetention")
       .WithStates(RetentionState.Collecting, RetentionState.Archived);
   ```
3. Ensure deterministic serialization for event sourcing
4. Update state inspection APIs for hierarchy
5. Implement composite state events

**Testing**:
- Hierarchical transition tests
- Orthogonal region independence tests
- Serialization determinism tests

### Phase 5: Sagas & Compensation (Week 8-9)
**Goal**: Multi-grain orchestration with compensation

**Tasks**:
1. Create saga definition DSL:
   ```csharp
   public class InvoiceSaga : StateMachineSaga<InvoiceState>
   {
       protected override void Configure()
       {
           DefineStep("CreateInvoice")
               .CallGrain<IInvoiceGrain>(g => g.Create())
               .WithCompensation(g => g.Delete());
       }
   }
   ```
2. Implement correlation ID tracking
3. Add compensation execution on failure
4. Create saga state persistence
5. Implement distributed transaction patterns

**Testing**:
- Saga completion tests
- Compensation rollback tests
- Distributed failure scenarios

### Phase 6: Versioning & Migration (Week 10-11)
**Goal**: Support FSM evolution without breaking existing grains

**Tasks**:
1. Implement FSM versioning:
   ```csharp
   [StateMachineVersion("2.0")]
   protected override StateMachine<TState, TTrigger> BuildStateMachine()
   ```
2. Add shadow mode for blue/green testing:
   ```csharp
   grain.EnableShadowMode("2.0");
   ```
3. Create migration hooks:
   ```csharp
   protected override Task MigrateState(string fromVersion, string toVersion)
   ```
4. Implement version compatibility checks
5. Add version telemetry

**Testing**:
- Version migration tests
- Shadow mode comparison tests
- Backward compatibility tests

### Phase 7: Observability & Audit (Week 12)
**Goal**: Comprehensive monitoring and compliance

**Tasks**:
1. OpenTelemetry integration:
   - Spans for state entry/exit/transition
   - Metrics (transition counts, time-in-state)
   - Distributed tracing with correlation IDs
2. Audit logging:
   ```csharp
   public record AuditEntry(
       string Who,
       DateTime When,
       string Action,
       string Reason,
       Dictionary<string, object> Metadata
   );
   ```
3. Add ICFR compliance features
4. Create dashboards templates (Grafana/Prometheus)

**Testing**:
- Telemetry emission tests
- Audit trail completeness tests
- Performance impact tests

### Phase 8: Developer Experience (Week 13-14)
**Goal**: Tools and utilities for development

**Tasks**:
1. State diagram generation:
   ```csharp
   var mermaid = machine.ToMermaidDiagram();
   var plantUml = machine.ToPlantUml();
   ```
2. Replay testing framework:
   ```csharp
   var tester = new StateMachineReplayTester<TState, TTrigger>();
   tester.Feed(eventStream).AssertState(State.Completed);
   ```
3. Roslyn source generator for typed triggers:
   ```yaml
   # statemachine.yaml
   states: [Initial, Processing, Completed]
   triggers: [Start, Process, Complete]
   ```
4. Hot reload support for development
5. Visual Studio/Rider extensions for visualization

**Testing**:
- Diagram generation accuracy
- Replay framework validation
- Source generator output tests

## Testing Strategy

### Unit Tests
- Each phase includes comprehensive unit tests
- Use xUnit with FluentAssertions
- Mock Orleans runtime where needed
- Test coverage target: >90%

### Integration Tests
- Use Orleans TestCluster
- Test grain interactions
- Verify event sourcing persistence
- Test timer/reminder integration

### Performance Tests
- Benchmark state transitions
- Measure event sourcing overhead
- Load test with multiple concurrent grains
- Memory profiling for long-running grains

### Replay Tests
- Capture production event streams
- Replay against new versions
- Validate state consistency
- Test migration scenarios

## Migration Path

### From ManagedCode.Orleans.StateMachine
1. Change namespace imports
2. Inherit from `EventSourcedStateMachineGrain` instead of `StateMachineGrain`
3. Add event type parameter
4. Optional: Enable event sourcing features gradually

### Example Migration:
```csharp
// Before
using ManagedCode.Orleans.StateMachine;
public class DoorGrain : StateMachineGrain<DoorState, DoorTrigger>

// After
using Orleans.StateMachineES;
public class DoorGrain : EventSourcedStateMachineGrain<DoorState, DoorTrigger, DoorEvent>
```

## Release Plan

### v1.0.0-alpha (Phases 1-2)
- Basic event sourcing
- Namespace migration
- Core functionality

### v1.0.0-beta (Phases 3-4)
- Timers and reminders
- Hierarchical states
- Production-ready event sourcing

### v1.0.0 (Phases 5-8)
- Full feature set
- Production tested
- Complete documentation

## Documentation Plan

1. **API Documentation**: XML comments on all public APIs
2. **Tutorials**: Step-by-step guides for each feature
3. **Migration Guide**: From original library
4. **Best Practices**: Patterns and anti-patterns
5. **Sample Applications**: Real-world examples

## Success Metrics

1. **Adoption**: NuGet downloads and GitHub stars
2. **Performance**: <5% overhead vs base Orleans grains
3. **Reliability**: >99.9% state consistency in replay tests
4. **Developer Satisfaction**: Issue resolution time <48h
5. **Test Coverage**: >90% code coverage

## Risk Mitigation

1. **Orleans API Changes**: Abstract Orleans dependencies
2. **Performance Regression**: Continuous benchmarking
3. **Backward Compatibility**: Extensive migration tests
4. **Complexity**: Modular design with feature flags
5. **Maintenance Burden**: Automated testing and CI/CD

## Next Steps

1. Set up new project structure with ivlt namespace
2. Implement Phase 1 foundation
3. Create initial NuGet package
4. Begin Phase 2 event sourcing integration
5. Establish CI/CD pipeline