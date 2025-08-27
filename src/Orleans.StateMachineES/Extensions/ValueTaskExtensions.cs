using System;
using System.Runtime.CompilerServices;
using System.Threading.Tasks;

namespace Orleans.StateMachineES.Extensions;

/// <summary>
/// Extension methods for ValueTask operations to improve performance in hot paths.
/// Provides utilities for converting between Task and ValueTask with minimal allocations.
/// </summary>
public static class ValueTaskExtensions
{
    /// <summary>
    /// Creates a ValueTask from a value with zero allocations.
    /// </summary>
    /// <typeparam name="T">The type of the value.</typeparam>
    /// <param name="value">The value to wrap in a ValueTask.</param>
    /// <returns>A ValueTask containing the value.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static ValueTask<T> FromResult<T>(T value)
    {
        return new ValueTask<T>(value);
    }

    /// <summary>
    /// Creates a completed ValueTask with zero allocations.
    /// </summary>
    /// <returns>A completed ValueTask.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static ValueTask CompletedTask()
    {
        return default;
    }

    /// <summary>
    /// Converts a Task to a ValueTask efficiently.
    /// </summary>
    /// <typeparam name="T">The type of the result.</typeparam>
    /// <param name="task">The task to convert.</param>
    /// <returns>A ValueTask wrapping the task.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static ValueTask<T> ToValueTask<T>(this Task<T> task)
    {
        return new ValueTask<T>(task);
    }

    /// <summary>
    /// Converts a Task to a ValueTask efficiently.
    /// </summary>
    /// <param name="task">The task to convert.</param>
    /// <returns>A ValueTask wrapping the task.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static ValueTask ToValueTask(this Task task)
    {
        return new ValueTask(task);
    }

    /// <summary>
    /// Creates a ValueTask from a value or task based on whether the operation is synchronous.
    /// </summary>
    /// <typeparam name="T">The type of the result.</typeparam>
    /// <param name="isSync">Whether the operation completed synchronously.</param>
    /// <param name="value">The value if synchronous.</param>
    /// <param name="task">The task if asynchronous.</param>
    /// <returns>A ValueTask representing the operation.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static ValueTask<T> FromSyncOrAsync<T>(bool isSync, T value, Task<T>? task)
    {
        return isSync ? new ValueTask<T>(value) : new ValueTask<T>(task!);
    }

    /// <summary>
    /// Preserves the synchronization context when awaiting a ValueTask.
    /// </summary>
    /// <typeparam name="T">The type of the result.</typeparam>
    /// <param name="valueTask">The ValueTask to configure.</param>
    /// <param name="continueOnCapturedContext">Whether to continue on the captured context.</param>
    /// <returns>A configured ValueTask awaitable.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static ConfiguredValueTaskAwaitable<T> ConfigureAwaitEx<T>(
        this ValueTask<T> valueTask, 
        bool continueOnCapturedContext = false)
    {
        return valueTask.ConfigureAwait(continueOnCapturedContext);
    }

    /// <summary>
    /// Preserves the synchronization context when awaiting a ValueTask.
    /// </summary>
    /// <param name="valueTask">The ValueTask to configure.</param>
    /// <param name="continueOnCapturedContext">Whether to continue on the captured context.</param>
    /// <returns>A configured ValueTask awaitable.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static ConfiguredValueTaskAwaitable ConfigureAwaitEx(
        this ValueTask valueTask, 
        bool continueOnCapturedContext = false)
    {
        return valueTask.ConfigureAwait(continueOnCapturedContext);
    }

    /// <summary>
    /// Creates a faulted ValueTask with the specified exception.
    /// </summary>
    /// <typeparam name="T">The type of the result.</typeparam>
    /// <param name="exception">The exception to wrap.</param>
    /// <returns>A faulted ValueTask.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static ValueTask<T> FromException<T>(Exception exception)
    {
        return new ValueTask<T>(Task.FromException<T>(exception));
    }

    /// <summary>
    /// Creates a faulted ValueTask with the specified exception.
    /// </summary>
    /// <param name="exception">The exception to wrap.</param>
    /// <returns>A faulted ValueTask.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static ValueTask FromException(Exception exception)
    {
        return new ValueTask(Task.FromException(exception));
    }

    /// <summary>
    /// Executes a function and wraps the result in a ValueTask, handling exceptions.
    /// </summary>
    /// <typeparam name="T">The type of the result.</typeparam>
    /// <param name="func">The function to execute.</param>
    /// <returns>A ValueTask containing the result or exception.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static ValueTask<T> TryExecute<T>(Func<T> func)
    {
        try
        {
            return new ValueTask<T>(func());
        }
        catch (Exception ex)
        {
            return FromException<T>(ex);
        }
    }

    /// <summary>
    /// Executes an async function and returns a ValueTask, optimizing for synchronous completion.
    /// </summary>
    /// <typeparam name="T">The type of the result.</typeparam>
    /// <param name="func">The async function to execute.</param>
    /// <returns>A ValueTask containing the result.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static ValueTask<T> ExecuteAsync<T>(Func<Task<T>> func)
    {
        var task = func();
        return task.IsCompletedSuccessfully 
            ? new ValueTask<T>(task.Result) 
            : new ValueTask<T>(task);
    }
}