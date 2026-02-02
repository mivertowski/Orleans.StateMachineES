using System.Collections.Concurrent;
using System.Reflection;
using Microsoft.Extensions.Logging;

namespace Orleans.StateMachineES.EventSourcing.Evolution;

/// <summary>
/// Registry for event upcasters that manages event schema evolution.
/// </summary>
public class EventUpcastRegistry
{
    private readonly ILogger<EventUpcastRegistry>? _logger;
    private readonly EventEvolutionOptions _options;

    // Maps (FromType, ToType) -> IEventUpcast
    private readonly ConcurrentDictionary<(Type From, Type To), IEventUpcast> _upcasters = new();

    // Maps EventType -> Version
    private readonly ConcurrentDictionary<Type, int> _typeVersions = new();

    // Maps (EventTypeName, Version) -> EventType
    private readonly ConcurrentDictionary<(string TypeName, int Version), Type> _versionedTypes = new();

    // Maps EventType -> Latest version type
    private readonly ConcurrentDictionary<Type, Type> _latestVersionCache = new();

    // Cache for upcasted events
    private readonly ConcurrentDictionary<(Type, object), object> _upcastCache = new();
    private int _cacheCount;

    /// <summary>
    /// Initializes a new instance of the EventUpcastRegistry.
    /// </summary>
    /// <param name="options">Evolution options.</param>
    /// <param name="logger">Optional logger.</param>
    public EventUpcastRegistry(EventEvolutionOptions? options = null, ILogger<EventUpcastRegistry>? logger = null)
    {
        _options = options ?? new EventEvolutionOptions();
        _logger = logger;

        if (_options.AutoRegisterUpcasters)
        {
            AutoRegisterFromAssemblies();
        }
    }

    /// <summary>
    /// Registers an upcaster for converting events from one type to another.
    /// </summary>
    /// <typeparam name="TFrom">The source event type.</typeparam>
    /// <typeparam name="TTo">The target event type.</typeparam>
    /// <param name="upcaster">The upcaster instance.</param>
    public void Register<TFrom, TTo>(IEventUpcast<TFrom, TTo> upcaster)
        where TFrom : class
        where TTo : class
    {
        ArgumentNullException.ThrowIfNull(upcaster);

        var key = (typeof(TFrom), typeof(TTo));

        // Wrap in non-generic interface
        var wrapper = new EventUpcastWrapper<TFrom, TTo>(upcaster);

        if (_upcasters.TryAdd(key, wrapper))
        {
            _logger?.LogDebug("Registered upcaster: {From} -> {To}", typeof(TFrom).Name, typeof(TTo).Name);
            RegisterTypeVersions(typeof(TFrom), typeof(TTo));
        }
        else
        {
            _logger?.LogWarning("Upcaster already registered for {From} -> {To}", typeof(TFrom).Name, typeof(TTo).Name);
        }
    }

    /// <summary>
    /// Registers a lambda-based upcaster.
    /// </summary>
    public void Register<TFrom, TTo>(Func<TFrom, EventMigrationContext, TTo> upcastFunc)
        where TFrom : class
        where TTo : class
    {
        Register(new LambdaEventUpcast<TFrom, TTo>(upcastFunc));
    }

    /// <summary>
    /// Registers an auto-upcast that copies properties from old to new type.
    /// </summary>
    public void RegisterAutoUpcast<TFrom, TTo>()
        where TFrom : class
        where TTo : class, new()
    {
        Register<TFrom, TTo>((oldEvent, context) =>
        {
            var newEvent = new TTo();
            CopyMatchingProperties(oldEvent, newEvent);
            return newEvent;
        });
    }

    /// <summary>
    /// Tries to upcast an event to the specified target type.
    /// </summary>
    /// <param name="oldEvent">The event to upcast.</param>
    /// <param name="targetType">The target type.</param>
    /// <returns>The upcasted event, or null if no upcast path exists.</returns>
    public object? TryUpcast(object oldEvent, Type targetType)
    {
        ArgumentNullException.ThrowIfNull(oldEvent);
        ArgumentNullException.ThrowIfNull(targetType);

        var sourceType = oldEvent.GetType();

        // Already the target type
        if (sourceType == targetType || targetType.IsAssignableFrom(sourceType))
        {
            return oldEvent;
        }

        // Check cache
        var cacheKey = (targetType, oldEvent);
        if (_options.CacheUpcastedEvents && _upcastCache.TryGetValue(cacheKey, out var cached))
        {
            return cached;
        }

        // Find upcast path
        var path = FindUpcastPath(sourceType, targetType);

        if (path == null || path.Count == 0)
        {
            if (_options.ThrowOnMissingUpcast)
            {
                throw new InvalidOperationException(
                    $"No upcast path found from {sourceType.Name} to {targetType.Name}");
            }

            _logger?.LogWarning("No upcast path found from {From} to {To}", sourceType.Name, targetType.Name);

            try
            {
                _options.OnUpcastFailed?.Invoke(new EventUpcastFailedEventArgs
                {
                    OriginalType = sourceType,
                    TargetType = targetType,
                    ErrorMessage = "No upcast path found"
                });
            }
            catch { /* Ignore callback errors */ }

            return null;
        }

