using System;
using System.Diagnostics;
using System.Collections.Generic;

namespace Orleans.StateMachineES.Tracing;

/// <summary>
/// Central activity source for state machine telemetry and distributed tracing.
/// Provides structured observability for state transitions, saga execution, and system operations.
/// </summary>
public static class StateMachineActivitySource
{
    /// <summary>
    /// The name of the activity source for Orleans.StateMachineES telemetry.
    /// </summary>
    public const string SourceName = "Orleans.StateMachineES";
    
    /// <summary>
    /// The version of the activity source, matching the assembly version.
    /// </summary>
    public const string SourceVersion = "1.0.0";
    
    /// <summary>
    /// The main activity source for all state machine operations.
    /// </summary>
    public static readonly ActivitySource Source = new(SourceName, SourceVersion);
    
    // Activity Names - Standard naming conventions for OpenTelemetry
    public const string StateTransition = "statemachine.transition";
    public const string SagaExecution = "saga.execute";
    public const string SagaStep = "saga.step";
    public const string SagaCompensation = "saga.compensation";
    public const string StateMachineActivation = "statemachine.activation";
    public const string StateMachineDeactivation = "statemachine.deactivation";
    public const string EventSourcing = "statemachine.eventsourcing";
    public const string VersionMigration = "statemachine.migration";
    public const string TimerExecution = "statemachine.timer";
    public const string IntrospectionAnalysis = "statemachine.introspection";
    
    // Tag Names - Following OpenTelemetry semantic conventions
    public const string GrainType = "statemachine.grain.type";
    public const string GrainId = "statemachine.grain.id";
    public const string FromState = "statemachine.from_state";
    public const string ToState = "statemachine.to_state";
    public const string Trigger = "statemachine.trigger";
    public const string SagaId = "saga.id";
    public const string StepName = "saga.step.name";
    public const string CorrelationId = "correlation.id";
    public const string BusinessTransactionId = "business.transaction.id";
    public const string StateMachineVersion = "statemachine.version";
    public const string EventType = "event.type";
    public const string CompensationReason = "compensation.reason";
    public const string MigrationStrategy = "migration.strategy";
    public const string TimerName = "timer.name";
    public const string ErrorType = "error.type";
    public const string RetryAttempt = "retry.attempt";
    public const string ExecutionDuration = "execution.duration";
    public const string StepCount = "saga.step.count";
    public const string SuccessfulSteps = "saga.steps.successful";
    public const string FailedSteps = "saga.steps.failed";
    
    /// <summary>
    /// Creates a new activity for state machine transition tracking.
    /// </summary>
    /// <param name="grainType">The type of the grain executing the transition.</param>
    /// <param name="grainId">The ID of the grain instance.</param>
    /// <param name="fromState">The source state of the transition.</param>
    /// <param name="toState">The destination state of the transition.</param>
    /// <param name="trigger">The trigger that initiated the transition.</param>
    /// <param name="version">The version of the state machine.</param>
    /// <returns>A new activity for the state transition, or null if not sampled.</returns>
    public static Activity? StartStateTransition(
        string grainType,
        string grainId, 
        string fromState,
        string toState,
        string trigger,
        string? version = null)
    {
        var activity = Source.StartActivity(StateTransition);
        
        activity?.SetTag(GrainType, grainType);
        activity?.SetTag(GrainId, grainId);
        activity?.SetTag(FromState, fromState);
        activity?.SetTag(ToState, toState);
        activity?.SetTag(Trigger, trigger);
        
        if (!string.IsNullOrEmpty(version))
        {
            activity?.SetTag(StateMachineVersion, version);
        }
        
        return activity;
    }
    
    /// <summary>
    /// Creates a new activity for saga execution tracking.
    /// </summary>
    /// <param name="sagaId">The unique identifier for the saga instance.</param>
    /// <param name="correlationId">The correlation ID for distributed tracing.</param>
    /// <param name="businessTransactionId">The business transaction identifier.</param>
    /// <param name="stepCount">The total number of steps in the saga.</param>
    /// <returns>A new activity for saga execution, or null if not sampled.</returns>
    public static Activity? StartSagaExecution(
        string sagaId,
        string correlationId,
        string? businessTransactionId = null,
        int? stepCount = null)
    {
        var activity = Source.StartActivity(SagaExecution);
        
        activity?.SetTag(SagaId, sagaId);
        activity?.SetTag(CorrelationId, correlationId);
        
        if (!string.IsNullOrEmpty(businessTransactionId))
        {
            activity?.SetTag(BusinessTransactionId, businessTransactionId);
        }
        
        if (stepCount.HasValue)
        {
            activity?.SetTag(StepCount, stepCount.Value);
        }
        
        return activity;
    }
    
    /// <summary>
    /// Creates a new activity for saga step execution tracking.
    /// </summary>
    /// <param name="stepName">The name of the saga step being executed.</param>
    /// <param name="sagaId">The saga instance identifier.</param>
    /// <param name="correlationId">The correlation ID for distributed tracing.</param>
    /// <param name="retryAttempt">The current retry attempt number.</param>
    /// <returns>A new activity for saga step execution, or null if not sampled.</returns>
    public static Activity? StartSagaStep(
        string stepName,
        string sagaId,
        string correlationId,
        int? retryAttempt = null)
    {
        var activity = Source.StartActivity(SagaStep);
        
        activity?.SetTag(StepName, stepName);
        activity?.SetTag(SagaId, sagaId);
        activity?.SetTag(CorrelationId, correlationId);
        
        if (retryAttempt.HasValue && retryAttempt > 0)
        {
            activity?.SetTag(RetryAttempt, retryAttempt.Value);
        }
        
        return activity;
    }
    
