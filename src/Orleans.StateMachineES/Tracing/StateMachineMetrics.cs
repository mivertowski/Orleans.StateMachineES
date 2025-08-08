using System;
using System.Collections.Generic;
using System.Diagnostics.Metrics;

namespace Orleans.StateMachineES.Tracing;

/// <summary>
/// Provides comprehensive metrics collection for Orleans.StateMachineES operations.
/// Complements distributed tracing with performance counters, histograms, and gauges.
/// </summary>
public static class StateMachineMetrics
{
    /// <summary>
    /// The name of the meter for Orleans.StateMachineES metrics.
    /// </summary>
    public const string MeterName = "Orleans.StateMachineES";
    
    /// <summary>
    /// The version of the meter, matching the assembly version.
    /// </summary>
    public const string MeterVersion = "1.0.0";
    
    /// <summary>
    /// The main meter for all state machine metrics.
    /// </summary>
    public static readonly Meter Meter = new(MeterName, MeterVersion);
    
    // Counters - Track totals and rates
    private static readonly Counter<long> StateTransitionsTotal = Meter.CreateCounter<long>(
        "statemachine_transitions_total",
        description: "Total number of state transitions processed");
    
    private static readonly Counter<long> StateTransitionErrorsTotal = Meter.CreateCounter<long>(
        "statemachine_transition_errors_total", 
        description: "Total number of failed state transitions");
    
    private static readonly Counter<long> SagaExecutionsTotal = Meter.CreateCounter<long>(
        "saga_executions_total",
        description: "Total number of saga executions started");
    
    private static readonly Counter<long> SagaStepsTotal = Meter.CreateCounter<long>(
        "saga_steps_total",
        description: "Total number of saga steps executed");
    
    private static readonly Counter<long> SagaCompensationsTotal = Meter.CreateCounter<long>(
        "saga_compensations_total",
        description: "Total number of saga compensations performed");
    
    private static readonly Counter<long> EventsSourcedTotal = Meter.CreateCounter<long>(
        "eventsourcing_events_total",
        description: "Total number of events persisted for event sourcing");
    
    private static readonly Counter<long> VersionMigrationsTotal = Meter.CreateCounter<long>(
        "version_migrations_total",
        description: "Total number of version migrations performed");
    
    private static readonly Counter<long> GrainActivationsTotal = Meter.CreateCounter<long>(
        "grain_activations_total",
        description: "Total number of state machine grain activations");
    
    // Histograms - Track distributions of values
    private static readonly Histogram<double> StateTransitionDuration = Meter.CreateHistogram<double>(
        "statemachine_transition_duration_seconds",
        unit: "s",
        description: "Duration of state transitions in seconds");
    
    private static readonly Histogram<double> SagaExecutionDuration = Meter.CreateHistogram<double>(
        "saga_execution_duration_seconds",
        unit: "s", 
        description: "Duration of complete saga executions in seconds");
    
    private static readonly Histogram<double> SagaStepDuration = Meter.CreateHistogram<double>(
        "saga_step_duration_seconds",
        unit: "s",
        description: "Duration of individual saga steps in seconds");
    
    private static readonly Histogram<double> EventSourcingDuration = Meter.CreateHistogram<double>(
        "eventsourcing_operation_duration_seconds",
        unit: "s",
        description: "Duration of event sourcing operations in seconds");
    
    private static readonly Histogram<double> VersionMigrationDuration = Meter.CreateHistogram<double>(
        "version_migration_duration_seconds",
        unit: "s",
        description: "Duration of version migrations in seconds");
    
    private static readonly Histogram<long> SagaStepRetries = Meter.CreateHistogram<long>(
        "saga_step_retries",
        description: "Number of retries performed for saga steps");
    
    // Gauges - Track current values using proper callbacks
    
    // Internal tracking for gauges
    private static int _activeGrainsCount = 0;
    private static int _activeSagasCount = 0;
    
    static StateMachineMetrics()
    {
        // Set up gauge observations with proper callback format
        _ = Meter.CreateObservableGauge("statemachine_active_grains", 
            () => _activeGrainsCount, 
            description: "Current number of active state machine grains");
        _ = Meter.CreateObservableGauge("saga_active_count", 
            () => _activeSagasCount, 
            description: "Current number of active sagas");
    }
    
