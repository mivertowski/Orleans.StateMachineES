using System.Diagnostics;

namespace Orleans.StateMachineES.Tracing;

/// <summary>
/// Helper class for adding tracing and metrics to state machine operations.
/// Provides static methods to wrap operations with observability.
/// </summary>
public static class TracingHelper
{
    /// <summary>
    /// Wraps a state machine transition with tracing and metrics.
    /// </summary>
    /// <param name="grainType">The type of grain executing the transition.</param>
    /// <param name="grainId">The ID of the grain instance.</param>
    /// <param name="fromState">The source state.</param>
    /// <param name="trigger">The trigger being fired.</param>
    /// <param name="operation">The operation to execute.</param>
    /// <param name="parameterCount">Number of parameters for the trigger.</param>
    /// <returns>The result of the operation.</returns>
    public static async Task<T> TraceStateTransition<T>(
        string grainType,
        string grainId,
        string fromState,
        string trigger,
        Func<Task<T>> operation,
        int parameterCount = 0)
    {
        using var activity = StateMachineActivitySource.StartStateTransition(
            grainType, grainId, fromState, "Unknown", trigger);
        
        if (parameterCount > 0)
        {
            activity?.SetTag("trigger.has_parameters", true);
            activity?.SetTag("trigger.parameter_count", parameterCount);
        }
        
        try
        {
            var startTime = DateTime.UtcNow;
            var result = await operation();
            var duration = DateTime.UtcNow - startTime;
            
            // For state transitions, we need to determine the final state
            // This would typically be done by the caller who knows the state machine
            StateMachineActivitySource.RecordSuccess(activity, duration, new Dictionary<string, object>
            {
                ["transition.successful"] = true
            });
            
            // Record metrics - final state would need to be provided by caller
            StateMachineMetrics.RecordStateTransition(
                grainType, fromState, "Unknown", trigger, duration);
            
            return result;
        }
        catch (Exception ex)
        {
            StateMachineActivitySource.RecordError(activity, ex, "TransitionError");
            StateMachineMetrics.RecordStateTransitionError(grainType, fromState, trigger, ex.GetType().Name);
            throw;
        }
    }
    
    /// <summary>
    /// Wraps a saga execution with tracing and metrics.
    /// </summary>
    /// <param name="sagaType">The type of saga being executed.</param>
    /// <param name="sagaId">The saga instance ID.</param>
    /// <param name="correlationId">The correlation ID.</param>
    /// <param name="stepCount">Total number of steps.</param>
    /// <param name="operation">The saga operation to execute.</param>
    /// <returns>The result of the saga execution.</returns>
    public static async Task<T> TraceSagaExecution<T>(
        string sagaType,
        string sagaId,
        string correlationId,
        int stepCount,
        Func<Task<T>> operation)
    {
        using var activity = StateMachineActivitySource.StartSagaExecution(
            sagaId, correlationId, null, stepCount);
        
        StateMachineMetrics.RecordSagaStart(sagaType, stepCount);
        
        try
        {
            var startTime = DateTime.UtcNow;
            var result = await operation();
            var duration = DateTime.UtcNow - startTime;
            
            StateMachineActivitySource.RecordSuccess(activity, duration);
            
            // Record completion metrics - caller would provide actual success/failure counts
            StateMachineMetrics.RecordSagaCompletion(
                sagaType, "Completed", duration, stepCount, 0, false);
            
            return result;
        }
        catch (Exception ex)
        {
            StateMachineActivitySource.RecordError(activity, ex, "SagaExecutionError");
            throw;
        }
    }
    
    /// <summary>
    /// Wraps an event sourcing operation with tracing and metrics.
    /// </summary>
    /// <param name="grainType">The type of grain performing the operation.</param>
    /// <param name="grainId">The grain instance ID.</param>
    /// <param name="eventType">The type of event being processed.</param>
    /// <param name="operation">The event sourcing operation.</param>
    /// <param name="isReplay">Whether this is a replay operation.</param>
    /// <param name="eventVersion">The event version/sequence number.</param>
    /// <returns>The result of the operation.</returns>
    public static async Task<T> TraceEventSourcing<T>(
        string grainType,
        string grainId,
        string eventType,
        Func<Task<T>> operation,
        bool isReplay = false,
        int? eventVersion = null)
    {
        using var activity = StateMachineActivitySource.StartEventSourcing(
            grainType, grainId, eventType, eventVersion?.ToString());
        
        try
        {
            var startTime = DateTime.UtcNow;
            var result = await operation();
            var duration = DateTime.UtcNow - startTime;
            
            StateMachineActivitySource.RecordSuccess(activity, duration);
            StateMachineMetrics.RecordEventSourcing(
                grainType, eventType, duration, isReplay, eventVersion);
            
            return result;
        }
        catch (Exception ex)
        {
            StateMachineActivitySource.RecordError(activity, ex, "EventSourcingError");
            throw;
        }
    }
    
