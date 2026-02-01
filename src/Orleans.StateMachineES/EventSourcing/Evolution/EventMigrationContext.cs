namespace Orleans.StateMachineES.EventSourcing.Evolution;

/// <summary>
/// Context information provided during event migration/upcasting.
/// </summary>
public class EventMigrationContext
{
    /// <summary>
    /// The source version of the event.
    /// </summary>
    public int SourceVersion { get; set; }

    /// <summary>
    /// The target version being migrated to.
    /// </summary>
    public int TargetVersion { get; set; }

    /// <summary>
    /// The grain ID associated with this event (if applicable).
    /// </summary>
    public string? GrainId { get; set; }

    /// <summary>
    /// The stream/aggregate ID for the event stream.
    /// </summary>
    public string? StreamId { get; set; }

    /// <summary>
    /// The sequence number of this event in the stream.
    /// </summary>
    public long? SequenceNumber { get; set; }

    /// <summary>
    /// The original timestamp of the event.
    /// </summary>
    public DateTime? OriginalTimestamp { get; set; }

    /// <summary>
    /// When the migration is occurring.
    /// </summary>
    public DateTime MigrationTimestamp { get; set; } = DateTime.UtcNow;

    /// <summary>
    /// Additional metadata that can be used during migration.
    /// </summary>
    public Dictionary<string, object> Metadata { get; set; } = new();

    /// <summary>
    /// Gets or sets a value from the metadata dictionary.
    /// </summary>
    public T? GetMetadata<T>(string key)
    {
        if (Metadata.TryGetValue(key, out var value) && value is T typedValue)
        {
            return typedValue;
        }
        return default;
    }

    /// <summary>
    /// Sets a value in the metadata dictionary.
    /// </summary>
    public void SetMetadata<T>(string key, T value)
    {
        if (value != null)
        {
            Metadata[key] = value;
        }
    }

    /// <summary>
    /// Creates a context for a specific event.
    /// </summary>
    public static EventMigrationContext Create(
        int sourceVersion,
        int targetVersion,
        string? grainId = null,
        string? streamId = null,
        long? sequenceNumber = null,
        DateTime? originalTimestamp = null)
    {
        return new EventMigrationContext
        {
            SourceVersion = sourceVersion,
            TargetVersion = targetVersion,
            GrainId = grainId,
            StreamId = streamId,
            SequenceNumber = sequenceNumber,
            OriginalTimestamp = originalTimestamp
        };
    }
}
