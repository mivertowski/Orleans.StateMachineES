namespace Orleans.StateMachineES.EventSourcing.Evolution;

/// <summary>
/// Interface for event upcasting from one version to another.
/// </summary>
/// <typeparam name="TFrom">The source event type (older version).</typeparam>
/// <typeparam name="TTo">The target event type (newer version).</typeparam>
public interface IEventUpcast<in TFrom, out TTo>
    where TFrom : class
    where TTo : class
{
    /// <summary>
    /// Upcasts an event from the old version to the new version.
    /// </summary>
    /// <param name="oldEvent">The event in the old format.</param>
    /// <param name="context">The migration context with additional information.</param>
    /// <returns>The event in the new format.</returns>
    TTo Upcast(TFrom oldEvent, EventMigrationContext context);
}

/// <summary>
/// Non-generic interface for runtime event upcasting.
/// </summary>
public interface IEventUpcast
{
    /// <summary>
    /// The source event type.
    /// </summary>
    Type FromType { get; }

    /// <summary>
    /// The target event type.
    /// </summary>
    Type ToType { get; }

    /// <summary>
    /// Upcasts an event from the old version to the new version.
    /// </summary>
    /// <param name="oldEvent">The event in the old format.</param>
    /// <param name="context">The migration context.</param>
    /// <returns>The event in the new format.</returns>
    object Upcast(object oldEvent, EventMigrationContext context);
}

/// <summary>
/// Base class for event upcasters that implements both generic and non-generic interfaces.
/// </summary>
/// <typeparam name="TFrom">The source event type.</typeparam>
/// <typeparam name="TTo">The target event type.</typeparam>
public abstract class EventUpcastBase<TFrom, TTo> : IEventUpcast<TFrom, TTo>, IEventUpcast
    where TFrom : class
    where TTo : class
{
    /// <inheritdoc/>
    public Type FromType => typeof(TFrom);

    /// <inheritdoc/>
    public Type ToType => typeof(TTo);

    /// <inheritdoc/>
    public abstract TTo Upcast(TFrom oldEvent, EventMigrationContext context);

    /// <inheritdoc/>
    object IEventUpcast.Upcast(object oldEvent, EventMigrationContext context)
    {
        if (oldEvent is not TFrom typedEvent)
        {
            throw new ArgumentException(
                $"Expected event of type {typeof(TFrom).Name} but got {oldEvent.GetType().Name}",
                nameof(oldEvent));
        }

        return Upcast(typedEvent, context);
    }
}
