namespace Orleans.StateMachineES.EventSourcing.Evolution;

/// <summary>
/// Specifies the version of an event class for schema evolution.
/// </summary>
[AttributeUsage(AttributeTargets.Class, AllowMultiple = false, Inherited = false)]
public class EventVersionAttribute : Attribute
{
    /// <summary>
    /// The version number of this event schema.
    /// </summary>
    public int Version { get; }

    /// <summary>
    /// The previous version's type that this version can be upcasted from.
    /// Null if this is the first version.
    /// </summary>
    public Type? PreviousVersionType { get; }

    /// <summary>
    /// Optional description of changes in this version.
    /// </summary>
    public string? ChangeDescription { get; set; }

    /// <summary>
    /// Initializes a new instance of the EventVersionAttribute.
    /// </summary>
    /// <param name="version">The version number of this event schema.</param>
    public EventVersionAttribute(int version)
    {
        if (version < 1)
        {
            throw new ArgumentOutOfRangeException(nameof(version), "Version must be at least 1");
        }

        Version = version;
        PreviousVersionType = null;
    }

    /// <summary>
    /// Initializes a new instance of the EventVersionAttribute with a previous version type.
    /// </summary>
    /// <param name="version">The version number of this event schema.</param>
    /// <param name="previousVersionType">The type of the previous version.</param>
    public EventVersionAttribute(int version, Type previousVersionType)
    {
        if (version < 1)
        {
            throw new ArgumentOutOfRangeException(nameof(version), "Version must be at least 1");
        }

        Version = version;
        PreviousVersionType = previousVersionType ?? throw new ArgumentNullException(nameof(previousVersionType));
    }
}
