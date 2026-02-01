using Orleans.StateMachineES.Persistence;

namespace Orleans.StateMachineES.Queries;

/// <summary>
/// Builder for constructing state history queries with fluent API.
/// </summary>
/// <typeparam name="TState">The type representing the states.</typeparam>
/// <typeparam name="TTrigger">The type representing the triggers.</typeparam>
public class StateHistoryQueryBuilder<TState, TTrigger> : IStateHistoryQuery<TState, TTrigger>
    where TState : notnull
    where TTrigger : notnull
{
    private readonly Func<CancellationToken, Task<IReadOnlyList<StoredEvent<TState, TTrigger>>>> _eventSource;
    private readonly List<Func<StoredEvent<TState, TTrigger>, bool>> _filters = [];
    private Func<IEnumerable<StoredEvent<TState, TTrigger>>, IEnumerable<StoredEvent<TState, TTrigger>>>? _ordering;
    private int? _skip;
    private int? _take;

    /// <summary>
    /// Creates a new query builder with the specified event source.
    /// </summary>
    /// <param name="eventSource">Function that retrieves all events.</param>
    public StateHistoryQueryBuilder(
        Func<CancellationToken, Task<IReadOnlyList<StoredEvent<TState, TTrigger>>>> eventSource)
    {
        _eventSource = eventSource;
    }

    /// <summary>
    /// Creates a new query builder from a persistence provider.
    /// </summary>
    /// <param name="persistence">The persistence provider.</param>
    /// <param name="streamId">The stream ID to query.</param>
    public StateHistoryQueryBuilder(
        IEventStore<TState, TTrigger> persistence,
        string streamId)
    {
        _eventSource = ct => persistence.ReadAllEventsAsync(streamId, ct);
    }

    /// <inheritdoc/>
    public IStateHistoryQuery<TState, TTrigger> InTimeRange(DateTime from, DateTime to)
    {
        _filters.Add(e => e.Timestamp >= from && e.Timestamp <= to);
        return this;
    }

    /// <inheritdoc/>
    public IStateHistoryQuery<TState, TTrigger> After(DateTime after)
    {
        _filters.Add(e => e.Timestamp > after);
        return this;
    }

    /// <inheritdoc/>
    public IStateHistoryQuery<TState, TTrigger> Before(DateTime before)
    {
        _filters.Add(e => e.Timestamp < before);
        return this;
    }

    /// <inheritdoc/>
    public IStateHistoryQuery<TState, TTrigger> FromState(TState state)
    {
        _filters.Add(e => EqualityComparer<TState>.Default.Equals(e.FromState, state));
        return this;
    }

    /// <inheritdoc/>
    public IStateHistoryQuery<TState, TTrigger> ToState(TState state)
    {
        _filters.Add(e => EqualityComparer<TState>.Default.Equals(e.ToState, state));
        return this;
    }

    /// <inheritdoc/>
    public IStateHistoryQuery<TState, TTrigger> WithTrigger(TTrigger trigger)
    {
        _filters.Add(e => EqualityComparer<TTrigger>.Default.Equals(e.Trigger, trigger));
        return this;
    }

    /// <inheritdoc/>
    public IStateHistoryQuery<TState, TTrigger> WithTriggers(params TTrigger[] triggers)
    {
        var triggerSet = new HashSet<TTrigger>(triggers);
        _filters.Add(e => triggerSet.Contains(e.Trigger));
        return this;
    }

    /// <inheritdoc/>
    public IStateHistoryQuery<TState, TTrigger> WithCorrelationId(string correlationId)
    {
        _filters.Add(e => e.CorrelationId == correlationId);
        return this;
    }

    /// <inheritdoc/>
    public IStateHistoryQuery<TState, TTrigger> WithMetadata(Func<Dictionary<string, object>?, bool> predicate)
    {
        _filters.Add(e => predicate(e.Metadata));
        return this;
    }

    /// <inheritdoc/>
    public IStateHistoryQuery<TState, TTrigger> InVersionRange(long fromVersion, long toVersion)
    {
        _filters.Add(e => e.SequenceNumber >= fromVersion && e.SequenceNumber <= toVersion);
        return this;
    }

    /// <inheritdoc/>
    public IStateHistoryQuery<TState, TTrigger> OrderByTime()
    {
        _ordering = events => events.OrderBy(e => e.Timestamp);
        return this;
    }

    /// <inheritdoc/>
    public IStateHistoryQuery<TState, TTrigger> OrderByTimeDescending()
    {
        _ordering = events => events.OrderByDescending(e => e.Timestamp);
        return this;
    }

    /// <inheritdoc/>
    public IStateHistoryQuery<TState, TTrigger> Take(int count)
    {
        _take = count;
        return this;
    }

    /// <inheritdoc/>
    public IStateHistoryQuery<TState, TTrigger> Skip(int count)
    {
        _skip = count;
        return this;
    }

    /// <inheritdoc/>
    public async Task<IReadOnlyList<StoredEvent<TState, TTrigger>>> ExecuteAsync(
        CancellationToken cancellationToken = default)
    {
        var events = await _eventSource(cancellationToken);
        return ApplyQuery(events).ToList();
    }

    /// <inheritdoc/>
    public async Task<StoredEvent<TState, TTrigger>?> FirstOrDefaultAsync(
        CancellationToken cancellationToken = default)
    {
        var events = await _eventSource(cancellationToken);
        return ApplyQuery(events).FirstOrDefault();
    }

    /// <inheritdoc/>
    public async Task<int> CountAsync(CancellationToken cancellationToken = default)
    {
        var events = await _eventSource(cancellationToken);
        return ApplyFilters(events).Count();
    }

    /// <inheritdoc/>
    public async Task<bool> AnyAsync(CancellationToken cancellationToken = default)
    {
        var events = await _eventSource(cancellationToken);
        return ApplyFilters(events).Any();
    }

    /// <inheritdoc/>
    public async Task<IReadOnlyDictionary<TState, StateStatistics>> GroupByStateAsync(
        CancellationToken cancellationToken = default)
    {
        var events = await _eventSource(cancellationToken);
        var filteredEvents = ApplyFilters(events).ToList();

        var result = new Dictionary<TState, StateStatistics>();

        // Collect all states
        var allStates = filteredEvents
            .SelectMany(e => new[] { e.FromState, e.ToState })
            .Distinct()
            .ToList();

        foreach (var state in allStates)
        {
            var entriesTo = filteredEvents.Where(e =>
                EqualityComparer<TState>.Default.Equals(e.ToState, state)).ToList();
            var exitsFrom = filteredEvents.Where(e =>
                EqualityComparer<TState>.Default.Equals(e.FromState, state)).ToList();

            // Calculate duration in state
            var durations = CalculateStateDurations(filteredEvents, state);

            result[state] = new StateStatistics
            {
                EntryCount = entriesTo.Count,
                ExitCount = exitsFrom.Count,
                TotalDuration = durations.total,
                AverageDuration = durations.count > 0 ? TimeSpan.FromTicks(durations.total.Ticks / durations.count) : TimeSpan.Zero,
                MinDuration = durations.min,
                MaxDuration = durations.max,
                FirstEntry = entriesTo.OrderBy(e => e.Timestamp).FirstOrDefault()?.Timestamp,
                LastEntry = entriesTo.OrderByDescending(e => e.Timestamp).FirstOrDefault()?.Timestamp
            };
        }

        return result;
    }

    /// <inheritdoc/>
    public async Task<IReadOnlyDictionary<TTrigger, TriggerStatistics>> GroupByTriggerAsync(
        CancellationToken cancellationToken = default)
    {
        var events = await _eventSource(cancellationToken);
        var filteredEvents = ApplyFilters(events).ToList();

        return filteredEvents
            .GroupBy(e => e.Trigger)
            .ToDictionary(
                g => g.Key,
                g => new TriggerStatistics
                {
                    FireCount = g.Count(),
                    DistinctSourceStates = g.Select(e => e.FromState).Distinct().Count(),
                    DistinctTargetStates = g.Select(e => e.ToState).Distinct().Count(),
                    FirstFired = g.OrderBy(e => e.Timestamp).First().Timestamp,
                    LastFired = g.OrderByDescending(e => e.Timestamp).First().Timestamp
                });
    }

    /// <inheritdoc/>
    public async Task<IReadOnlyList<TimePeriodStatistics<TState, TTrigger>>> GroupByTimeAsync(
        TimePeriodType periodType,
        CancellationToken cancellationToken = default)
    {
        var events = await _eventSource(cancellationToken);
        var filteredEvents = ApplyFilters(events).OrderBy(e => e.Timestamp).ToList();

        if (filteredEvents.Count == 0)
        {
            return [];
        }

        var result = new List<TimePeriodStatistics<TState, TTrigger>>();
        var grouped = filteredEvents.GroupBy(e => GetPeriodStart(e.Timestamp, periodType));

        foreach (var group in grouped.OrderBy(g => g.Key))
        {
            var periodEvents = group.ToList();
            result.Add(new TimePeriodStatistics<TState, TTrigger>
            {
                PeriodStart = group.Key,
                PeriodEnd = GetPeriodEnd(group.Key, periodType),
                EventCount = periodEvents.Count,
                DistinctStates = periodEvents.SelectMany(e => new[] { e.FromState, e.ToState }).Distinct().Count(),
                DistinctTriggers = periodEvents.Select(e => e.Trigger).Distinct().Count(),
                Events = periodEvents
            });
        }

        return result;
    }

    private IEnumerable<StoredEvent<TState, TTrigger>> ApplyQuery(
        IReadOnlyList<StoredEvent<TState, TTrigger>> events)
    {
        IEnumerable<StoredEvent<TState, TTrigger>> result = ApplyFilters(events);

        // Apply ordering
        if (_ordering != null)
        {
            result = _ordering(result);
        }

        // Apply skip
        if (_skip.HasValue)
        {
            result = result.Skip(_skip.Value);
        }

        // Apply take
        if (_take.HasValue)
        {
            result = result.Take(_take.Value);
        }

        return result;
    }

    private IEnumerable<StoredEvent<TState, TTrigger>> ApplyFilters(
        IReadOnlyList<StoredEvent<TState, TTrigger>> events)
    {
        IEnumerable<StoredEvent<TState, TTrigger>> result = events;

        foreach (var filter in _filters)
        {
            result = result.Where(filter);
        }

        return result;
    }

    private (TimeSpan total, TimeSpan min, TimeSpan max, int count) CalculateStateDurations(
        List<StoredEvent<TState, TTrigger>> events,
        TState state)
    {
        var orderedEvents = events.OrderBy(e => e.Timestamp).ToList();
        var durations = new List<TimeSpan>();

        DateTime? lastEntryTime = null;

        foreach (var evt in orderedEvents)
        {
            // Entry to this state
            if (EqualityComparer<TState>.Default.Equals(evt.ToState, state))
            {
                lastEntryTime = evt.Timestamp;
            }
            // Exit from this state
            else if (EqualityComparer<TState>.Default.Equals(evt.FromState, state) && lastEntryTime.HasValue)
            {
                durations.Add(evt.Timestamp - lastEntryTime.Value);
                lastEntryTime = null;
            }
        }

        if (durations.Count == 0)
        {
            return (TimeSpan.Zero, TimeSpan.Zero, TimeSpan.Zero, 0);
        }

        return (
            total: TimeSpan.FromTicks(durations.Sum(d => d.Ticks)),
            min: durations.Min(),
            max: durations.Max(),
            count: durations.Count
        );
    }

    private static DateTime GetPeriodStart(DateTime timestamp, TimePeriodType periodType)
    {
        return periodType switch
        {
            TimePeriodType.Hour => new DateTime(timestamp.Year, timestamp.Month, timestamp.Day, timestamp.Hour, 0, 0, timestamp.Kind),
            TimePeriodType.Day => new DateTime(timestamp.Year, timestamp.Month, timestamp.Day, 0, 0, 0, timestamp.Kind),
            TimePeriodType.Week => GetWeekStart(timestamp),
            TimePeriodType.Month => new DateTime(timestamp.Year, timestamp.Month, 1, 0, 0, 0, timestamp.Kind),
            _ => throw new ArgumentOutOfRangeException(nameof(periodType))
        };
    }

    private static DateTime GetPeriodEnd(DateTime periodStart, TimePeriodType periodType)
    {
        return periodType switch
        {
            TimePeriodType.Hour => periodStart.AddHours(1).AddTicks(-1),
            TimePeriodType.Day => periodStart.AddDays(1).AddTicks(-1),
            TimePeriodType.Week => periodStart.AddDays(7).AddTicks(-1),
            TimePeriodType.Month => periodStart.AddMonths(1).AddTicks(-1),
            _ => throw new ArgumentOutOfRangeException(nameof(periodType))
        };
    }

    private static DateTime GetWeekStart(DateTime date)
    {
        var diff = (7 + (date.DayOfWeek - DayOfWeek.Monday)) % 7;
        return new DateTime(date.Year, date.Month, date.Day, 0, 0, 0, date.Kind).AddDays(-diff);
    }
}