    /// <summary>
    /// Creates a new activity for saga compensation tracking.
    /// </summary>
    /// <param name="sagaId">The saga instance identifier.</param>
    /// <param name="correlationId">The correlation ID for distributed tracing.</param>
    /// <param name="reason">The reason for compensation.</param>
    /// <param name="stepCount">The number of steps being compensated.</param>
    /// <returns>A new activity for saga compensation, or null if not sampled.</returns>
    public static Activity? StartSagaCompensation(
        string sagaId,
        string correlationId,
        string reason,
        int? stepCount = null)
    {
        var activity = Source.StartActivity(SagaCompensation);
        
        activity?.SetTag(SagaId, sagaId);
        activity?.SetTag(CorrelationId, correlationId);
        activity?.SetTag(CompensationReason, reason);
        
        if (stepCount.HasValue)
        {
            activity?.SetTag(StepCount, stepCount.Value);
        }
        
        return activity;
    }
    
    /// <summary>
    /// Creates a new activity for event sourcing operations.
    /// </summary>
    /// <param name="grainType">The type of the grain performing event sourcing.</param>
    /// <param name="grainId">The grain instance identifier.</param>
    /// <param name="eventType">The type of event being processed.</param>
    /// <param name="version">The state machine version.</param>
    /// <returns>A new activity for event sourcing, or null if not sampled.</returns>
    public static Activity? StartEventSourcing(
        string grainType,
        string grainId,
        string eventType,
        string? version = null)
    {
        var activity = Source.StartActivity(EventSourcing);
        
        activity?.SetTag(GrainType, grainType);
        activity?.SetTag(GrainId, grainId);
        activity?.SetTag(EventType, eventType);
        
        if (!string.IsNullOrEmpty(version))
        {
            activity?.SetTag(StateMachineVersion, version);
        }
        
        return activity;
    }
    
    /// <summary>
    /// Creates a new activity for version migration tracking.
    /// </summary>
    /// <param name="grainType">The type of the grain being migrated.</param>
    /// <param name="grainId">The grain instance identifier.</param>
    /// <param name="fromVersion">The source version.</param>
    /// <param name="toVersion">The target version.</param>
    /// <param name="strategy">The migration strategy being used.</param>
    /// <returns>A new activity for version migration, or null if not sampled.</returns>
    public static Activity? StartVersionMigration(
        string grainType,
        string grainId,
        string fromVersion,
        string toVersion,
        string strategy)
    {
        var activity = Source.StartActivity(VersionMigration);
        
        activity?.SetTag(GrainType, grainType);
        activity?.SetTag(GrainId, grainId);
        activity?.SetTag("migration.from_version", fromVersion);
        activity?.SetTag("migration.to_version", toVersion);
        activity?.SetTag(MigrationStrategy, strategy);
        
        return activity;
    }
    
    /// <summary>
    /// Adds standard error information to an activity.
    /// </summary>
    /// <param name="activity">The activity to add error information to.</param>
    /// <param name="exception">The exception that occurred.</param>
    /// <param name="errorType">The type/category of error.</param>
    public static void RecordError(Activity? activity, Exception exception, string? errorType = null)
    {
        if (activity == null) return;
        
        activity.SetStatus(ActivityStatusCode.Error, exception.Message);
        activity.SetTag("error", true);
        activity.SetTag("error.message", exception.Message);
        activity.SetTag("error.stack", exception.StackTrace);
        
        if (!string.IsNullOrEmpty(errorType))
        {
            activity.SetTag(ErrorType, errorType);
        }
        else
        {
            activity.SetTag(ErrorType, exception.GetType().Name);
        }
    }
    
    /// <summary>
    /// Records successful completion with performance metrics.
    /// </summary>
    /// <param name="activity">The activity to mark as successful.</param>
    /// <param name="duration">The execution duration.</param>
    /// <param name="additionalTags">Additional success metrics to record.</param>
    public static void RecordSuccess(Activity? activity, TimeSpan? duration = null, Dictionary<string, object>? additionalTags = null)
    {
        if (activity == null) return;
        
        activity.SetStatus(ActivityStatusCode.Ok);
        
        if (duration.HasValue)
        {
            activity.SetTag(ExecutionDuration, duration.Value.TotalMilliseconds);
        }
        
        if (additionalTags != null)
        {
            foreach (var tag in additionalTags)
            {
                activity.SetTag(tag.Key, tag.Value);
            }
        }
    }
    
    /// <summary>
    /// Starts a health check activity for a state machine grain.
    /// </summary>
    /// <param name="grainType">The type of grain being health checked.</param>
    /// <param name="grainId">The grain ID being health checked.</param>
    /// <returns>A new activity for the health check operation.</returns>
    public static Activity? StartHealthCheck(string grainType, string grainId)
    {
        var activity = Source.StartActivity("statemachine.health_check");
        
        activity?.SetTag(GrainType, grainType);
        activity?.SetTag(GrainId, grainId);
        activity?.SetTag("operation.name", "health_check");
        activity?.SetTag("operation.category", "monitoring");
        
        return activity;
    }

    /// <summary>
    /// Disposes of the activity source when the application shuts down.
    /// Should be called during application cleanup.
    /// </summary>
    public static void Dispose()
    {
        Source?.Dispose();
    }
}