        // Execute upcast chain
        return ExecuteUpcastChain(oldEvent, path, sourceType, targetType);
    }

    /// <summary>
    /// Upcasts an event to the latest registered version.
    /// </summary>
    public object UpcastToLatest(object oldEvent)
    {
        ArgumentNullException.ThrowIfNull(oldEvent);

        var sourceType = oldEvent.GetType();

        // Find the latest version type
        if (!_latestVersionCache.TryGetValue(sourceType, out var latestType))
        {
            latestType = FindLatestVersionType(sourceType);
            _latestVersionCache.TryAdd(sourceType, latestType);
        }

        if (latestType == sourceType)
        {
            return oldEvent; // Already at latest version
        }

        return TryUpcast(oldEvent, latestType) ?? oldEvent;
    }

    /// <summary>
    /// Gets the version of an event type.
    /// </summary>
    public int GetEventVersion(Type eventType)
    {
        if (_typeVersions.TryGetValue(eventType, out var version))
        {
            return version;
        }

        // Check for version attribute
        var attr = eventType.GetCustomAttribute<EventVersionAttribute>();
        if (attr != null)
        {
            _typeVersions.TryAdd(eventType, attr.Version);
            return attr.Version;
        }

        return 1; // Default version
    }

    /// <summary>
    /// Checks if an upcast path exists between two types.
    /// </summary>
    public bool CanUpcast(Type fromType, Type toType)
    {
        if (fromType == toType || toType.IsAssignableFrom(fromType))
        {
            return true;
        }

        var path = FindUpcastPath(fromType, toType);
        return path != null && path.Count > 0;
    }

    /// <summary>
    /// Gets all registered upcasters.
    /// </summary>
    public IReadOnlyDictionary<(Type From, Type To), IEventUpcast> GetRegisteredUpcasters()
        => _upcasters;

    /// <summary>
    /// Clears the upcast cache.
    /// </summary>
    public void ClearCache()
    {
        _upcastCache.Clear();
        _cacheCount = 0;
        _logger?.LogDebug("Event upcast cache cleared");
    }

    private void AutoRegisterFromAssemblies()
    {
        var assemblies = _options.AssembliesToScan.Count > 0
            ? _options.AssembliesToScan
            : new List<Assembly> { Assembly.GetCallingAssembly() };

        foreach (var assembly in assemblies)
        {
            try
            {
                var upcastTypes = assembly.GetTypes()
                    .Where(t => !t.IsAbstract && !t.IsInterface)
                    .Where(t => t.GetInterfaces().Any(i =>
                        i.IsGenericType && i.GetGenericTypeDefinition() == typeof(IEventUpcast<,>)));

                foreach (var upcastType in upcastTypes)
                {
                    try
                    {
                        var instance = Activator.CreateInstance(upcastType);
                        if (instance is IEventUpcast upcast)
                        {
                            _upcasters.TryAdd((upcast.FromType, upcast.ToType), upcast);
                            RegisterTypeVersions(upcast.FromType, upcast.ToType);
                            _logger?.LogDebug("Auto-registered upcaster: {Type}", upcastType.Name);
                        }
                    }
                    catch (Exception ex)
                    {
                        _logger?.LogWarning(ex, "Failed to auto-register upcaster: {Type}", upcastType.Name);
                    }
                }
            }
            catch (Exception ex)
            {
                _logger?.LogWarning(ex, "Failed to scan assembly for upcasters: {Assembly}", assembly.FullName);
            }
        }
    }

    private void RegisterTypeVersions(Type fromType, Type toType)
    {
        var fromVersion = GetEventVersion(fromType);
        var toVersion = GetEventVersion(toType);

        _typeVersions.TryAdd(fromType, fromVersion);
        _typeVersions.TryAdd(toType, toVersion);

        var baseName = GetBaseTypeName(fromType);
        _versionedTypes.TryAdd((baseName, fromVersion), fromType);
        _versionedTypes.TryAdd((baseName, toVersion), toType);
    }

    private static string GetBaseTypeName(Type type)
    {
        var name = type.Name;

        // Remove version suffix patterns like V1, V2, Version1, etc.
        var patterns = new[] { "V", "Version", "_v", "_V" };
        foreach (var pattern in patterns)
        {
            var idx = name.LastIndexOf(pattern, StringComparison.OrdinalIgnoreCase);
            if (idx > 0 && int.TryParse(name[(idx + pattern.Length)..], out _))
            {
                return name[..idx];
            }
        }

        return name;
    }

