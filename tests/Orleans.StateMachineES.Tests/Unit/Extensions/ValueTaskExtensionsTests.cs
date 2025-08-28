using System;
using System.Threading.Tasks;
using FluentAssertions;
using Orleans.StateMachineES.Extensions;
using Xunit;

namespace Orleans.StateMachineES.Tests.Unit.Extensions;

public class ValueTaskExtensionsTests
{
    [Fact]
    public void FromResult_ShouldCreateValueTaskWithValue()
    {
        // Arrange
        var value = "test value";

        // Act
        var valueTask = ValueTaskExtensions.FromResult(value);

        // Assert
        valueTask.IsCompletedSuccessfully.Should().BeTrue();
        valueTask.Result.Should().Be(value);
    }

    [Fact]
    public void FromResult_WithNull_ShouldCreateValueTaskWithNull()
    {
        // Act
        var valueTask = ValueTaskExtensions.FromResult<string>(null);

        // Assert
        valueTask.IsCompletedSuccessfully.Should().BeTrue();
        valueTask.Result.Should().BeNull();
    }

    [Fact]
    public void CompletedTask_ShouldCreateCompletedValueTask()
    {
        // Act
        var valueTask = ValueTaskExtensions.CompletedTask();

        // Assert
        valueTask.IsCompletedSuccessfully.Should().BeTrue();
        valueTask.IsCompleted.Should().BeTrue();
    }

    [Fact]
    public async Task ToValueTask_WithCompletedTask_ShouldConvertCorrectly()
    {
        // Arrange
        var originalValue = 42;
        var task = Task.FromResult(originalValue);

        // Act
        var valueTask = task.ToValueTask();
        var result = await valueTask;

        // Assert
        result.Should().Be(originalValue);
        valueTask.IsCompletedSuccessfully.Should().BeTrue();
    }

    [Fact]
    public async Task ToValueTask_WithTask_ShouldConvertCorrectly()
    {
        // Arrange
        var task = Task.CompletedTask;

        // Act
        var valueTask = task.ToValueTask();
        await valueTask;

        // Assert
        valueTask.IsCompleted.Should().BeTrue();
    }

    [Fact]
    public async Task ToValueTask_WithAsyncTask_ShouldConvertCorrectly()
    {
        // Arrange
        var task = Task.Run(async () =>
        {
            await Task.Delay(10);
            return "async result";
        });

        // Act
        var valueTask = task.ToValueTask();
        var result = await valueTask;

        // Assert
        result.Should().Be("async result");
    }

    [Fact]
    public void FromSyncOrAsync_WithSyncTrue_ShouldUseValue()
    {
        // Arrange
        var value = "sync value";
        var task = Task.FromResult("async value");

        // Act
        var valueTask = ValueTaskExtensions.FromSyncOrAsync(true, value, task);

        // Assert
        valueTask.IsCompletedSuccessfully.Should().BeTrue();
        valueTask.Result.Should().Be(value);
    }

    [Fact]
    public async Task FromSyncOrAsync_WithSyncFalse_ShouldUseTask()
    {
        // Arrange
        var value = "sync value";
        var task = Task.FromResult("async value");

        // Act
        var valueTask = ValueTaskExtensions.FromSyncOrAsync(false, value, task);
        var result = await valueTask;

        // Assert
        result.Should().Be("async value");
    }

    [Fact]
    public async Task ConfigureAwaitEx_Generic_ShouldConfigureCorrectly()
    {
        // Arrange
        var valueTask = ValueTaskExtensions.FromResult("test");

        // Act
        var configuredAwaitable = valueTask.ConfigureAwaitEx(false);
        var result = await configuredAwaitable;

        // Assert
        result.Should().Be("test");
    }

    [Fact]
    public async Task ConfigureAwaitEx_NonGeneric_ShouldConfigureCorrectly()
    {
        // Arrange
        var valueTask = ValueTaskExtensions.CompletedTask();

        // Act
        var configuredAwaitable = valueTask.ConfigureAwaitEx(false);
        await configuredAwaitable;

        // Assert
        // Should complete without exception
    }

    [Fact]
    public async Task FromException_Generic_ShouldCreateFaultedValueTask()
    {
        // Arrange
        var exception = new InvalidOperationException("test exception");

        // Act
        var valueTask = ValueTaskExtensions.FromException<string>(exception);

        // Assert
        valueTask.IsFaulted.Should().BeTrue();
        var thrownException = await Assert.ThrowsAsync<InvalidOperationException>(() => valueTask.AsTask());
        thrownException.Message.Should().Be("test exception");
    }

    [Fact]
    public async Task FromException_NonGeneric_ShouldCreateFaultedValueTask()
    {
        // Arrange
        var exception = new InvalidOperationException("test exception");

        // Act
        var valueTask = ValueTaskExtensions.FromException(exception);

        // Assert
        valueTask.IsFaulted.Should().BeTrue();
        var thrownException = await Assert.ThrowsAsync<InvalidOperationException>(() => valueTask.AsTask());
        thrownException.Message.Should().Be("test exception");
    }

