using FluentAssertions;
using Orleans.StateMachineES.Batch;

namespace Orleans.StateMachineES.Tests.Unit.Batch;

public class BatchOperationTests
{
    private enum TestState { Idle, Processing, Completed, Failed }
    private enum TestTrigger { Start, Process, Complete, Fail }

    [Fact]
    public void BatchOperationRequest_Create_ShouldSetProperties()
    {
        // Arrange & Act
        var request = BatchOperationRequest<TestTrigger>.Create(
            "grain-123",
            TestTrigger.Start,
            new object[] { "arg1", 42 },
            "correlation-456");

        // Assert
        request.GrainId.Should().Be("grain-123");
        request.Trigger.Should().Be(TestTrigger.Start);
        request.Arguments.Should().BeEquivalentTo(new object[] { "arg1", 42 });
        request.CorrelationId.Should().Be("correlation-456");
    }

    [Fact]
    public void BatchItemResult_Success_ShouldSetCorrectProperties()
    {
        // Act
        var result = BatchItemResult<TestState>.Success(
            "grain-123",
            TestState.Idle,
            TestState.Processing,
            TimeSpan.FromMilliseconds(100),
            5,
            "correlation-456");

        // Assert
        result.GrainId.Should().Be("grain-123");
        result.IsSuccess.Should().BeTrue();
        result.FromState.Should().Be(TestState.Idle);
        result.ToState.Should().Be(TestState.Processing);
        result.Duration.Should().Be(TimeSpan.FromMilliseconds(100));
        result.BatchIndex.Should().Be(5);
        result.CorrelationId.Should().Be("correlation-456");
        result.ErrorMessage.Should().BeNull();
    }

    [Fact]
    public void BatchItemResult_Failure_ShouldSetCorrectProperties()
    {
        // Act
        var result = BatchItemResult<TestState>.Failure(
            "grain-123",
            "Invalid transition",
            "InvalidOperationException",
            TimeSpan.FromMilliseconds(50),
            3,
            "correlation-456");

        // Assert
        result.GrainId.Should().Be("grain-123");
        result.IsSuccess.Should().BeFalse();
        result.ErrorMessage.Should().Be("Invalid transition");
        result.ExceptionType.Should().Be("InvalidOperationException");
        result.Duration.Should().Be(TimeSpan.FromMilliseconds(50));
        result.BatchIndex.Should().Be(3);
        result.CorrelationId.Should().Be("correlation-456");
    }

    [Fact]
    public void BatchOperationResult_FullySuccessful_ShouldBeTrue()
    {
        // Arrange
        var result = new BatchOperationResult<TestState>
        {
            TotalOperations = 10,
            SuccessCount = 10,
            FailureCount = 0,
            SkippedCount = 0
        };

        // Assert
        result.IsFullySuccessful.Should().BeTrue();
        result.IsPartiallySuccessful.Should().BeFalse();
        result.IsFullyFailed.Should().BeFalse();
        result.SuccessRate.Should().Be(100);
    }

    [Fact]
    public void BatchOperationResult_PartiallySuccessful_ShouldBeTrue()
    {
        // Arrange
        var result = new BatchOperationResult<TestState>
        {
            TotalOperations = 10,
            SuccessCount = 7,
            FailureCount = 3,
            SkippedCount = 0
        };

        // Assert
        result.IsFullySuccessful.Should().BeFalse();
        result.IsPartiallySuccessful.Should().BeTrue();
        result.IsFullyFailed.Should().BeFalse();
        result.SuccessRate.Should().Be(70);
    }

    [Fact]
    public void BatchOperationResult_FullyFailed_ShouldBeTrue()
    {
        // Arrange
        var result = new BatchOperationResult<TestState>
        {
            TotalOperations = 10,
            SuccessCount = 0,
            FailureCount = 10,
            SkippedCount = 0
        };

        // Assert
        result.IsFullySuccessful.Should().BeFalse();
        result.IsPartiallySuccessful.Should().BeFalse();
        result.IsFullyFailed.Should().BeTrue();
        result.SuccessRate.Should().Be(0);
    }