    private List<IEventUpcast>? FindUpcastPath(Type fromType, Type toType)
    {
        // BFS to find shortest path
        var visited = new HashSet<Type> { fromType };
        var queue = new Queue<(Type Current, List<IEventUpcast> Path)>();
        queue.Enqueue((fromType, new List<IEventUpcast>()));

        while (queue.Count > 0 && queue.Count < 1000) // Prevent infinite loops
        {
            var (current, path) = queue.Dequeue();

            if (path.Count >= _options.MaxUpcastChainLength)
            {
                continue;
            }

            // Find all upcasters from current type
            foreach (var kvp in _upcasters.Where(kv => kv.Key.From == current))
            {
                var nextType = kvp.Key.To;

                if (nextType == toType)
                {
                    // Found the path
                    var finalPath = new List<IEventUpcast>(path) { kvp.Value };
                    return finalPath;
                }

                if (!visited.Contains(nextType))
                {
                    visited.Add(nextType);
                    var newPath = new List<IEventUpcast>(path) { kvp.Value };
                    queue.Enqueue((nextType, newPath));
                }
            }
        }

        return null;
    }

    private Type FindLatestVersionType(Type sourceType)
    {
        var baseName = GetBaseTypeName(sourceType);
        var currentVersion = GetEventVersion(sourceType);
        var latestType = sourceType;
        var latestVersion = currentVersion;

        // Find the highest version
        foreach (var kvp in _versionedTypes.Where(kv => kv.Key.TypeName == baseName))
        {
            if (kvp.Key.Version > latestVersion && CanUpcast(sourceType, kvp.Value))
            {
                latestVersion = kvp.Key.Version;
                latestType = kvp.Value;
            }
        }

        return latestType;
    }

    private object? ExecuteUpcastChain(
        object oldEvent,
        List<IEventUpcast> path,
        Type sourceType,
        Type targetType)
    {
        var current = oldEvent;
        var steps = 0;

        var context = EventMigrationContext.Create(
            GetEventVersion(sourceType),
            GetEventVersion(targetType));

        try
        {
            foreach (var upcast in path)
            {
                current = upcast.Upcast(current, context);
                steps++;

                if (current == null)
                {
                    _logger?.LogWarning("Upcast returned null at step {Step}", steps);
                    return null;
                }
            }

            // Cache the result
            if (_options.CacheUpcastedEvents && _cacheCount < _options.MaxCacheSize)
            {
                _upcastCache.TryAdd((targetType, oldEvent), current);
                Interlocked.Increment(ref _cacheCount);
            }

            // Invoke callback
            try
            {
                _options.OnEventUpcasted?.Invoke(new EventUpcastedEventArgs
                {
                    OriginalType = sourceType,
                    TargetType = targetType,
                    OriginalVersion = context.SourceVersion,
                    TargetVersion = context.TargetVersion,
                    UpcastSteps = steps
                });
            }
            catch { /* Ignore callback errors */ }

            _logger?.LogDebug(
                "Successfully upcasted event from {From} to {To} in {Steps} step(s)",
                sourceType.Name, targetType.Name, steps);

            return current;
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "Error during upcast chain execution");

            try
            {
                _options.OnUpcastFailed?.Invoke(new EventUpcastFailedEventArgs
                {
                    OriginalType = sourceType,
                    TargetType = targetType,
                    Exception = ex,
                    ErrorMessage = ex.Message
                });
            }
            catch { /* Ignore callback errors */ }

            if (_options.ThrowOnMissingUpcast)
            {
                throw;
            }

            return null;
        }
    }

    private static void CopyMatchingProperties(object source, object target)
    {
        var sourceProps = source.GetType().GetProperties(BindingFlags.Public | BindingFlags.Instance);
        var targetProps = target.GetType().GetProperties(BindingFlags.Public | BindingFlags.Instance)
            .Where(p => p.CanWrite)
            .ToDictionary(p => p.Name, p => p);

        foreach (var sourceProp in sourceProps.Where(p => p.CanRead))
        {
            if (targetProps.TryGetValue(sourceProp.Name, out var targetProp) &&
                targetProp.PropertyType.IsAssignableFrom(sourceProp.PropertyType))
            {
                var value = sourceProp.GetValue(source);
                targetProp.SetValue(target, value);
            }
        }
    }

    /// <summary>
    /// Wrapper class for generic upcasters.
    /// </summary>
    private class EventUpcastWrapper<TFrom, TTo> : IEventUpcast
        where TFrom : class
        where TTo : class
    {
        private readonly IEventUpcast<TFrom, TTo> _inner;

        public EventUpcastWrapper(IEventUpcast<TFrom, TTo> inner)
        {
            _inner = inner;
        }

        public Type FromType => typeof(TFrom);
        public Type ToType => typeof(TTo);

        public object Upcast(object oldEvent, EventMigrationContext context)
        {
            return _inner.Upcast((TFrom)oldEvent, context);
        }
    }

    /// <summary>
    /// Lambda-based upcaster implementation.
    /// </summary>
    private class LambdaEventUpcast<TFrom, TTo> : IEventUpcast<TFrom, TTo>
        where TFrom : class
        where TTo : class
    {
        private readonly Func<TFrom, EventMigrationContext, TTo> _upcastFunc;

        public LambdaEventUpcast(Func<TFrom, EventMigrationContext, TTo> upcastFunc)
        {
            _upcastFunc = upcastFunc ?? throw new ArgumentNullException(nameof(upcastFunc));
        }

        public TTo Upcast(TFrom oldEvent, EventMigrationContext context)
        {
            return _upcastFunc(oldEvent, context);
        }
    }
}
