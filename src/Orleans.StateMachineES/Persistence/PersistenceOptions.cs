namespace Orleans.StateMachineES.Persistence;

/// <summary>
/// Configuration options for state machine persistence.
/// </summary>
public class PersistenceOptions
{
    /// <summary>
    /// Gets or sets the provider name for event storage.
    /// Default: "InMemory"
    /// </summary>
    public string EventStoreProvider { get; set; } = "InMemory";

    /// <summary>
    /// Gets or sets the provider name for snapshot storage.
    /// Default: "InMemory"
    /// </summary>
    public string SnapshotStoreProvider { get; set; } = "InMemory";

    /// <summary>
    /// Gets or sets whether snapshots are enabled.
    /// Default: true
    /// </summary>
    public bool EnableSnapshots { get; set; } = true;

    /// <summary>
    /// Gets or sets the interval (in events) between automatic snapshots.
    /// Default: 100
    /// </summary>
    public int SnapshotInterval { get; set; } = 100;

    /// <summary>
    /// Gets or sets the maximum number of snapshots to retain per stream.
    /// Default: 5
    /// </summary>
    public int MaxSnapshotsPerStream { get; set; } = 5;

    /// <summary>
    /// Gets or sets whether to automatically prune old snapshots.
    /// Default: true
    /// </summary>
    public bool AutoPruneSnapshots { get; set; } = true;

    /// <summary>
    /// Gets or sets whether to enable event caching.
    /// Default: true
    /// </summary>
    public bool EnableEventCaching { get; set; } = true;

    /// <summary>
    /// Gets or sets the maximum number of events to cache per stream.
    /// Default: 1000
    /// </summary>
    public int MaxCachedEventsPerStream { get; set; } = 1000;

    /// <summary>
    /// Gets or sets the cache expiration time.
    /// Default: 5 minutes
    /// </summary>
    public TimeSpan CacheExpiration { get; set; } = TimeSpan.FromMinutes(5);

    /// <summary>
    /// Gets or sets whether to enable compression for stored events.
    /// Default: false
    /// </summary>
    public bool EnableCompression { get; set; } = false;

    /// <summary>
    /// Gets or sets the compression level (0-9).
    /// Default: 6
    /// </summary>
    public int CompressionLevel { get; set; } = 6;

    /// <summary>
    /// Gets or sets the connection string for the event store.
    /// </summary>
    public string? EventStoreConnectionString { get; set; }

    /// <summary>
    /// Gets or sets the connection string for the snapshot store.
    /// </summary>
    public string? SnapshotStoreConnectionString { get; set; }

    /// <summary>
    /// Gets or sets whether to use optimistic concurrency.
    /// Default: true
    /// </summary>
    public bool UseOptimisticConcurrency { get; set; } = true;

    /// <summary>
    /// Gets or sets the retry policy for transient failures.
    /// </summary>
    public RetryOptions RetryPolicy { get; set; } = new();

    /// <summary>
    /// Gets or sets whether to verify checksums when loading snapshots.
    /// Default: true
    /// </summary>
    public bool VerifyChecksums { get; set; } = true;

    /// <summary>
    /// Gets or sets whether to enable event archival.
    /// Default: false
    /// </summary>
    public bool EnableArchival { get; set; } = false;

    /// <summary>
    /// Gets or sets the age after which events can be archived.
    /// Default: 30 days
    /// </summary>
    public TimeSpan ArchivalAge { get; set; } = TimeSpan.FromDays(30);

    /// <summary>
    /// Gets or sets whether to enable distributed tracing for persistence operations.
    /// Default: true
    /// </summary>
    public bool EnableTracing { get; set; } = true;

    /// <summary>
    /// Gets or sets the batch size for bulk operations.
    /// Default: 100
    /// </summary>
    public int BatchSize { get; set; } = 100;

    /// <summary>
    /// Gets or sets the timeout for persistence operations.
    /// Default: 30 seconds
    /// </summary>
    public TimeSpan OperationTimeout { get; set; } = TimeSpan.FromSeconds(30);
}

/// <summary>
/// Retry policy options for persistence operations.
/// </summary>
public class RetryOptions
{
    /// <summary>
    /// Gets or sets the maximum number of retry attempts.
    /// Default: 3
    /// </summary>
    public int MaxRetries { get; set; } = 3;

    /// <summary>
    /// Gets or sets the initial delay between retries.
    /// Default: 100ms
    /// </summary>
    public TimeSpan InitialDelay { get; set; } = TimeSpan.FromMilliseconds(100);

