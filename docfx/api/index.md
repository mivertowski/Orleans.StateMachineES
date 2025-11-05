# API Reference

This section contains the complete API documentation for Orleans.StateMachineES, automatically generated from XML documentation comments in the source code.

## Packages

### [Orleans.StateMachineES](Orleans.StateMachineES.yml)
The main library containing state machine grain implementations, event sourcing, advanced features, and production components.

**Key Namespaces:**
- `Orleans.StateMachineES` - Core state machine grain base classes
- `Orleans.StateMachineES.EventSourcing` - Event sourcing support with state replay
- `Orleans.StateMachineES.Hierarchical` - Nested state hierarchies
- `Orleans.StateMachineES.Sagas` - Distributed saga patterns
- `Orleans.StateMachineES.Timers` - Timer and reminder management
- `Orleans.StateMachineES.Tracing` - OpenTelemetry distributed tracing
- `Orleans.StateMachineES.Visualization` - State diagram generation (DOT, Mermaid, PlantUML, HTML)
- `Orleans.StateMachineES.Versioning` - State machine versioning and migration
- `Orleans.StateMachineES.Composition` - Composable state machine patterns
- `Orleans.StateMachineES.Composition.Components` - Built-in components (CircuitBreaker, Retry, Validation, Approval)
- `Orleans.StateMachineES.Orthogonal` - Parallel state regions
- `Orleans.StateMachineES.Memory` - Performance optimizations (TriggerParameterCache, ObjectPools)

### [Orleans.StateMachineES.Abstractions](Orleans.StateMachineES.Abstractions.yml)
Core interfaces, models, and abstractions used across the library.

**Key Namespaces:**
- `Orleans.StateMachineES.Interfaces` - Core grain interfaces (IStateMachineGrain, IEventSourcedStateMachineGrain)
- `Orleans.StateMachineES.Models` - Data models and metadata structures

### [Orleans.StateMachineES.Generators](Orleans.StateMachineES.Generators.yml)
Roslyn analyzers and source generators for compile-time safety and code generation.

**Key Components:**
- **10 Roslyn Analyzers** (OSMES001-010) - Compile-time safety checks
- **Source Generator** - YAML/JSON to state machine code generation

## Quick Links

- **Getting Started**: See [First State Machine](../articles/getting-started/first-state-machine.md)
- **Core Concepts**: See [Core Concepts Guide](../articles/getting-started/core-concepts.md)
- **Analyzer Reference**: See [Analyzer Guide](../articles/guides/analyzers.md)
- **Examples**: See [Examples Overview](../articles/examples/index.md)

## Searching the API

Use the search box in the top navigation to quickly find classes, methods, properties, and other API members across all packages.

## Common Entry Points

If you're new to the API, start with these key types:

- **StateMachineGrain&lt;TState, TTrigger&gt;** - Base class for basic state machine grains
- **EventSourcedStateMachineGrain&lt;TState, TTrigger&gt;** - Base class for event-sourced state machines
- **IStateMachineGrain&lt;TState, TTrigger&gt;** - Core interface for state machine operations
- **CircuitBreakerComponent** - Production resilience pattern for state machines
- **StateMachineVisualization** - Generate visual diagrams of your state machines

## Need Help?

- **Guides**: Browse the [Guides section](../articles/guides/index.md) for detailed tutorials
- **Examples**: Check out [complete examples](../articles/examples/index.md) with working code
- **Troubleshooting**: See the [troubleshooting guide](../articles/reference/troubleshooting.md)
- **Issues**: Report issues on [GitHub](https://github.com/mivertowski/Orleans.StateMachineES/issues)
