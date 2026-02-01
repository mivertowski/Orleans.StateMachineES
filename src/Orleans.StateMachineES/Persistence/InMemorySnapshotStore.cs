using System.Collections.Concurrent;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace Orleans.StateMachineES.Persistence;

/// <summary>
/// In-memory implementation of snapshot store for development and testing.
/// Not suitable for production use as data is not persisted across restarts.
/// </summary>
/// <typeparam name="TState">The type representing the states.</typeparam>
public class InMemorySnapshotStore<TState> : ISnapshotStore<TState>, ISnapshotStore
    where TState : notnull
{
    private readonly ConcurrentDictionary<string, List<SnapshotInfo<TState>>> _snapshots = new();
    private readonly SemaphoreSlim _globalLock = new(1, 1);

    /// <inheritdoc/>
    public string ProviderName => "InMemory";

    /// <inheritdoc/>
    public async Task<bool> SaveSnapshotAsync(
        SnapshotInfo<TState> snapshot,
        SnapshotSaveOptions? options = null,
        CancellationToken cancellationToken = default)
    {
        options ??= new SnapshotSaveOptions();

        var snapshots = _snapshots.GetOrAdd(snapshot.StreamId, _ => []);

        await _globalLock.WaitAsync(cancellationToken);
        try
        {
            // Calculate checksum if requested
            if (options.CalculateChecksum)
            {
                snapshot.Checksum = CalculateChecksum(snapshot);
            }

            // Add to list
            lock (snapshots)
            {
                snapshots.Add(snapshot);

                // Sort by version descending
                snapshots.Sort((a, b) => b.Version.CompareTo(a.Version));

                // Prune old snapshots
                while (snapshots.Count > options.MaxSnapshotsToRetain)
                {
                    snapshots.RemoveAt(snapshots.Count - 1);
                }
            }

            return true;
        }
        finally
        {
            _globalLock.Release();
        }
    }

    /// <inheritdoc/>
    public Task<SnapshotInfo<TState>?> LoadSnapshotAsync(
        string streamId,
        SnapshotLoadOptions? options = null,
        CancellationToken cancellationToken = default)
    {
        options ??= new SnapshotLoadOptions();

        if (!_snapshots.TryGetValue(streamId, out var snapshots))
        {
            if (options.RequireSnapshot)
            {
                throw new SnapshotStoreException($"No snapshot found for stream '{streamId}'", streamId);
            }
            return Task.FromResult<SnapshotInfo<TState>?>(null);
        }

        lock (snapshots)
        {
            if (snapshots.Count == 0)
            {
                if (options.RequireSnapshot)
                {
                    throw new SnapshotStoreException($"No snapshot found for stream '{streamId}'", streamId);
                }
                return Task.FromResult<SnapshotInfo<TState>?>(null);
            }

            // Get the most recent snapshot
            var snapshot = options.PreferMostRecent
                ? snapshots.First()
                : snapshots.Last();

            // Check age limit
            if (options.MaxAge.HasValue)
            {
                var age = DateTime.UtcNow - snapshot.CreatedAt;
                if (age > options.MaxAge.Value)
                {
                    if (options.RequireSnapshot)
                    {
                        throw new SnapshotStoreException(
                            $"Snapshot for stream '{streamId}' is too old ({age.TotalMinutes:F1} minutes)", streamId);
                    }
                    return Task.FromResult<SnapshotInfo<TState>?>(null);
                }
            }

            // Verify checksum
            if (options.VerifyChecksum && !string.IsNullOrEmpty(snapshot.Checksum))
            {
                var calculatedChecksum = CalculateChecksum(snapshot);
                if (calculatedChecksum != snapshot.Checksum)
                {
                    throw new ChecksumValidationException(streamId, snapshot.Checksum, calculatedChecksum);
                }
            }

            return Task.FromResult<SnapshotInfo<TState>?>(snapshot);
        }
    }

    /// <inheritdoc/>
    public Task<SnapshotInfo<TState>?> LoadSnapshotAtVersionAsync(
        string streamId,
        long maxVersion,
        SnapshotLoadOptions? options = null,
        CancellationToken cancellationToken = default)
    {
        options ??= new SnapshotLoadOptions();

        if (!_snapshots.TryGetValue(streamId, out var snapshots))
        {
            return Task.FromResult<SnapshotInfo<TState>?>(null);
        }

        lock (snapshots)
        {
            var snapshot = snapshots
                .Where(s => s.Version <= maxVersion)
                .OrderByDescending(s => s.Version)
                .FirstOrDefault();

            if (snapshot != null && options.VerifyChecksum && !string.IsNullOrEmpty(snapshot.Checksum))
            {
                var calculatedChecksum = CalculateChecksum(snapshot);
                if (calculatedChecksum != snapshot.Checksum)
                {
                    throw new ChecksumValidationException(streamId, snapshot.Checksum, calculatedChecksum);
                }
            }

            return Task.FromResult(snapshot);
        }
    }

    /// <inheritdoc/>
    public Task<IReadOnlyList<SnapshotInfo<TState>>> GetAllSnapshotsAsync(
        string streamId,
        CancellationToken cancellationToken = default)
    {
        if (!_snapshots.TryGetValue(streamId, out var snapshots))
        {
            return Task.FromResult<IReadOnlyList<SnapshotInfo<TState>>>([]);
        }

        lock (snapshots)
        {
            return Task.FromResult<IReadOnlyList<SnapshotInfo<TState>>>(snapshots.ToList());
        }
    }

    /// <inheritdoc/>
    public Task<bool> DeleteSnapshotAsync(
        string streamId,
        string snapshotId,
        CancellationToken cancellationToken = default)
    {
        if (!_snapshots.TryGetValue(streamId, out var snapshots))
        {
            return Task.FromResult(false);
        }

        lock (snapshots)
        {
            var index = snapshots.FindIndex(s => s.SnapshotId == snapshotId);
            if (index >= 0)
            {
                snapshots.RemoveAt(index);
                return Task.FromResult(true);
            }
        }

        return Task.FromResult(false);
    }

    /// <inheritdoc/>
    public Task<int> DeleteAllSnapshotsAsync(
        string streamId,
        CancellationToken cancellationToken = default)
    {
        if (_snapshots.TryRemove(streamId, out var snapshots))
        {
            lock (snapshots)
            {
                return Task.FromResult(snapshots.Count);
            }
        }

        return Task.FromResult(0);
    }

    /// <inheritdoc/>
    public Task<int> PruneSnapshotsAsync(
        string streamId,
        long beforeVersion,
        int keepCount = 1,
        CancellationToken cancellationToken = default)
    {
        if (!_snapshots.TryGetValue(streamId, out var snapshots))
        {
            return Task.FromResult(0);
        }

        var deleted = 0;
        lock (snapshots)
        {
            // Keep the most recent ones
            var toKeep = snapshots.Take(keepCount).ToList();

            // Remove old ones
            var toRemove = snapshots
                .Skip(keepCount)
                .Where(s => s.Version < beforeVersion)
                .ToList();

            foreach (var snapshot in toRemove)
            {
                snapshots.Remove(snapshot);
                deleted++;
            }
        }

        return Task.FromResult(deleted);
    }

    /// <inheritdoc/>
    public Task<bool> HasSnapshotAsync(
        string streamId,
        CancellationToken cancellationToken = default)
    {
        if (!_snapshots.TryGetValue(streamId, out var snapshots))
        {
            return Task.FromResult(false);
        }

        lock (snapshots)
        {
            return Task.FromResult(snapshots.Count > 0);
        }
    }

    /// <inheritdoc/>
    public Task<bool> IsHealthyAsync(CancellationToken cancellationToken = default)
    {
        return Task.FromResult(true);
    }

    /// <inheritdoc/>
    public Task<SnapshotStoreStats> GetStatsAsync(CancellationToken cancellationToken = default)
    {
        var stats = new SnapshotStoreStats
        {
            StreamCount = _snapshots.Count,
            StorageSizeBytes = 0 // In-memory, no meaningful size
        };

        var allSnapshots = _snapshots.Values
            .SelectMany(s =>
            {
                lock (s)
                {
                    return s.ToList();
                }
            })
            .ToList();

        stats.SnapshotCount = allSnapshots.Count;

        if (allSnapshots.Count > 0)
        {
            stats.OldestSnapshotTimestamp = allSnapshots.Min(s => s.CreatedAt);
            stats.NewestSnapshotTimestamp = allSnapshots.Max(s => s.CreatedAt);
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
            _snapshots.Clear();
        }
        finally
        {
            _globalLock.Release();
        }
    }

    private static string CalculateChecksum(SnapshotInfo<TState> snapshot)
    {
        // Create a string representation for hashing
        var content = JsonSerializer.Serialize(new
        {
            snapshot.StreamId,
            snapshot.State,
            snapshot.Version,
            snapshot.TransitionCount,
            snapshot.StateMachineVersion
        });

        var bytes = SHA256.HashData(Encoding.UTF8.GetBytes(content));
        return Convert.ToBase64String(bytes);
    }
}
