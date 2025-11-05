# Architecture

Deep dive into Orleans.StateMachineES design, implementation, and best practices for production deployments.

## Overview

Orleans.StateMachineES is built on three foundational technologies:

1. **Microsoft Orleans** - Distributed actor framework
2. **Stateless** - Hierarchical state machine library
3. **.NET 9.0** - Modern runtime with performance optimizations

This section explores architectural decisions, performance characteristics, and production deployment strategies.

## Architecture Topics

### [Design Decisions](design-decisions.md)
Why Orleans.StateMachineES is designed the way it is.

**Topics covered:**
- Orleans integration strategy
- Async operation patterns
- Event sourcing architecture
- Analyzer infrastructure
- Component composition model

### [Performance Architecture](performance.md)
How Orleans.StateMachineES achieves high performance.

**Key discoveries:**
- **Event sourcing is 30.4% faster** than regular grains
- TriggerParameterCache provides ~100x speedup
- FrozenCollections offer 40%+ faster lookups
- ObjectPool optimization patterns
- ValueTask zero-allocation paths

**Performance metrics:**
- Event-sourced: 5,923 transitions/sec (0.17ms latency)
- Regular: 4,123 transitions/sec (0.24ms latency)
- Parameterized triggers: 100x faster with caching

### [Production Deployment](production.md)
Running Orleans.StateMachineES in production.

**Topics covered:**
- Silo configuration and clustering
- Storage provider selection
- Monitoring and observability
- Circuit breaker patterns
- Error handling strategies
- High availability setup
- Performance tuning
- Capacity planning

### [Security Considerations](security.md)
Security best practices and threat modeling.

**Topics covered:**
- Grain isolation and authorization
- State persistence security
- Event sourcing audit trails
- Input validation
- Dependency security
- Network security
- Compliance considerations

### [Testing Strategy](testing.md)
Comprehensive testing approach.

**Topics covered:**
- Unit testing state machines
- Integration testing with TestCluster
- Testing timers and reminders
- Testing event sourcing replay
- Testing hierarchical states
- Testing distributed sagas
- Performance testing
- Chaos engineering

### [Scalability Patterns](scalability.md)
Scaling state machines to millions of grains.

**Topics covered:**
- Horizontal scaling strategies
- Grain placement optimization
- State partitioning
- Stream processing at scale
- Performance under load
- Resource management

## Design Principles

### 1. Orleans-First Design
Orleans.StateMachineES embraces Orleans patterns:
- Grain lifecycle integration
- Async-native APIs
- Stream support
- Event sourcing with JournaledGrain
- Distributed tracing

### 2. Compile-Time Safety
10 Roslyn analyzers catch issues before runtime:
- Async lambda detection (OSMES001)
- FireAsync in callback prevention (OSMES002)
- Missing configuration detection (OSMES003, OSMES009)
- Design quality checks (OSMES004-008, OSMES010)

### 3. Performance by Default
Optimizations built in:
- Automatic trigger parameter caching
- Thread-safe object pooling
- FrozenCollections for immutable lookup
- ValueTask for synchronous paths
- Event sourcing with AutoConfirmEvents

### 4. Production Ready
Battle-tested components:
- Circuit breaker pattern
- Retry components
- Validation components
- Health checks
- Distributed tracing
- Comprehensive error handling

## Component Architecture

```
┌─────────────────────────────────────────────────────────┐
│                   Your Grain                            │
│  ┌──────────────────────────────────────────────────┐  │
│  │   StateMachineGrain<TState, TTrigger>            │  │
│  │                                                   │  │
│  │  ┌──────────────────────────────────────────┐   │  │
│  │  │  Stateless State Machine                 │   │  │
│  │  │  - State configuration                   │   │  │
│  │  │  - Trigger handling                      │   │  │
│  │  │  - Guard conditions                      │   │  │
│  │  └──────────────────────────────────────────┘   │  │
│  │                                                   │  │
│  │  ┌──────────────────────────────────────────┐   │  │
│  │  │  TriggerParameterCache                   │   │  │
│  │  │  - 100x performance improvement          │   │  │
│  │  └──────────────────────────────────────────┘   │  │
│  │                                                   │  │
│  │  ┌──────────────────────────────────────────┐   │  │
│  │  │  Composition Components (Optional)       │   │  │
│  │  │  - CircuitBreakerComponent               │   │  │
│  │  │  - RetryComponent                        │   │  │
│  │  │  - ValidationComponent                   │   │  │
│  │  └──────────────────────────────────────────┘   │  │
│  └──────────────────────────────────────────────────┘  │
└─────────────────────────────────────────────────────────┘
         ↓                    ↓                  ↓
┌────────────────┐  ┌──────────────────┐  ┌─────────────┐
│ Orleans Grain  │  │ Event Sourcing   │  │ Distributed │
│ Storage        │  │ (JournaledGrain) │  │ Tracing     │
└────────────────┘  └──────────────────┘  └─────────────┘
```

## Technology Stack

### Core Dependencies
- **Microsoft.Orleans.Sdk** 9.1.2 - Orleans actor framework
- **Stateless** 5.17.0 - State machine library
- **.NET 9.0** - Target framework

### Optional Dependencies
- **OpenTelemetry** - Distributed tracing
- **ASP.NET Core** - Health checks
- **System.Text.Json** - Serialization
- **Microsoft.Extensions.Logging** - Logging

### Analyzer Dependencies
- **Microsoft.CodeAnalysis** - Roslyn analyzer infrastructure
- **Microsoft.CodeAnalysis.CSharp** - C# syntax analysis

## Evolution and Roadmap

### Version History
- **v1.0.0** - Initial release with core features
- **v1.0.2** - Async safety, hierarchical states, sagas, versioning
- **v1.0.3** - Performance optimizations, 10 analyzers, circuit breaker
- **v1.0.4** - Event sourcing performance breakthrough, production enhancements

### Future Directions
- Additional visualization formats
- Enhanced saga patterns
- More built-in components
- Performance improvements
- Analyzer enhancements

## Best Practices Summary

### Design
- Keep states focused and meaningful
- Use hierarchical states for related behaviors
- Design for testability
- Enable nullable reference types

### Performance
- Use event sourcing with AutoConfirmEvents
- Enable TriggerParameterCache (automatic in base class)
- Configure appropriate snapshot intervals
- Use ValueTask where applicable

### Production
- Implement circuit breakers for external dependencies
- Configure health checks
- Enable distributed tracing
- Monitor state machine metrics
- Plan for version upgrades

### Testing
- Test state transitions comprehensively
- Verify guard conditions
- Test timeout behavior
- Validate event replay
- Performance test under load

## Additional Resources

- [Production Deployment Guide](production.md)
- [Performance Tuning](performance.md)
- [Security Best Practices](security.md)
- [API Reference](../../api/index.md)
- [Examples](../examples/index.md)
