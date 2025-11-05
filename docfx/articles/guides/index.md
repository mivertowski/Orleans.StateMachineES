# Guides

Comprehensive guides for mastering Orleans.StateMachineES features and patterns.

## Core Guides

### [Async Patterns](async-patterns.md)
**Critical reading** - Understanding async operations and Stateless library limitations.

Learn the correct patterns for handling async operations in state machine grains, common mistakes to avoid, and compile-time safety with OSMES001 and OSMES002 analyzers.

### [Roslyn Analyzers](analyzers.md)
Complete guide to all 10 Roslyn analyzers for compile-time safety.

Understand OSMES001-OSMES010, how to configure analyzer severities, use code fixes, and integrate analyzers into your CI/CD pipeline.

## Advanced Features

### [Event Sourcing](event-sourcing.md)
Enable complete state history with automatic persistence and replay.

**Performance discovery**: Event sourcing is 30.4% faster than regular state machines when properly configured with `AutoConfirmEvents = true`.

### [Hierarchical States](hierarchical-states.md)
Build parent-child state relationships with substates.

Model complex state hierarchies, navigate state trees, and use inheritance patterns for cleaner state machine design.

### [Distributed Sagas](distributed-sagas.md)
Implement multi-grain workflows with automatic compensation.

Orchestrate complex business processes across multiple grains with automatic rollback, retry logic, and full audit trails.

### [Orthogonal Regions](orthogonal-regions.md)
Manage parallel independent state machines.

Run multiple state machines concurrently within a single grain, perfect for systems with independent subsystems like smart home automation.

## Production Features

### [Circuit Breaker Pattern](circuit-breaker.md)
Prevent cascading failures with built-in resilience.

Configure failure thresholds, recovery strategies, and exception handling for production-grade reliability.

### [Timers & Reminders](timers.md)
State-driven timeouts and scheduled transitions.

Choose between timers (short-lived) and reminders (persistent), configure timeouts, and handle expiration gracefully.

### [Distributed Tracing](tracing.md)
OpenTelemetry integration for complete observability.

Trace state transitions across distributed systems, correlate operations, and monitor state machine performance.

### [State Visualization](visualization.md)
Generate diagrams from your state machines.

Export state machines to DOT, Mermaid, PlantUML, and HTML formats for documentation and debugging.

## Patterns & Best Practices

### [State Machine Composition](composition.md)
Build reusable components for common patterns.

Use ValidationComponent, RetryComponent, ApprovalComponent, and CircuitBreakerComponent to compose complex behaviors.

### [State Versioning](versioning.md)
Manage state machine evolution across deployments.

Handle version upgrades, backward compatibility, breaking changes, and migration strategies for long-running systems.

### [Health Checks](health-checks.md)
ASP.NET Core health monitoring integration.

Monitor state machine health, expose health endpoints, and integrate with monitoring dashboards.

## Quick Navigation

- **New to Orleans.StateMachineES?** Start with [Getting Started](../getting-started/index.md)
- **Looking for examples?** Check out [Examples](../examples/index.md)
- **Need API reference?** Browse the [API Documentation](../../api/index.md)
- **Production deployment?** See [Architecture](../architecture/index.md)