    [Fact]
    public void BatchOperationResult_GetFailedResults_ShouldFilterCorrectly()
    {
        // Arrange
        var results = new List<BatchItemResult<TestState>>
        {
            BatchItemResult<TestState>.Success("grain-1", TestState.Idle, TestState.Processing, TimeSpan.Zero, 0),
            BatchItemResult<TestState>.Failure("grain-2", "Error", null, TimeSpan.Zero, 1),
            BatchItemResult<TestState>.Success("grain-3", TestState.Processing, TestState.Completed, TimeSpan.Zero, 2),
            BatchItemResult<TestState>.Failure("grain-4", "Another Error", null, TimeSpan.Zero, 3)
        };

        var batchResult = new BatchOperationResult<TestState>
        {
            TotalOperations = 4,
            SuccessCount = 2,
            FailureCount = 2,
            Results = results
        };

        // Act
        var failedResults = batchResult.GetFailedResults().ToList();

        // Assert
        failedResults.Should().HaveCount(2);
        failedResults.Select(r => r.GrainId).Should().BeEquivalentTo(new[] { "grain-2", "grain-4" });
    }

    [Fact]
    public void BatchOperationResult_AverageOperationDuration_ShouldCalculateCorrectly()
    {
        // Arrange
        var result = new BatchOperationResult<TestState>
        {
            TotalOperations = 4,
            Duration = TimeSpan.FromMilliseconds(400)
        };

        // Assert
        result.AverageOperationDuration.Should().Be(TimeSpan.FromMilliseconds(100));
    }

    [Fact]
    public void BatchOperationOptions_DefaultValues_ShouldBeSet()
    {
        // Arrange & Act
        var options = new BatchOperationOptions();

        // Assert
        options.MaxParallelism.Should().Be(10);
        options.StopOnFirstFailure.Should().BeFalse();
        options.Timeout.Should().Be(TimeSpan.FromMinutes(5));
        options.OperationTimeout.Should().Be(TimeSpan.FromSeconds(30));
        options.ContinueOnError.Should().BeTrue();
        options.OrderByPriority.Should().BeFalse();
        options.EnableRetry.Should().BeFalse();
        options.MaxRetryAttempts.Should().Be(3);
        options.RetryDelay.Should().Be(TimeSpan.FromSeconds(1));
        options.UseExponentialBackoff.Should().BeTrue();
    }

    [Fact]
    public void BatchOperationRequest_Priority_ShouldDefaultToZero()
    {
        // Arrange & Act
        var request = new BatchOperationRequest<TestTrigger>
        {
            GrainId = "grain-1",
            Trigger = TestTrigger.Start
        };

        // Assert
        request.Priority.Should().Be(0);
    }

    [Fact]
    public void BatchOperationResult_GroupByErrorType_ShouldGroupCorrectly()
    {
        // Arrange
        var results = new List<BatchItemResult<TestState>>
        {
            BatchItemResult<TestState>.Success("grain-1", TestState.Idle, TestState.Processing, TimeSpan.Zero, 0),
            BatchItemResult<TestState>.Failure("grain-2", "Timeout", "TimeoutException", TimeSpan.Zero, 1),
            BatchItemResult<TestState>.Failure("grain-3", "Invalid", "InvalidOperationException", TimeSpan.Zero, 2),
            BatchItemResult<TestState>.Failure("grain-4", "Another Timeout", "TimeoutException", TimeSpan.Zero, 3)
        };

        var batchResult = new BatchOperationResult<TestState>
        {
            TotalOperations = 4,
            SuccessCount = 1,
            FailureCount = 3,
            Results = results
        };

        // Act
        var groupedByError = batchResult.GetResultsByErrorType().ToList();

        // Assert
        groupedByError.Should().HaveCount(2);
        groupedByError.First(g => g.Key == "TimeoutException").Should().HaveCount(2);
        groupedByError.First(g => g.Key == "InvalidOperationException").Should().HaveCount(1);
    }

    [Fact]
    public void BatchItemCompletedEventArgs_ShouldHaveCorrectProgress()
    {
        // Arrange & Act
        var args = new BatchItemCompletedEventArgs
        {
            GrainId = "grain-1",
            IsSuccess = true,
            BatchIndex = 4,
            CompletedCount = 5,
            TotalCount = 10
        };

        // Assert
        args.CompletedCount.Should().Be(5);
        args.TotalCount.Should().Be(10);
    }
}