/// <summary>
/// Extension methods for creating state history queries.
/// </summary>
public static class StateHistoryQueryExtensions
{
    /// <summary>
    /// Creates a new query builder for the specified stream.
    /// </summary>
    /// <typeparam name="TState">The type representing the states.</typeparam>
    /// <typeparam name="TTrigger">The type representing the triggers.</typeparam>
    /// <param name="persistence">The persistence provider.</param>
    /// <param name="streamId">The stream ID to query.</param>
    /// <returns>A new query builder.</returns>
    public static IStateHistoryQuery<TState, TTrigger> Query<TState, TTrigger>(
        this IEventStore<TState, TTrigger> persistence,
        string streamId)
        where TState : notnull
        where TTrigger : notnull
    {
        return new StateHistoryQueryBuilder<TState, TTrigger>(persistence, streamId);
    }

    /// <summary>
    /// Creates a new query builder from the combined persistence provider.
    /// </summary>
    /// <typeparam name="TState">The type representing the states.</typeparam>
    /// <typeparam name="TTrigger">The type representing the triggers.</typeparam>
    /// <param name="persistence">The persistence provider.</param>
    /// <param name="streamId">The stream ID to query.</param>
    /// <returns>A new query builder.</returns>
    public static IStateHistoryQuery<TState, TTrigger> Query<TState, TTrigger>(
        this IStateMachinePersistence<TState, TTrigger> persistence,
        string streamId)
        where TState : notnull
        where TTrigger : notnull
    {
        return new StateHistoryQueryBuilder<TState, TTrigger>(persistence, streamId);
    }

