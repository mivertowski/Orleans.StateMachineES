using System.Collections.Concurrent;

namespace Orleans.StateMachineES.Persistence;

/// <summary>
/// In-memory implementation of event store for development and testing.
/// Not suitable for production use as data is not persisted across restarts.
/// </summary>
/// <typeparam name="TState">The type representing the states.</typeparam>
/// <typeparam name="TTrigger">The type representing the triggers.</typeparam>
public class InMemoryEventStore<TState, TTrigger> : IEventStore<TState, TTrigger>, IEventStore
    where TState : notnull
    where TTrigger : notnull
{
    private readonly ConcurrentDictionary<string, StreamData> _streams = new();
    private readonly ConcurrentDictionary<string, List<Func<StoredEvent<TState, TTrigger>, Task>>> _subscriptions = new();
    private readonly SemaphoreSlim _globalLock = new(1, 1);
    private long _totalEventCount;

    /// <inheritdoc/>
    public string ProviderName => "InMemory";

    private class StreamData
    {
        public List<StoredEvent<TState, TTrigger>> Events { get; } = [];
        public SemaphoreSlim Lock { get; } = new(1, 1);
        public long Version => Events.Count;
    }

    /// <inheritdoc/>
    public async Task<AppendResult> AppendEventsAsync(
        string streamId,
        IEnumerable<StoredEvent<TState, TTrigger>> events,
        long expectedVersion,
        CancellationToken cancellationToken = default)
    {
        var eventList = events.ToList();
        if (eventList.Count == 0)
        {
            return AppendResult.Succeeded(0, 0, 0);
        }

        var stream = _streams.GetOrAdd(streamId, _ => new StreamData());
        await stream.Lock.WaitAsync(cancellationToken);
        try
        {
            // Check expected version
            if (expectedVersion != ExpectedVersion.Any)
            {
                if (expectedVersion == ExpectedVersion.NoStream && stream.Events.Count > 0)
                {
                    return AppendResult.Failed($"Stream '{streamId}' already exists");
                }
                if (expectedVersion == ExpectedVersion.StreamExists && stream.Events.Count == 0)
                {
                    return AppendResult.Failed($"Stream '{streamId}' does not exist");
                }
                if (expectedVersion >= 0 && stream.Version != expectedVersion)
                {
                    return AppendResult.Failed($"Expected version {expectedVersion}, but current version is {stream.Version}");
                }
            }

            var firstPosition = stream.Events.Count;

            // Assign sequence numbers and add events
            foreach (var evt in eventList)
            {
                evt.SequenceNumber = stream.Events.Count;
                evt.StreamId = streamId;
                stream.Events.Add(evt);
                Interlocked.Increment(ref _totalEventCount);
            }

            var newVersion = stream.Version;

            // Notify subscribers
            if (_subscriptions.TryGetValue(streamId, out var subscribers))
            {
                foreach (var subscriber in subscribers.ToList())
                {
                    foreach (var evt in eventList)
                    {
                        try
                        {
                            await subscriber(evt);
                        }
                        catch
                        {
                            // Ignore subscriber errors
                        }
                    }
                }
            }

            return AppendResult.Succeeded(newVersion, firstPosition, eventList.Count);
        }
        finally
        {
            stream.Lock.Release();
        }
    }

    /// <inheritdoc/>
    public async Task<IReadOnlyList<StoredEvent<TState, TTrigger>>> ReadEventsAsync(
        string streamId,
        long fromVersion,
        int count,
        CancellationToken cancellationToken = default)
    {
        if (!_streams.TryGetValue(streamId, out var stream))
        {
            return [];
        }

        await stream.Lock.WaitAsync(cancellationToken);
        try
        {
            var start = (int)Math.Max(0, fromVersion);
            var take = Math.Min(count, stream.Events.Count - start);
            if (take <= 0)
            {
                return [];
            }

            return stream.Events.Skip(start).Take(take).ToList();
        }
        finally
        {
            stream.Lock.Release();
        }
    }

    /// <inheritdoc/>
    public async Task<IReadOnlyList<StoredEvent<TState, TTrigger>>> ReadAllEventsAsync(
        string streamId,
        CancellationToken cancellationToken = default)
    {
        if (!_streams.TryGetValue(streamId, out var stream))
        {
            return [];
        }

        await stream.Lock.WaitAsync(cancellationToken);
        try
        {
            return stream.Events.ToList();
        }
        finally
        {
            stream.Lock.Release();
        }
    }

    /// <inheritdoc/>
    public async Task<IReadOnlyList<StoredEvent<TState, TTrigger>>> ReadEventsBackwardAsync(
        string streamId,
        long fromVersion,
        int count,
        CancellationToken cancellationToken = default)
    {
        if (!_streams.TryGetValue(streamId, out var stream))
        {
            return [];
        }

        await stream.Lock.WaitAsync(cancellationToken);
        try
        {
            var end = (int)Math.Min(fromVersion, stream.Events.Count - 1);
            var start = Math.Max(0, end - count + 1);
            var take = end - start + 1;

            if (take <= 0)
            {
                return [];
            }

            return stream.Events.Skip(start).Take(take).Reverse().ToList();
        }
        finally
        {
            stream.Lock.Release();
        }
    }

    /// <inheritdoc/>
    public Task<long> GetStreamVersionAsync(
        string streamId,
        CancellationToken cancellationToken = default)
    {
        if (!_streams.TryGetValue(streamId, out var stream))
        {
            return Task.FromResult(-1L);
        }

        return Task.FromResult(stream.Version);
    }

    /// <inheritdoc/>
    public Task<bool> StreamExistsAsync(
        string streamId,
        CancellationToken cancellationToken = default)
    {
        return Task.FromResult(_streams.ContainsKey(streamId));
    }

    /// <inheritdoc/>
    public async Task<bool> DeleteStreamAsync(
        string streamId,
        long expectedVersion,
        CancellationToken cancellationToken = default)
    {
        if (!_streams.TryGetValue(streamId, out var stream))
        {
            return false;
        }

        await stream.Lock.WaitAsync(cancellationToken);
        try
        {
            if (expectedVersion >= 0 && stream.Version != expectedVersion)
            {
                throw new ConcurrencyException(streamId, expectedVersion, stream.Version);
            }

            var eventCount = stream.Events.Count;
            if (_streams.TryRemove(streamId, out _))
            {
                Interlocked.Add(ref _totalEventCount, -eventCount);
                return true;
            }

            return false;
        }
        finally
        {
            stream.Lock.Release();
        }
    }

    /// <inheritdoc/>
    public Task<long> GetTotalEventCountAsync(CancellationToken cancellationToken = default)
    {
        return Task.FromResult(Interlocked.Read(ref _totalEventCount));
    }

    /// <inheritdoc/>
    public Task<IAsyncDisposable> SubscribeAsync(
        string streamId,
        long fromVersion,
        Func<StoredEvent<TState, TTrigger>, Task> onEvent,
        CancellationToken cancellationToken = default)
    {
        var subscriptions = _subscriptions.GetOrAdd(streamId, _ => []);
        lock (subscriptions)
        {
            subscriptions.Add(onEvent);
        }

        var subscription = new Subscription(() =>
        {
            if (_subscriptions.TryGetValue(streamId, out var subs))
            {
                lock (subs)
                {
                    subs.Remove(onEvent);
                }
            }
        });

        return Task.FromResult<IAsyncDisposable>(subscription);
    }

    /// <inheritdoc/>
    public Task<bool> IsHealthyAsync(CancellationToken cancellationToken = default)
    {
        return Task.FromResult(true);
    }

    /// <inheritdoc/>
    public Task<EventStoreStats> GetStatsAsync(CancellationToken cancellationToken = default)
    {
        var stats = new EventStoreStats
        {
            StreamCount = _streams.Count,
            EventCount = Interlocked.Read(ref _totalEventCount),
            StorageSizeBytes = 0 // In-memory, no meaningful size
        };

        if (_streams.Count > 0)
        {
            var allEvents = _streams.Values
                .SelectMany(s => s.Events)
                .OrderBy(e => e.Timestamp)
                .ToList();

            if (allEvents.Count > 0)
            {
                stats.OldestEventTimestamp = allEvents.First().Timestamp;
                stats.NewestEventTimestamp = allEvents.Last().Timestamp;
            }
        }

        return Task.FromResult(stats);
    }

    /// <summary>
    /// Clears all data from the in-memory store.
    /// </summary>
    public async Task ClearAsync()
    {
        await _globalLock.WaitAsync();
        try
        {
            _streams.Clear();
            _subscriptions.Clear();
            Interlocked.Exchange(ref _totalEventCount, 0);
        }
        finally
        {
            _globalLock.Release();
        }
    }

    private class Subscription : IAsyncDisposable
    {
        private readonly Action _unsubscribe;
        private bool _disposed;

        public Subscription(Action unsubscribe)
        {
            _unsubscribe = unsubscribe;
        }

        public ValueTask DisposeAsync()
        {
            if (!_disposed)
            {
                _disposed = true;
                _unsubscribe();
            }
            return ValueTask.CompletedTask;
        }
    }
}
