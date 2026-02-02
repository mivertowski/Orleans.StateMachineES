using Orleans.StateMachineES.Persistence;

namespace Orleans.StateMachineES.Queries;

/// <summary>
/// Interface for querying state machine history with temporal and filter capabilities.
/// Provides a fluent API for building complex queries against event history.
/// </summary>
/// <typeparam name="TState">The type representing the states.</typeparam>
/// <typeparam name="TTrigger">The type representing the triggers.</typeparam>
public interface IStateHistoryQuery<TState, TTrigger>
    where TState : notnull
    where TTrigger : notnull
{
    /// <summary>
    /// Filters events to those within the specified time range.
    /// </summary>
    /// <param name="from">Start of the time range (inclusive).</param>
    /// <param name="to">End of the time range (inclusive).</param>
    /// <returns>The query builder for chaining.</returns>
    IStateHistoryQuery<TState, TTrigger> InTimeRange(DateTime from, DateTime to);

    /// <summary>
    /// Filters events to those after the specified time.
    /// </summary>
    /// <param name="after">The time after which to include events.</param>
    /// <returns>The query builder for chaining.</returns>
    IStateHistoryQuery<TState, TTrigger> After(DateTime after);

    /// <summary>
    /// Filters events to those before the specified time.
    /// </summary>
    /// <param name="before">The time before which to include events.</param>
    /// <returns>The query builder for chaining.</returns>
    IStateHistoryQuery<TState, TTrigger> Before(DateTime before);

    /// <summary>
    /// Filters events that transition from the specified state.
    /// </summary>
    /// <param name="state">The source state to filter by.</param>
    /// <returns>The query builder for chaining.</returns>
    IStateHistoryQuery<TState, TTrigger> FromState(TState state);

    /// <summary>
    /// Filters events that transition to the specified state.
    /// </summary>
    /// <param name="state">The target state to filter by.</param>
    /// <returns>The query builder for chaining.</returns>
    IStateHistoryQuery<TState, TTrigger> ToState(TState state);

    /// <summary>
    /// Filters events with the specified trigger.
    /// </summary>
    /// <param name="trigger">The trigger to filter by.</param>
    /// <returns>The query builder for chaining.</returns>
    IStateHistoryQuery<TState, TTrigger> WithTrigger(TTrigger trigger);

    /// <summary>
    /// Filters events with any of the specified triggers.
    /// </summary>
    /// <param name="triggers">The triggers to filter by.</param>
    /// <returns>The query builder for chaining.</returns>
    IStateHistoryQuery<TState, TTrigger> WithTriggers(params TTrigger[] triggers);

    /// <summary>
    /// Filters events with the specified correlation ID.
    /// </summary>
    /// <param name="correlationId">The correlation ID to filter by.</param>
    /// <returns>The query builder for chaining.</returns>
    IStateHistoryQuery<TState, TTrigger> WithCorrelationId(string correlationId);

    /// <summary>
    /// Filters events with metadata matching the predicate.
    /// </summary>
    /// <param name="predicate">The metadata filter predicate.</param>
    /// <returns>The query builder for chaining.</returns>
    IStateHistoryQuery<TState, TTrigger> WithMetadata(Func<Dictionary<string, object>?, bool> predicate);

    /// <summary>
    /// Filters events within a specific version range.
    /// </summary>
    /// <param name="fromVersion">The starting version (inclusive).</param>
    /// <param name="toVersion">The ending version (inclusive).</param>
    /// <returns>The query builder for chaining.</returns>
    IStateHistoryQuery<TState, TTrigger> InVersionRange(long fromVersion, long toVersion);

    /// <summary>
    /// Orders the results by timestamp ascending.
    /// </summary>
    /// <returns>The query builder for chaining.</returns>
    IStateHistoryQuery<TState, TTrigger> OrderByTime();

    /// <summary>
    /// Orders the results by timestamp descending.
    /// </summary>
    /// <returns>The query builder for chaining.</returns>
    IStateHistoryQuery<TState, TTrigger> OrderByTimeDescending();

    /// <summary>
    /// Limits the number of results returned.
    /// </summary>
    /// <param name="count">Maximum number of results.</param>
    /// <returns>The query builder for chaining.</returns>
    IStateHistoryQuery<TState, TTrigger> Take(int count);

    /// <summary>
    /// Skips the specified number of results.
    /// </summary>
    /// <param name="count">Number of results to skip.</param>
    /// <returns>The query builder for chaining.</returns>
    IStateHistoryQuery<TState, TTrigger> Skip(int count);

    /// <summary>
    /// Executes the query and returns the matching events.
    /// </summary>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The matching events.</returns>
    Task<IReadOnlyList<StoredEvent<TState, TTrigger>>> ExecuteAsync(
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Executes the query and returns the first matching event.
    /// </summary>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The first matching event, or null if none found.</returns>
    Task<StoredEvent<TState, TTrigger>?> FirstOrDefaultAsync(
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Executes the query and returns the count of matching events.
    /// </summary>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The count of matching events.</returns>
    Task<int> CountAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Executes the query and checks if any events match.
    /// </summary>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>True if any events match, false otherwise.</returns>
    Task<bool> AnyAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Groups the results by state and returns state statistics.
    /// </summary>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Statistics grouped by state.</returns>
    Task<IReadOnlyDictionary<TState, StateStatistics>> GroupByStateAsync(
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Groups the results by trigger and returns trigger statistics.
    /// </summary>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Statistics grouped by trigger.</returns>
    Task<IReadOnlyDictionary<TTrigger, TriggerStatistics>> GroupByTriggerAsync(
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Groups the results by time period.
    /// </summary>
    /// <param name="periodType">The type of period to group by.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Statistics grouped by time period.</returns>
    Task<IReadOnlyList<TimePeriodStatistics<TState, TTrigger>>> GroupByTimeAsync(
        TimePeriodType periodType,
        CancellationToken cancellationToken = default);
}

/// <summary>
/// Statistics for a specific state.
/// </summary>
[GenerateSerializer]
[Alias("Orleans.StateMachineES.Queries.StateStatistics")]
public class StateStatistics
{
    /// <summary>
    /// Gets or sets the number of times this state was entered.
    /// </summary>
    [Id(0)]
    public int EntryCount { get; set; }

    /// <summary>
    /// Gets or sets the number of times this state was exited.
    /// </summary>
    [Id(1)]
    public int ExitCount { get; set; }

    /// <summary>
    /// Gets or sets the total time spent in this state.
    /// </summary>
    [Id(2)]
    public TimeSpan TotalDuration { get; set; }

    /// <summary>
    /// Gets or sets the average time spent in this state.
    /// </summary>
    [Id(3)]
    public TimeSpan AverageDuration { get; set; }

    /// <summary>
    /// Gets or sets the minimum time spent in this state.
    /// </summary>
    [Id(4)]
    public TimeSpan MinDuration { get; set; }

    /// <summary>
    /// Gets or sets the maximum time spent in this state.
    /// </summary>
    [Id(5)]
    public TimeSpan MaxDuration { get; set; }

    /// <summary>
    /// Gets or sets the first time this state was entered.
    /// </summary>
    [Id(6)]
    public DateTime? FirstEntry { get; set; }

    /// <summary>
    /// Gets or sets the last time this state was entered.
    /// </summary>
    [Id(7)]
    public DateTime? LastEntry { get; set; }
}

/// <summary>
/// Statistics for a specific trigger.
/// </summary>
[GenerateSerializer]
[Alias("Orleans.StateMachineES.Queries.TriggerStatistics")]
public class TriggerStatistics
{
    /// <summary>
    /// Gets or sets the number of times this trigger was fired.
    /// </summary>
    [Id(0)]
    public int FireCount { get; set; }

    /// <summary>
    /// Gets or sets the distinct source states this trigger was fired from.
    /// </summary>
    [Id(1)]
    public int DistinctSourceStates { get; set; }

    /// <summary>
    /// Gets or sets the distinct target states this trigger transitioned to.
    /// </summary>
    [Id(2)]
    public int DistinctTargetStates { get; set; }

    /// <summary>
    /// Gets or sets the first time this trigger was fired.
    /// </summary>
    [Id(3)]
    public DateTime? FirstFired { get; set; }

    /// <summary>
    /// Gets or sets the last time this trigger was fired.
    /// </summary>
    [Id(4)]
    public DateTime? LastFired { get; set; }
}

/// <summary>
/// Statistics for a time period.
/// </summary>
/// <typeparam name="TState">The type representing the states.</typeparam>
/// <typeparam name="TTrigger">The type representing the triggers.</typeparam>
[GenerateSerializer]
[Alias("Orleans.StateMachineES.Queries.TimePeriodStatistics`2")]
public class TimePeriodStatistics<TState, TTrigger>
    where TState : notnull
    where TTrigger : notnull
{
    /// <summary>
    /// Gets or sets the start of the time period.
    /// </summary>
    [Id(0)]
    public DateTime PeriodStart { get; set; }

    /// <summary>
    /// Gets or sets the end of the time period.
    /// </summary>
    [Id(1)]
    public DateTime PeriodEnd { get; set; }

    /// <summary>
    /// Gets or sets the number of events in this period.
    /// </summary>
    [Id(2)]
    public int EventCount { get; set; }

    /// <summary>
    /// Gets or sets the distinct states visited in this period.
    /// </summary>
    [Id(3)]
    public int DistinctStates { get; set; }

    /// <summary>
    /// Gets or sets the distinct triggers fired in this period.
    /// </summary>
    [Id(4)]
    public int DistinctTriggers { get; set; }

    /// <summary>
    /// Gets or sets the events in this period.
    /// </summary>
    [Id(5)]
    public IReadOnlyList<StoredEvent<TState, TTrigger>>? Events { get; set; }
}

/// <summary>
/// Type of time period for grouping.
/// </summary>
public enum TimePeriodType
{
    /// <summary>
    /// Group by hour.
    /// </summary>
    Hour,

    /// <summary>
    /// Group by day.
    /// </summary>
    Day,

    /// <summary>
    /// Group by week.
    /// </summary>
    Week,

    /// <summary>
    /// Group by month.
    /// </summary>
    Month
}