    /// <summary>
    /// Filters to events that occurred today.
    /// </summary>
    public static IStateHistoryQuery<TState, TTrigger> Today<TState, TTrigger>(
        this IStateHistoryQuery<TState, TTrigger> query)
        where TState : notnull
        where TTrigger : notnull
    {
        var today = DateTime.UtcNow.Date;
        return query.InTimeRange(today, today.AddDays(1).AddTicks(-1));
    }

    /// <summary>
    /// Filters to events that occurred in the last N hours.
    /// </summary>
    public static IStateHistoryQuery<TState, TTrigger> LastHours<TState, TTrigger>(
        this IStateHistoryQuery<TState, TTrigger> query,
        int hours)
        where TState : notnull
        where TTrigger : notnull
    {
        return query.After(DateTime.UtcNow.AddHours(-hours));
    }

    /// <summary>
    /// Filters to events that occurred in the last N days.
    /// </summary>
    public static IStateHistoryQuery<TState, TTrigger> LastDays<TState, TTrigger>(
        this IStateHistoryQuery<TState, TTrigger> query,
        int days)
        where TState : notnull
        where TTrigger : notnull
    {
        return query.After(DateTime.UtcNow.AddDays(-days));
    }

    /// <summary>
    /// Gets the most recent N events.
    /// </summary>
    public static IStateHistoryQuery<TState, TTrigger> MostRecent<TState, TTrigger>(
        this IStateHistoryQuery<TState, TTrigger> query,
        int count)
        where TState : notnull
        where TTrigger : notnull
    {
        return query.OrderByTimeDescending().Take(count);
    }
}