    /// <summary>
    /// Gets or sets the maximum delay between retries.
    /// Default: 5 seconds
    /// </summary>
    public TimeSpan MaxDelay { get; set; } = TimeSpan.FromSeconds(5);

    /// <summary>
    /// Gets or sets the backoff multiplier.
    /// Default: 2.0
    /// </summary>
    public double BackoffMultiplier { get; set; } = 2.0;

    /// <summary>
    /// Gets or sets whether to add jitter to retry delays.
    /// Default: true
    /// </summary>
    public bool AddJitter { get; set; } = true;
}

/// <summary>
/// Options for CosmosDB persistence provider.
/// </summary>
public class CosmosDbPersistenceOptions
{
    /// <summary>
    /// Gets or sets the CosmosDB endpoint.
    /// </summary>
    public string? Endpoint { get; set; }

    /// <summary>
    /// Gets or sets the CosmosDB key.
    /// </summary>
    public string? Key { get; set; }

    /// <summary>
    /// Gets or sets the database name.
    /// Default: "StateMachine"
    /// </summary>
    public string DatabaseName { get; set; } = "StateMachine";

    /// <summary>
    /// Gets or sets the events container name.
    /// Default: "Events"
    /// </summary>
    public string EventsContainerName { get; set; } = "Events";

    /// <summary>
    /// Gets or sets the snapshots container name.
    /// Default: "Snapshots"
    /// </summary>
    public string SnapshotsContainerName { get; set; } = "Snapshots";

    /// <summary>
    /// Gets or sets the throughput (RU/s) for containers.
    /// Default: 400
    /// </summary>
    public int Throughput { get; set; } = 400;

    /// <summary>
    /// Gets or sets whether to use serverless mode.
    /// Default: false
    /// </summary>
    public bool UseServerless { get; set; } = false;
}

/// <summary>
/// Options for PostgreSQL persistence provider.
/// </summary>
public class PostgreSqlPersistenceOptions
{
    /// <summary>
    /// Gets or sets the connection string.
    /// </summary>
    public string? ConnectionString { get; set; }

    /// <summary>
    /// Gets or sets the schema name.
    /// Default: "state_machine"
    /// </summary>
    public string SchemaName { get; set; } = "state_machine";

    /// <summary>
    /// Gets or sets the events table name.
    /// Default: "events"
    /// </summary>
    public string EventsTableName { get; set; } = "events";

    /// <summary>
    /// Gets or sets the snapshots table name.
    /// Default: "snapshots"
    /// </summary>
    public string SnapshotsTableName { get; set; } = "snapshots";

    /// <summary>
    /// Gets or sets whether to auto-create the schema.
    /// Default: true
    /// </summary>
    public bool AutoCreateSchema { get; set; } = true;

    /// <summary>
    /// Gets or sets whether to use connection pooling.
    /// Default: true
    /// </summary>
    public bool UseConnectionPooling { get; set; } = true;

    /// <summary>
    /// Gets or sets the minimum pool size.
    /// Default: 5
    /// </summary>
    public int MinPoolSize { get; set; } = 5;

    /// <summary>
    /// Gets or sets the maximum pool size.
    /// Default: 100
    /// </summary>
    public int MaxPoolSize { get; set; } = 100;
}

/// <summary>
/// Options for MongoDB persistence provider.
/// </summary>
public class MongoDbPersistenceOptions
{
    /// <summary>
    /// Gets or sets the connection string.
    /// </summary>
    public string? ConnectionString { get; set; }

    /// <summary>
    /// Gets or sets the database name.
    /// Default: "state_machine"
    /// </summary>
    public string DatabaseName { get; set; } = "state_machine";

    /// <summary>
    /// Gets or sets the events collection name.
    /// Default: "events"
    /// </summary>
    public string EventsCollectionName { get; set; } = "events";

    /// <summary>
    /// Gets or sets the snapshots collection name.
    /// Default: "snapshots"
    /// </summary>
    public string SnapshotsCollectionName { get; set; } = "snapshots";

    /// <summary>
    /// Gets or sets whether to create indexes automatically.
    /// Default: true
    /// </summary>
    public bool AutoCreateIndexes { get; set; } = true;

    /// <summary>
    /// Gets or sets the write concern.
    /// Default: "majority"
    /// </summary>
    public string WriteConcern { get; set; } = "majority";

    /// <summary>
    /// Gets or sets the read concern.
    /// Default: "majority"
    /// </summary>
    public string ReadConcern { get; set; } = "majority";
}
