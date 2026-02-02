namespace Orleans.StateMachineES.EventSourcing.Evolution;

/// <summary>
/// Configuration options for event schema evolution.
/// </summary>
public class EventEvolutionOptions
{
    /// <summary>
    /// Whether to automatically discover and register upcasters from assemblies.
    /// Default: true
    /// </summary>
    public bool AutoRegisterUpcasters { get; set; } = true;

    /// <summary>
    /// Assemblies to scan for auto-registration of upcasters.
    /// If empty and AutoRegisterUpcasters is true, scans the calling assembly.
    /// </summary>
    public List<System.Reflection.Assembly> AssembliesToScan { get; set; } = new();

    /// <summary>
    /// Whether to throw an exception when no upcast path is found.
    /// If false, returns the original event unchanged.
    /// Default: false
    /// </summary>
    public bool ThrowOnMissingUpcast { get; set; } = false;

    /// <summary>
    /// Whether to enable tracking of event versions in stored events.
    /// Default: true
    /// </summary>
    public bool EnableEventVersionTracking { get; set; } = true;

    /// <summary>
    /// Whether to allow implicit upcasting for compatible types.
    /// Implicit upcasting uses property mapping for types with compatible shapes.
    /// Default: false
    /// </summary>
    public bool AllowImplicitUpcast { get; set; } = false;

    /// <summary>
    /// Maximum number of upcast steps allowed in a chain.
    /// Prevents infinite loops in misconfigured upcast chains.
    /// Default: 10
    /// </summary>
    public int MaxUpcastChainLength { get; set; } = 10;

    /// <summary>
    /// Whether to cache upcasted events for performance.
    /// Default: true
    /// </summary>
    public bool CacheUpcastedEvents { get; set; } = true;

    /// <summary>
    /// Maximum number of events to cache.
    /// Default: 10000
    /// </summary>
    public int MaxCacheSize { get; set; } = 10000;

    /// <summary>
    /// Callback invoked when an event is upcasted.
    /// </summary>
    public Action<EventUpcastedEventArgs>? OnEventUpcasted { get; set; }

    /// <summary>
    /// Callback invoked when an upcast fails.
    /// </summary>
    public Action<EventUpcastFailedEventArgs>? OnUpcastFailed { get; set; }
}

/// <summary>
/// Event args for successful event upcasting.
/// </summary>
public class EventUpcastedEventArgs
{
    /// <summary>
    /// The original event type.
    /// </summary>
    public Type OriginalType { get; set; } = null!;

    /// <summary>
    /// The target event type.
    /// </summary>
    public Type TargetType { get; set; } = null!;

    /// <summary>
    /// The original version.
    /// </summary>
    public int OriginalVersion { get; set; }

    /// <summary>
    /// The target version.
    /// </summary>
    public int TargetVersion { get; set; }

    /// <summary>
    /// Number of upcast steps taken.
    /// </summary>
    public int UpcastSteps { get; set; }
}

/// <summary>
/// Event args for failed event upcasting.
/// </summary>
public class EventUpcastFailedEventArgs
{
    /// <summary>
    /// The original event type.
    /// </summary>
    public Type OriginalType { get; set; } = null!;

    /// <summary>
    /// The target event type (if known).
    /// </summary>
    public Type? TargetType { get; set; }

    /// <summary>
    /// The exception that occurred.
    /// </summary>
    public Exception? Exception { get; set; }

    /// <summary>
    /// Error message.
    /// </summary>
    public string? ErrorMessage { get; set; }
}
