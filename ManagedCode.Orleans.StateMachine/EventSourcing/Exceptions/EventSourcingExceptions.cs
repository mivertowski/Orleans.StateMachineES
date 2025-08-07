using System;
using System.Runtime.Serialization;
using Orleans;

namespace ivlt.Orleans.StateMachineES.EventSourcing.Exceptions;

/// <summary>
/// Base exception for event sourcing related errors.
/// </summary>
[Serializable]
[GenerateSerializer]
public class EventSourcingException : Exception
{
    /// <summary>
    /// Initializes a new instance of the EventSourcingException class.
    /// </summary>
    public EventSourcingException()
    {
    }

    /// <summary>
    /// Initializes a new instance of the EventSourcingException class with a specified error message.
    /// </summary>
    public EventSourcingException(string message) : base(message)
    {
    }

    /// <summary>
    /// Initializes a new instance of the EventSourcingException class with a specified error message
    /// and a reference to the inner exception that is the cause of this exception.
    /// </summary>
    public EventSourcingException(string message, Exception innerException) : base(message, innerException)
    {
    }

    /// <summary>
    /// Initializes a new instance of the EventSourcingException class with serialized data.
    /// </summary>
    [Obsolete("This API supports obsolete formatter-based serialization. It should not be called or extended by application code.")]
    protected EventSourcingException(SerializationInfo info, StreamingContext context) : base(info, context)
    {
    }
}

/// <summary>
/// Exception thrown when an invalid state transition is attempted.
/// </summary>
[Serializable]
[GenerateSerializer]
public class InvalidStateTransitionException : EventSourcingException
{
    /// <summary>
    /// Gets the source state of the invalid transition.
    /// </summary>
    [Id(0)]
    public object? FromState { get; }

    /// <summary>
    /// Gets the target state of the invalid transition.
    /// </summary>
    [Id(1)]
    public object? ToState { get; }

    /// <summary>
    /// Gets the trigger that was attempted.
    /// </summary>
    [Id(2)]
    public object? Trigger { get; }

    /// <summary>
    /// Initializes a new instance of the InvalidStateTransitionException class.
    /// </summary>
    public InvalidStateTransitionException()
    {
    }

    /// <summary>
    /// Initializes a new instance of the InvalidStateTransitionException class with a specified error message.
    /// </summary>
    public InvalidStateTransitionException(string message) : base(message)
    {
    }

    /// <summary>
    /// Initializes a new instance of the InvalidStateTransitionException class with state details.
    /// </summary>
    public InvalidStateTransitionException(string message, object fromState, object trigger) : base(message)
    {
        FromState = fromState;
        Trigger = trigger;
    }

    /// <summary>
    /// Initializes a new instance of the InvalidStateTransitionException class with full transition details.
    /// </summary>
    public InvalidStateTransitionException(string message, object fromState, object toState, object trigger) : base(message)
    {
        FromState = fromState;
        ToState = toState;
        Trigger = trigger;
    }

    /// <summary>
    /// Initializes a new instance of the InvalidStateTransitionException class with a specified error message
    /// and a reference to the inner exception that is the cause of this exception.
    /// </summary>
    public InvalidStateTransitionException(string message, Exception innerException) : base(message, innerException)
    {
    }

    /// <summary>
    /// Initializes a new instance of the InvalidStateTransitionException class with serialized data.
    /// </summary>
    [Obsolete("This API supports obsolete formatter-based serialization. It should not be called or extended by application code.")]
    protected InvalidStateTransitionException(SerializationInfo info, StreamingContext context) : base(info, context)
    {
        FromState = info.GetValue(nameof(FromState), typeof(object));
        ToState = info.GetValue(nameof(ToState), typeof(object));
        Trigger = info.GetValue(nameof(Trigger), typeof(object));
    }

    /// <summary>
    /// Sets the SerializationInfo with information about the exception.
    /// </summary>
    [Obsolete("This API supports obsolete formatter-based serialization. It should not be called or extended by application code.")]
    public override void GetObjectData(SerializationInfo info, StreamingContext context)
    {
        base.GetObjectData(info, context);
        info.AddValue(nameof(FromState), FromState);
        info.AddValue(nameof(ToState), ToState);
        info.AddValue(nameof(Trigger), Trigger);
    }
}