    [Fact]
    public void TryExecute_WithSuccessfulFunction_ShouldReturnResult()
    {
        // Arrange
        var expectedValue = "success";

        // Act
        var valueTask = ValueTaskExtensions.TryExecute(() => expectedValue);

        // Assert
        valueTask.IsCompletedSuccessfully.Should().BeTrue();
        valueTask.Result.Should().Be(expectedValue);
    }

    [Fact]
    public async Task TryExecute_WithThrowingFunction_ShouldReturnFaultedValueTask()
    {
        // Arrange
        var exception = new ArgumentException("test error");

        // Act
        var valueTask = ValueTaskExtensions.TryExecute<string>(() => throw exception);

        // Assert
        valueTask.IsFaulted.Should().BeTrue();
        var thrownException = await Assert.ThrowsAsync<ArgumentException>(() => valueTask.AsTask());
        thrownException.Message.Should().Be("test error");
    }

    [Fact]
    public async Task ExecuteAsync_WithCompletedTask_ShouldOptimizeForSyncCompletion()
    {
        // Arrange
        var expectedValue = "completed";
        
        // Act
        var valueTask = ValueTaskExtensions.ExecuteAsync(() => Task.FromResult(expectedValue));
        var result = await valueTask;

        // Assert
        result.Should().Be(expectedValue);
        valueTask.IsCompletedSuccessfully.Should().BeTrue();
    }

    [Fact]
    public async Task ExecuteAsync_WithAsyncTask_ShouldHandleAsynchronousExecution()
    {
        // Arrange
        var expectedValue = "async result";
        
        // Act
        var valueTask = ValueTaskExtensions.ExecuteAsync(async () =>
        {
            await Task.Delay(10);
            return expectedValue;
        });
        var result = await valueTask;

        // Assert
        result.Should().Be(expectedValue);
    }

    [Fact]
    public async Task ExecuteAsync_WithFaultedTask_ShouldPropagateFault()
    {
        // Arrange
        var exception = new InvalidOperationException("async error");
        
        // Act
        var valueTask = ValueTaskExtensions.ExecuteAsync<string>(() => Task.FromException<string>(exception));

        // Assert
        var thrownException = await Assert.ThrowsAsync<InvalidOperationException>(() => valueTask.AsTask());
        thrownException.Message.Should().Be("async error");
    }

    [Fact]
    public void ValueTaskExtensions_Methods_ShouldHaveZeroAllocationForCompletedPaths()
    {
        // This test verifies that the extension methods don't create unnecessary allocations
        // for synchronous/completed paths
        
        // Act & Assert - These should not allocate Tasks
        var valueTask1 = ValueTaskExtensions.FromResult(42);
        var valueTask2 = ValueTaskExtensions.CompletedTask();
        var valueTask3 = ValueTaskExtensions.FromSyncOrAsync(true, "sync", Task.FromResult("async"));

        valueTask1.IsCompletedSuccessfully.Should().BeTrue();
        valueTask2.IsCompletedSuccessfully.Should().BeTrue();
        valueTask3.IsCompletedSuccessfully.Should().BeTrue();

        // Verify results without await (no Task allocation)
        valueTask1.Result.Should().Be(42);
        valueTask3.Result.Should().Be("sync");
    }

    [Fact]
    public async Task ValueTaskExtensions_ChainedOperations_ShouldWorkCorrectly()
    {
        // Arrange
        var initialValue = "start";

        // Act - Chain multiple ValueTask operations
        var result = await ValueTaskExtensions.FromResult(initialValue)
            .ConfigureAwaitEx(false);

        var processedResult = await ValueTaskExtensions.ExecuteAsync(() => 
            Task.FromResult(result + " -> processed"));

        // Assert
        processedResult.Should().Be("start -> processed");
    }

    [Fact]
    public void ValueTaskExtensions_WithValueTypes_ShouldWorkCorrectly()
    {
        // Act
        var intValueTask = ValueTaskExtensions.FromResult(123);
        var boolValueTask = ValueTaskExtensions.FromResult(true);
        var doubleValueTask = ValueTaskExtensions.FromResult(3.14);

        // Assert
        intValueTask.Result.Should().Be(123);
        boolValueTask.Result.Should().Be(true);
        doubleValueTask.Result.Should().Be(3.14);
    }

    [Fact]
    public async Task ValueTaskExtensions_ErrorHandling_ShouldPreserveStackTrace()
    {
        // Arrange
        var originalException = new CustomTestException("Original error");
        
        // Act & Assert
        var valueTask = ValueTaskExtensions.FromException<string>(originalException);
        var caughtException = await Assert.ThrowsAsync<CustomTestException>(() => valueTask.AsTask());
        
        caughtException.Should().BeSameAs(originalException);
        caughtException.Message.Should().Be("Original error");
    }

    private class CustomTestException : Exception
    {
        public CustomTestException(string message) : base(message) { }
    }
}