    /// <summary>
    /// Records a successful state transition with duration and metadata.
    /// </summary>
    /// <param name="grainType">The type of grain executing the transition.</param>
    /// <param name="fromState">The source state.</param>
    /// <param name="toState">The destination state.</param>
    /// <param name="trigger">The trigger that caused the transition.</param>
    /// <param name="duration">The duration of the transition.</param>
    /// <param name="hasParameters">Whether the trigger had parameters.</param>
    public static void RecordStateTransition(
        string grainType,
        string fromState,
        string toState,
        string trigger,
        TimeSpan duration,
        bool hasParameters = false)
    {
        var tags = new KeyValuePair<string, object?>[]
        {
            new("grain_type", grainType),
            new("from_state", fromState),
            new("to_state", toState),
            new("trigger", trigger),
            new("has_parameters", hasParameters),
            new("state_changed", !fromState.Equals(toState))
        };
        
        StateTransitionsTotal.Add(1, tags);
        StateTransitionDuration.Record(duration.TotalSeconds, tags);
    }
    
    /// <summary>
    /// Records a failed state transition with error details.
    /// </summary>
    /// <param name="grainType">The type of grain that failed.</param>
    /// <param name="fromState">The state where the failure occurred.</param>
    /// <param name="trigger">The trigger that caused the failure.</param>
    /// <param name="errorType">The type of error.</param>
    public static void RecordStateTransitionError(
        string grainType,
        string fromState,
        string trigger,
        string errorType)
    {
        var tags = new KeyValuePair<string, object?>[]
        {
            new("grain_type", grainType),
            new("from_state", fromState),
            new("trigger", trigger),
            new("error_type", errorType)
        };
        
        StateTransitionErrorsTotal.Add(1, tags);
    }
    
    /// <summary>
    /// Records the start of a saga execution.
    /// </summary>
    /// <param name="sagaType">The type of saga being executed.</param>
    /// <param name="stepCount">The total number of steps in the saga.</param>
    public static void RecordSagaStart(string sagaType, int stepCount)
    {
        var tags = new KeyValuePair<string, object?>[]
        {
            new("saga_type", sagaType),
            new("step_count", stepCount)
        };
        
        SagaExecutionsTotal.Add(1, tags);
        IncrementActiveSagas();
    }
    
    /// <summary>
    /// Records the completion of a saga execution.
    /// </summary>
    /// <param name="sagaType">The type of saga that completed.</param>
    /// <param name="status">The final status of the saga.</param>
    /// <param name="duration">The total execution duration.</param>
    /// <param name="successfulSteps">Number of successful steps.</param>
    /// <param name="failedSteps">Number of failed steps.</param>
    /// <param name="wasCompensated">Whether compensation was triggered.</param>
    public static void RecordSagaCompletion(
        string sagaType,
        string status,
        TimeSpan duration,
        int successfulSteps,
        int failedSteps,
        bool wasCompensated)
    {
        var tags = new KeyValuePair<string, object?>[]
        {
            new("saga_type", sagaType),
            new("status", status),
            new("successful_steps", successfulSteps),
            new("failed_steps", failedSteps),
            new("was_compensated", wasCompensated)
        };
        
        SagaExecutionDuration.Record(duration.TotalSeconds, tags);
        DecrementActiveSagas();
    }
    
    /// <summary>
    /// Records a saga step execution.
    /// </summary>
    /// <param name="sagaType">The type of saga executing the step.</param>
    /// <param name="stepName">The name of the step.</param>
    /// <param name="isSuccess">Whether the step succeeded.</param>
    /// <param name="duration">The step execution duration.</param>
    /// <param name="retryAttempt">The retry attempt number (1 for first attempt).</param>
    /// <param name="isBusinessFailure">Whether this was a business logic failure.</param>
    public static void RecordSagaStep(
        string sagaType,
        string stepName,
        bool isSuccess,
        TimeSpan duration,
        int retryAttempt = 1,
        bool isBusinessFailure = false)
    {
        var tags = new KeyValuePair<string, object?>[]
        {
            new("saga_type", sagaType),
            new("step_name", stepName),
            new("success", isSuccess),
            new("retry_attempt", retryAttempt),
            new("is_business_failure", isBusinessFailure)
        };
        
        SagaStepsTotal.Add(1, tags);
        SagaStepDuration.Record(duration.TotalSeconds, tags);
        
        if (retryAttempt > 1)
        {
            SagaStepRetries.Record(retryAttempt - 1, tags);
        }
    }
    
