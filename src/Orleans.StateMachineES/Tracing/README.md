# OpenTelemetry Distributed Tracing and Metrics

This directory contains comprehensive OpenTelemetry integration for Orleans.StateMachineES, providing enterprise-grade observability through distributed tracing and metrics collection.

## Core Components

### 1. StateMachineActivitySource.cs
Central telemetry source providing:
- Standardized activity names and semantic tags following OpenTelemetry conventions
- Helper methods for creating activities with proper context
- Support for state transitions, saga execution, event sourcing, and version migrations
- Built-in error recording and success metrics

### 2. StateMachineMetrics.cs  
Comprehensive metrics collection including:
- **Counters**: State transitions, saga executions, errors, compensations
- **Histograms**: Duration distributions for operations and retries
- **Gauges**: Active grain and saga counts
- Performance metrics with detailed tagging for filtering and aggregation

### 3. TracingExtensions.cs
Fluent configuration API supporting:
- Host builder and silo builder integration
- Multiple exporters (Console, OTLP, Jaeger, Zipkin)
- Automatic instrumentation for Orleans, ASP.NET Core, and HTTP clients  
- Flexible sampling strategies and resource attributes
- Combined tracing and metrics configuration

### 4. Examples/TracingSetupExamples.cs
Production-ready configuration examples for:
- Development environments with console output
- Production deployments with OTLP collectors
- Jaeger integration for distributed tracing visualization
- High-performance minimal overhead setups
- Environment-aware configuration patterns

## Key Features

✅ **Distributed Tracing**: Complete request flow visibility across Orleans grains  
✅ **Performance Metrics**: Comprehensive counters, histograms, and gauges  
✅ **Multiple Exporters**: Console, OTLP, Jaeger, Zipkin support  
✅ **Flexible Configuration**: Fluent API with environment-aware setups  
✅ **Production Ready**: Sampling strategies and resource optimization  
✅ **Standards Compliant**: OpenTelemetry semantic conventions

## Usage Examples

### Basic Development Setup
```csharp
hostBuilder.AddStateMachineTracing(config =>
{
    config.ServiceName = "MyStateMachineApp";
    config.AddConsoleExporter()
          .AddConsoleMetricsExporter()
          .WithSampling(1.0); // Full sampling for development
});
```

### Production OTLP Setup  
```csharp
hostBuilder.AddStateMachineTracing(config =>
{
    config.ServiceName = "MyApp-Production";
    config.AddOtlpExporter("https://otlp-collector:4317")
          .AddOtlpMetricsExporter("https://otlp-collector:4317")
          .WithSampling(0.1); // 10% sampling for production
});
```

### Orleans Silo Configuration
```csharp
siloBuilder.AddStateMachineTracing(config =>
{
    config.ServiceName = "Orleans-Silo";
    config.AddOtlpExporter()
          .WithSampling(0.2);
});
```

## Integration Status

| Component | Status | Notes |
|-----------|--------|-------|
| Core Tracing Infrastructure | ✅ Complete | ActivitySource, semantic tags, context propagation |
| Metrics Collection | ✅ Complete | Counters, histograms, gauges for all operations |
| Configuration Extensions | ✅ Complete | Fluent API with multiple exporter support |
| Example Configurations | ✅ Complete | Development, production, and specialized setups |
| TracingHelper Utility | ✅ Complete | Static helper methods for easy integration |
| Build Status | ✅ Clean | No compilation errors, minimal warnings resolved |

## Observability Capabilities

The implementation provides complete observability for:
- State machine transitions with timing and success rates
- Saga orchestration including step execution and compensation
- Event sourcing operations and replay scenarios  
- Version migrations and upgrade processes
- Grain lifecycle events (activation/deactivation)
- Performance bottlenecks and error patterns

## Next Steps

While the core OpenTelemetry implementation is complete and production-ready, the traced grain base classes need refinement to handle the inheritance complexities of the Orleans.StateMachineES architecture. The tracing infrastructure can be used directly via the ActivitySource and Metrics classes in custom grain implementations.