/// <summary>
/// Exception thrown when event replay fails.
/// </summary>
[Serializable]
[GenerateSerializer]
public class EventReplayException : EventSourcingException
{
    /// <summary>
    /// Gets the event number where replay failed.
    /// </summary>
    [Id(0)]
    public int? FailedAtEventNumber { get; }

    /// <summary>
    /// Gets the event that caused the failure.
    /// </summary>
    [Id(1)]
    public object? FailedEvent { get; }

    /// <summary>
    /// Initializes a new instance of the EventReplayException class.
    /// </summary>
    public EventReplayException()
    {
    }

    /// <summary>
    /// Initializes a new instance of the EventReplayException class with a specified error message.
    /// </summary>
    public EventReplayException(string message) : base(message)
    {
    }

    /// <summary>
    /// Initializes a new instance of the EventReplayException class with event details.
    /// </summary>
    public EventReplayException(string message, int eventNumber, object failedEvent) : base(message)
    {
        FailedAtEventNumber = eventNumber;
        FailedEvent = failedEvent;
    }

    /// <summary>
    /// Initializes a new instance of the EventReplayException class with a specified error message
    /// and a reference to the inner exception that is the cause of this exception.
    /// </summary>
    public EventReplayException(string message, Exception innerException) : base(message, innerException)
    {
    }

    /// <summary>
    /// Initializes a new instance of the EventReplayException class with serialized data.
    /// </summary>
    [Obsolete("This API supports obsolete formatter-based serialization. It should not be called or extended by application code.")]
    protected EventReplayException(SerializationInfo info, StreamingContext context) : base(info, context)
    {
        FailedAtEventNumber = (int?)info.GetValue(nameof(FailedAtEventNumber), typeof(int?));
        FailedEvent = info.GetValue(nameof(FailedEvent), typeof(object));
    }

    /// <summary>
    /// Sets the SerializationInfo with information about the exception.
    /// </summary>
    [Obsolete("This API supports obsolete formatter-based serialization. It should not be called or extended by application code.")]
    public override void GetObjectData(SerializationInfo info, StreamingContext context)
    {
        base.GetObjectData(info, context);
        info.AddValue(nameof(FailedAtEventNumber), FailedAtEventNumber);
        info.AddValue(nameof(FailedEvent), FailedEvent);
    }
}

/// <summary>
/// Exception thrown when snapshot operations fail.
/// </summary>
[Serializable]
[GenerateSerializer]
public class SnapshotException : EventSourcingException
{
    /// <summary>
    /// Gets the version at which the snapshot failed.
    /// </summary>
    [Id(0)]
    public int? Version { get; }

    /// <summary>
    /// Initializes a new instance of the SnapshotException class.
    /// </summary>
    public SnapshotException()
    {
    }

    /// <summary>
    /// Initializes a new instance of the SnapshotException class with a specified error message.
    /// </summary>
    public SnapshotException(string message) : base(message)
    {
    }

    /// <summary>
    /// Initializes a new instance of the SnapshotException class with version details.
    /// </summary>
    public SnapshotException(string message, int version) : base(message)
    {
        Version = version;
    }

    /// <summary>
    /// Initializes a new instance of the SnapshotException class with a specified error message
    /// and a reference to the inner exception that is the cause of this exception.
    /// </summary>
    public SnapshotException(string message, Exception innerException) : base(message, innerException)
    {
    }

    /// <summary>
    /// Initializes a new instance of the SnapshotException class with serialized data.
    /// </summary>
    [Obsolete("This API supports obsolete formatter-based serialization. It should not be called or extended by application code.")]
    protected SnapshotException(SerializationInfo info, StreamingContext context) : base(info, context)
    {
        Version = (int?)info.GetValue(nameof(Version), typeof(int?));
    }

    /// <summary>
    /// Sets the SerializationInfo with information about the exception.
    /// </summary>
    [Obsolete("This API supports obsolete formatter-based serialization. It should not be called or extended by application code.")]
    public override void GetObjectData(SerializationInfo info, StreamingContext context)
    {
        base.GetObjectData(info, context);
        info.AddValue(nameof(Version), Version);
    }
}