    /// <summary>
    /// Wraps a version migration with tracing and metrics.
    /// </summary>
    /// <param name="grainType">The type of grain being migrated.</param>
    /// <param name="grainId">The grain instance ID.</param>
    /// <param name="fromVersion">The source version.</param>
    /// <param name="toVersion">The target version.</param>
    /// <param name="strategy">The migration strategy.</param>
    /// <param name="operation">The migration operation.</param>
    /// <param name="isAutoUpgrade">Whether this is an automatic upgrade.</param>
    /// <returns>The result of the migration.</returns>
    public static async Task<T> TraceVersionMigration<T>(
        string grainType,
        string grainId,
        string fromVersion,
        string toVersion,
        string strategy,
        Func<Task<T>> operation,
        bool isAutoUpgrade = false)
    {
        using var activity = StateMachineActivitySource.StartVersionMigration(
            grainType, grainId, fromVersion, toVersion, strategy);
        
        try
        {
            var startTime = DateTime.UtcNow;
            var result = await operation();
            var duration = DateTime.UtcNow - startTime;
            
            StateMachineActivitySource.RecordSuccess(activity, duration);
            StateMachineMetrics.RecordVersionMigration(
                grainType, fromVersion, toVersion, strategy, true, duration, isAutoUpgrade);
            
            return result;
        }
        catch (Exception ex)
        {
            StateMachineActivitySource.RecordError(activity, ex, "VersionMigrationError");
            StateMachineMetrics.RecordVersionMigration(
                grainType, fromVersion, toVersion, strategy, false, TimeSpan.Zero, isAutoUpgrade);
            throw;
        }
    }
    
    /// <summary>
    /// Records grain activation with metrics.
    /// </summary>
    /// <param name="grainType">The type of grain being activated.</param>
    /// <param name="hasEventSourcing">Whether the grain uses event sourcing.</param>
    /// <param name="hasVersioning">Whether the grain uses versioning.</param>
    /// <param name="eventsReplayed">Number of events replayed (if applicable).</param>
    public static void RecordGrainActivation(
        string grainType,
        bool hasEventSourcing = false,
        bool hasVersioning = false,
        int? eventsReplayed = null)
    {
        StateMachineMetrics.RecordGrainActivation(grainType, hasEventSourcing, hasVersioning, eventsReplayed);
    }
    
    /// <summary>
    /// Records grain deactivation with metrics.
    /// </summary>
    /// <param name="grainType">The type of grain being deactivated.</param>
    /// <param name="reason">The reason for deactivation.</param>
    public static void RecordGrainDeactivation(string grainType, string reason)
    {
        StateMachineMetrics.RecordGrainDeactivation(grainType, reason);
    }
    
    /// <summary>
    /// Creates a child activity for custom operations.
    /// </summary>
    /// <param name="operationName">The name of the operation.</param>
    /// <param name="grainType">The type of grain.</param>
    /// <param name="grainId">The grain instance ID.</param>
    /// <param name="additionalTags">Additional tags for the activity.</param>
    /// <returns>A new activity or null if not sampled.</returns>
    public static Activity? StartChildActivity(
        string operationName,
        string grainType,
        string grainId,
        Dictionary<string, object>? additionalTags = null)
    {
        var activity = StateMachineActivitySource.Source.StartActivity($"statemachine.{operationName}");
        
        activity?.SetTag(StateMachineActivitySource.GrainType, grainType);
        activity?.SetTag(StateMachineActivitySource.GrainId, grainId);
        
        if (additionalTags != null)
        {
            foreach (var tag in additionalTags)
            {
                activity?.SetTag(tag.Key, tag.Value);
            }
        }
        
        return activity;
    }

    /// <summary>
    /// Traces a health check operation for a state machine grain.
    /// </summary>
    /// <typeparam name="T">The type of the health check result.</typeparam>
    /// <param name="grainType">The type of grain being health checked.</param>
    /// <param name="grainId">The ID of the grain being health checked.</param>
    /// <param name="operation">The health check operation to trace.</param>
    /// <returns>The result of the health check operation.</returns>
    public static async Task<T> TraceHealthCheck<T>(
        string grainType,
        string grainId,
        Func<Task<T>> operation)
    {
        using var activity = StateMachineActivitySource.StartHealthCheck(grainType, grainId);
        var stopwatch = Stopwatch.StartNew();
        
        try
        {
            activity?.SetTag("operation.type", "health_check");
            activity?.SetTag("grain.type", grainType);
            activity?.SetTag("grain.id", grainId);
            
            var result = await operation();
            
            stopwatch.Stop();
            activity?.SetTag("health_check.success", true);
            activity?.SetTag("health_check.duration_ms", stopwatch.ElapsedMilliseconds);
            
            // Record metrics
            StateMachineMetrics.RecordHealthCheck("success", stopwatch.Elapsed);
            
            return result;
        }
        catch (Exception ex)
        {
            stopwatch.Stop();
            activity?.SetStatus(ActivityStatusCode.Error, ex.Message);
            activity?.SetTag("health_check.success", false);
            activity?.SetTag("health_check.error", ex.Message);
            activity?.SetTag("health_check.duration_ms", stopwatch.ElapsedMilliseconds);
            
            // Record metrics
            StateMachineMetrics.RecordHealthCheck("error", stopwatch.Elapsed);
            
            throw;
        }
    }
}