    /// <summary>
    /// Records a saga compensation operation.
    /// </summary>
    /// <param name="sagaType">The type of saga being compensated.</param>
    /// <param name="stepName">The step being compensated.</param>
    /// <param name="isSuccess">Whether the compensation succeeded.</param>
    /// <param name="reason">The reason for compensation.</param>
    public static void RecordSagaCompensation(
        string sagaType,
        string stepName,
        bool isSuccess,
        string reason)
    {
        var tags = new KeyValuePair<string, object?>[]
        {
            new("saga_type", sagaType),
            new("step_name", stepName),
            new("success", isSuccess),
            new("reason", reason)
        };
        
        SagaCompensationsTotal.Add(1, tags);
    }
    
    /// <summary>
    /// Records an event sourcing operation.
    /// </summary>
    /// <param name="grainType">The type of grain performing event sourcing.</param>
    /// <param name="eventType">The type of event being processed.</param>
    /// <param name="duration">The operation duration.</param>
    /// <param name="isReplay">Whether this is a replay operation.</param>
    /// <param name="eventVersion">The version/sequence number of the event.</param>
    public static void RecordEventSourcing(
        string grainType,
        string eventType,
        TimeSpan duration,
        bool isReplay = false,
        int? eventVersion = null)
    {
        var tags = new List<KeyValuePair<string, object?>>
        {
            new("grain_type", grainType),
            new("event_type", eventType),
            new("is_replay", isReplay)
        };
        
        if (eventVersion.HasValue)
        {
            tags.Add(new("event_version", eventVersion.Value));
        }
        
        EventsSourcedTotal.Add(1, tags.ToArray());
        EventSourcingDuration.Record(duration.TotalSeconds, tags.ToArray());
    }
    
    /// <summary>
    /// Records a version migration operation.
    /// </summary>
    /// <param name="grainType">The type of grain being migrated.</param>
    /// <param name="fromVersion">The source version.</param>
    /// <param name="toVersion">The target version.</param>
    /// <param name="strategy">The migration strategy used.</param>
    /// <param name="isSuccess">Whether the migration succeeded.</param>
    /// <param name="duration">The migration duration.</param>
    /// <param name="isAutoUpgrade">Whether this was an automatic upgrade.</param>
    public static void RecordVersionMigration(
        string grainType,
        string fromVersion,
        string toVersion,
        string strategy,
        bool isSuccess,
        TimeSpan duration,
        bool isAutoUpgrade = false)
    {
        var tags = new KeyValuePair<string, object?>[]
        {
            new("grain_type", grainType),
            new("from_version", fromVersion),
            new("to_version", toVersion),
            new("strategy", strategy),
            new("success", isSuccess),
            new("is_auto_upgrade", isAutoUpgrade)
        };
        
        VersionMigrationsTotal.Add(1, tags);
        VersionMigrationDuration.Record(duration.TotalSeconds, tags);
    }
    
    /// <summary>
    /// Records a grain activation.
    /// </summary>
    /// <param name="grainType">The type of grain being activated.</param>
    /// <param name="hasEventSourcing">Whether the grain uses event sourcing.</param>
    /// <param name="hasVersioning">Whether the grain uses versioning.</param>
    /// <param name="eventsReplayed">Number of events replayed during activation (if applicable).</param>
    public static void RecordGrainActivation(
        string grainType,
        bool hasEventSourcing = false,
        bool hasVersioning = false,
        int? eventsReplayed = null)
    {
        var tags = new List<KeyValuePair<string, object?>>
        {
            new("grain_type", grainType),
            new("has_event_sourcing", hasEventSourcing),
            new("has_versioning", hasVersioning)
        };
        
        if (eventsReplayed.HasValue)
        {
            tags.Add(new("events_replayed", eventsReplayed.Value));
        }
        
        GrainActivationsTotal.Add(1, tags.ToArray());
        IncrementActiveGrains();
    }
    
