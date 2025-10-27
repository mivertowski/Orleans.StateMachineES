using System.Runtime.CompilerServices;
using Stateless;

namespace Orleans.StateMachineES.Memory;

/// <summary>
/// High-performance cache for trigger parameters to avoid repeated configuration of Stateless state machine.
/// Caches TriggerWithParameters objects to improve performance of parameterized trigger operations.
/// </summary>
/// <typeparam name="TState">The type representing the states.</typeparam>
/// <typeparam name="TTrigger">The type representing the triggers.</typeparam>
/// <remarks>
/// Initializes a new instance of the TriggerParameterCache class.
/// </remarks>
/// <param name="stateMachine">The state machine to cache trigger parameters for.</param>
public sealed class TriggerParameterCache<TState, TTrigger>(StateMachine<TState, TTrigger> stateMachine)
    where TTrigger : notnull
{
    private readonly StateMachine<TState, TTrigger> _stateMachine = stateMachine ?? throw new ArgumentNullException(nameof(stateMachine));
    private readonly Dictionary<TTrigger, object> _cache = [];
    private readonly object _lock = new();

    /// <summary>
    /// Gets or creates cached trigger parameters for a trigger with one argument.
    /// </summary>
    /// <typeparam name="TArg0">Type of the first trigger argument.</typeparam>
    /// <param name="trigger">The trigger to get parameters for.</param>
    /// <returns>A cached or newly created TriggerWithParameters instance.</returns>
    public StateMachine<TState, TTrigger>.TriggerWithParameters<TArg0> GetOrCreate<TArg0>(TTrigger trigger)
    {
        // Fast path: check without lock first
        if (_cache.TryGetValue(trigger, out var cached))
        {
            return (StateMachine<TState, TTrigger>.TriggerWithParameters<TArg0>)cached;
        }

        // Slow path: acquire lock and double-check
        lock (_lock)
        {
            if (_cache.TryGetValue(trigger, out cached))
            {
                return (StateMachine<TState, TTrigger>.TriggerWithParameters<TArg0>)cached;
            }

            cached = _stateMachine.SetTriggerParameters<TArg0>(trigger);
            _cache[trigger] = cached;
            return (StateMachine<TState, TTrigger>.TriggerWithParameters<TArg0>)cached;
        }
    }

    /// <summary>
    /// Gets or creates cached trigger parameters for a trigger with two arguments.
    /// </summary>
    /// <typeparam name="TArg0">Type of the first trigger argument.</typeparam>
    /// <typeparam name="TArg1">Type of the second trigger argument.</typeparam>
    /// <param name="trigger">The trigger to get parameters for.</param>
    /// <returns>A cached or newly created TriggerWithParameters instance.</returns>
    public StateMachine<TState, TTrigger>.TriggerWithParameters<TArg0, TArg1> GetOrCreate<TArg0, TArg1>(TTrigger trigger)
    {
        // Fast path: check without lock first
        if (_cache.TryGetValue(trigger, out var cached))
        {
            return (StateMachine<TState, TTrigger>.TriggerWithParameters<TArg0, TArg1>)cached;
        }

        // Slow path: acquire lock and double-check
        lock (_lock)
        {
            if (_cache.TryGetValue(trigger, out cached))
            {
                return (StateMachine<TState, TTrigger>.TriggerWithParameters<TArg0, TArg1>)cached;
            }

            cached = _stateMachine.SetTriggerParameters<TArg0, TArg1>(trigger);
            _cache[trigger] = cached;
            return (StateMachine<TState, TTrigger>.TriggerWithParameters<TArg0, TArg1>)cached;
        }
    }

    /// <summary>
    /// Gets or creates cached trigger parameters for a trigger with three arguments.
    /// </summary>
    /// <typeparam name="TArg0">Type of the first trigger argument.</typeparam>
    /// <typeparam name="TArg1">Type of the second trigger argument.</typeparam>
    /// <typeparam name="TArg2">Type of the third trigger argument.</typeparam>
    /// <param name="trigger">The trigger to get parameters for.</param>
    /// <returns>A cached or newly created TriggerWithParameters instance.</returns>
    public StateMachine<TState, TTrigger>.TriggerWithParameters<TArg0, TArg1, TArg2> GetOrCreate<TArg0, TArg1, TArg2>(TTrigger trigger)
    {
        // Fast path: check without lock first
        if (_cache.TryGetValue(trigger, out var cached))
        {
            return (StateMachine<TState, TTrigger>.TriggerWithParameters<TArg0, TArg1, TArg2>)cached;
        }

        // Slow path: acquire lock and double-check
        lock (_lock)
        {
            if (_cache.TryGetValue(trigger, out cached))
            {
                return (StateMachine<TState, TTrigger>.TriggerWithParameters<TArg0, TArg1, TArg2>)cached;
            }

            cached = _stateMachine.SetTriggerParameters<TArg0, TArg1, TArg2>(trigger);
            _cache[trigger] = cached;
            return (StateMachine<TState, TTrigger>.TriggerWithParameters<TArg0, TArg1, TArg2>)cached;
        }
    }

    /// <summary>
    /// Clears all cached trigger parameters.
    /// Call this when rebuilding the state machine to ensure cache coherence.
    /// </summary>
    public void Clear()
    {
        _cache.Clear();
    }

    /// <summary>
    /// Gets the number of cached trigger parameter configurations.
    /// </summary>
    public int Count => _cache.Count;

    /// <summary>
    /// Checks if a trigger has cached parameters.
    /// </summary>
    /// <param name="trigger">The trigger to check.</param>
    /// <returns>True if the trigger has cached parameters, false otherwise.</returns>
    public bool Contains(TTrigger trigger)
    {
        return _cache.ContainsKey(trigger);
    }

    /// <summary>
    /// Manually adds a pre-configured trigger parameter to the cache.
    /// Use this when trigger parameters were configured before the cache was created.
    /// </summary>
    /// <typeparam name="TArg0">Type of the first trigger argument.</typeparam>
    /// <param name="trigger">The trigger.</param>
    /// <param name="triggerWithParameters">The pre-configured trigger with parameters.</param>
    public void Add<TArg0>(TTrigger trigger, StateMachine<TState, TTrigger>.TriggerWithParameters<TArg0> triggerWithParameters)
    {
        _cache[trigger] = triggerWithParameters;
    }
}