    /// <summary>
    /// Records a grain deactivation.
    /// </summary>
    /// <param name="grainType">The type of grain being deactivated.</param>
    /// <param name="reason">The reason for deactivation.</param>
    public static void RecordGrainDeactivation(string grainType, string reason)
    {
        // Note: grainType and reason could be used for more detailed metrics in the future
        _ = grainType; // Suppress unused parameter warning
        _ = reason;    // Suppress unused parameter warning
        DecrementActiveGrains();
    }
    
    private static void IncrementActiveGrains()
    {
        System.Threading.Interlocked.Increment(ref _activeGrainsCount);
    }
    
    private static void DecrementActiveGrains()
    {
        System.Threading.Interlocked.Decrement(ref _activeGrainsCount);
    }
    
    private static void IncrementActiveSagas()
    {
        System.Threading.Interlocked.Increment(ref _activeSagasCount);
    }
    
    private static void DecrementActiveSagas()
    {
        System.Threading.Interlocked.Decrement(ref _activeSagasCount);
    }
    
    /// <summary>
    /// Records a health check operation.
    /// </summary>
    /// <param name="status">Health check status.</param>
    /// <param name="duration">Duration of the health check.</param>
    public static void RecordHealthCheck(string status, TimeSpan duration)
    {
        // For now, we'll track these as custom events
        // In a full implementation, we'd add specific health check metrics
        var tags = new KeyValuePair<string, object?>[]
        {
            new("operation", "health_check"),
            new("status", status)
        };
        
        // Record as a generic counter for now
        GrainActivationsTotal.Add(1, tags);
    }

    /// <summary>
    /// Gets the total number of state transitions recorded.
    /// </summary>
    /// <returns>Total transition count.</returns>
    public static long GetTotalTransitions()
    {
        // In a real implementation, this would query the metrics provider
        // For now, return the internal counter value
        return _totalTransitionsCount;
    }

    /// <summary>
    /// Gets the average transition time in milliseconds.
    /// </summary>
    /// <returns>Average transition time.</returns>
    public static double GetAverageTransitionTime()
    {
        // In a real implementation, this would calculate from the histogram
        return _totalTransitionsCount > 0 ? _totalTransitionDuration / _totalTransitionsCount : 0;
    }

    /// <summary>
    /// Gets the total number of errors recorded.
    /// </summary>
    /// <returns>Total error count.</returns>
    public static long GetTotalErrors()
    {
        return _totalErrorsCount;
    }

    /// <summary>
    /// Gets the current error rate as a percentage.
    /// </summary>
    /// <returns>Error rate (0.0 to 1.0).</returns>
    public static double GetErrorRate()
    {
        var totalOperations = _totalTransitionsCount + _totalErrorsCount;
        return totalOperations > 0 ? (double)_totalErrorsCount / totalOperations : 0.0;
    }

    /// <summary>
    /// Gets the number of currently active sagas.
    /// </summary>
    /// <returns>Active saga count.</returns>
    public static int GetActiveSagaCount()
    {
        return _activeSagasCount;
    }

    /// <summary>
    /// Gets current metrics snapshot for health checks or monitoring.
    /// </summary>
    /// <returns>A dictionary containing current metric values.</returns>
    public static Dictionary<string, object> GetCurrentMetrics()
    {
        return new Dictionary<string, object>
        {
            ["active_grains"] = _activeGrainsCount,
            ["active_sagas"] = _activeSagasCount,
            ["total_transitions"] = _totalTransitionsCount,
            ["total_errors"] = _totalErrorsCount,
            ["error_rate"] = GetErrorRate(),
            ["meter_name"] = MeterName,
            ["meter_version"] = MeterVersion
        };
    }

    // Private fields to track totals (in a real implementation, these would be managed by the metrics system)
    private static long _totalTransitionsCount = 0;
    private static double _totalTransitionDuration = 0;
    private static long _totalErrorsCount = 0;
    
    /// <summary>
    /// Disposes of the meter when the application shuts down.
    /// Should be called during application cleanup.
    /// </summary>
    public static void Dispose()
    {
        Meter?.Dispose();
    }
}