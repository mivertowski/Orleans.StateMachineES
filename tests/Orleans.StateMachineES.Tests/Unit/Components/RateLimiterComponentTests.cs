using FluentAssertions;
using Orleans.StateMachineES.Composition.Components;
using Stateless;

namespace Orleans.StateMachineES.Tests.Unit.Components;

public class RateLimiterComponentTests
{
    private enum TestState { Idle, Processing, Completed }
    private enum TestTrigger { Start, Process, Complete }

    private static StateMachine<TestState, TestTrigger> CreateTestStateMachine()
    {
        var machine = new StateMachine<TestState, TestTrigger>(TestState.Idle);
        machine.Configure(TestState.Idle)
            .Permit(TestTrigger.Start, TestState.Processing);
        machine.Configure(TestState.Processing)
            .Permit(TestTrigger.Process, TestState.Processing)
            .Permit(TestTrigger.Complete, TestState.Completed);
        return machine;
    }

    [Fact]
    public async Task TryAcquireAsync_WithAvailableTokens_ShouldSucceed()
    {
        // Arrange
        var options = new RateLimiterOptions
        {
            TokensPerInterval = 10,
            BurstCapacity = 20
        };
        var rateLimiter = new RateLimiterComponent<TestState, TestTrigger>(options);
        var stateMachine = CreateTestStateMachine();

        // Act
        var result = await rateLimiter.TryAcquireAsync(TestTrigger.Start, stateMachine);

        // Assert
        result.Should().BeTrue();
        rateLimiter.AvailableTokens.Should().Be(19); // Started with 20, used 1
        rateLimiter.TotalAllowed.Should().Be(1);
    }

    [Fact]
    public async Task TryAcquireAsync_WhenTokensExhausted_ShouldFail()
    {
        // Arrange
        var options = new RateLimiterOptions
        {
            TokensPerInterval = 5,
            BurstCapacity = 3,
            ThrowWhenExceeded = false
        };
        var rateLimiter = new RateLimiterComponent<TestState, TestTrigger>(options);
        var stateMachine = CreateTestStateMachine();

        // Exhaust all tokens
        for (int i = 0; i < 3; i++)
        {
            await rateLimiter.TryAcquireAsync(TestTrigger.Process, stateMachine);
        }

        // Act
        var result = await rateLimiter.TryAcquireAsync(TestTrigger.Process, stateMachine);

        // Assert
        result.Should().BeFalse();
        rateLimiter.AvailableTokens.Should().Be(0);
        rateLimiter.TotalRejected.Should().Be(1);
    }

    [Fact]
    public async Task TryAcquireAsync_WhenTokensExhaustedAndThrowEnabled_ShouldThrow()
    {
        // Arrange
        var options = new RateLimiterOptions
        {
            TokensPerInterval = 5,
            BurstCapacity = 2,
            ThrowWhenExceeded = true
        };
        var rateLimiter = new RateLimiterComponent<TestState, TestTrigger>(options);
        var stateMachine = CreateTestStateMachine();

        // Exhaust all tokens
        await rateLimiter.TryAcquireAsync(TestTrigger.Process, stateMachine);
        await rateLimiter.TryAcquireAsync(TestTrigger.Process, stateMachine);

        // Act & Assert
        var exception = await Assert.ThrowsAsync<RateLimitExceededException>(
            () => rateLimiter.TryAcquireAsync(TestTrigger.Process, stateMachine));

        exception.Trigger.Should().Be(TestTrigger.Process);
        exception.AvailableTokens.Should().Be(0);
        exception.RequiredTokens.Should().Be(1);
    }

    [Fact]
    public async Task TryAcquireAsync_WithMonitoredTriggers_ShouldOnlyLimitSpecifiedTriggers()
    {
        // Arrange
        var options = new RateLimiterOptions
        {
            TokensPerInterval = 5,
            BurstCapacity = 1,
            ThrowWhenExceeded = true,
            MonitoredTriggers = new object[] { TestTrigger.Process }
        };
        var rateLimiter = new RateLimiterComponent<TestState, TestTrigger>(options);
        var stateMachine = CreateTestStateMachine();

        // Exhaust the single token with monitored trigger
        await rateLimiter.TryAcquireAsync(TestTrigger.Process, stateMachine);

        // Act - Unmonitored trigger should succeed
        var result = await rateLimiter.TryAcquireAsync(TestTrigger.Start, stateMachine);

        // Assert
        result.Should().BeTrue();
        rateLimiter.AvailableTokens.Should().Be(0); // Token wasn't taken for unmonitored trigger
    }

    [Fact]
    public async Task TryAcquireAsync_TokensRefillOverTime()
    {
        // Arrange
        var options = new RateLimiterOptions
        {
            TokensPerInterval = 100,
            BurstCapacity = 5,
            RefillInterval = TimeSpan.FromMilliseconds(100),
            UseSlidingWindow = true
        };
        var rateLimiter = new RateLimiterComponent<TestState, TestTrigger>(options);
        var stateMachine = CreateTestStateMachine();

        // Exhaust all tokens
        for (int i = 0; i < 5; i++)
        {
            await rateLimiter.TryAcquireAsync(TestTrigger.Process, stateMachine);
        }
        rateLimiter.AvailableTokens.Should().Be(0);

        // Wait for tokens to refill
        await Task.Delay(150);

        // Act
        var result = await rateLimiter.TryAcquireAsync(TestTrigger.Process, stateMachine);

        // Assert
        result.Should().BeTrue();
    }

    [Fact]
    public async Task TryAcquireAsync_WithMultipleTokensPerOperation()
    {
        // Arrange
        var options = new RateLimiterOptions
        {
            TokensPerInterval = 10,
            BurstCapacity = 10,
            TokensPerOperation = 5
        };
        var rateLimiter = new RateLimiterComponent<TestState, TestTrigger>(options);
        var stateMachine = CreateTestStateMachine();

        // Act
        var result1 = await rateLimiter.TryAcquireAsync(TestTrigger.Process, stateMachine);
        var result2 = await rateLimiter.TryAcquireAsync(TestTrigger.Process, stateMachine);
        var result3 = await rateLimiter.TryAcquireAsync(TestTrigger.Process, stateMachine);

        // Assert
        result1.Should().BeTrue();
        result2.Should().BeTrue();
        result3.Should().BeFalse(); // Not enough tokens (need 5, have 0)
        rateLimiter.AvailableTokens.Should().Be(0);
    }

    [Fact]
    public void Reset_ShouldRestoreFullCapacity()
    {
        // Arrange
        var options = new RateLimiterOptions
        {
            TokensPerInterval = 10,
            BurstCapacity = 100
        };
        var rateLimiter = new RateLimiterComponent<TestState, TestTrigger>(options);
        var stateMachine = CreateTestStateMachine();

        // Consume some tokens
        for (int i = 0; i < 50; i++)
        {
            rateLimiter.TryAcquireAsync(TestTrigger.Process, stateMachine).GetAwaiter().GetResult();
        }
        rateLimiter.AvailableTokens.Should().Be(50);

        // Act
        rateLimiter.Reset();

        // Assert
        rateLimiter.AvailableTokens.Should().Be(100);
    }

    [Fact]
    public void GetStatistics_ShouldReturnAccurateStats()
    {
        // Arrange
        var options = new RateLimiterOptions
        {
            TokensPerInterval = 10,
            BurstCapacity = 20,
            RefillInterval = TimeSpan.FromSeconds(1)
        };
        var rateLimiter = new RateLimiterComponent<TestState, TestTrigger>(options);
        var stateMachine = CreateTestStateMachine();

        // Perform some operations
        for (int i = 0; i < 5; i++)
        {
            rateLimiter.TryAcquireAsync(TestTrigger.Process, stateMachine).GetAwaiter().GetResult();
        }

        // Act
        var stats = rateLimiter.GetStatistics();

        // Assert
        stats.AvailableTokens.Should().Be(15);
        stats.BurstCapacity.Should().Be(20);
        stats.TokensPerInterval.Should().Be(10);
        stats.RefillInterval.Should().Be(TimeSpan.FromSeconds(1));
        stats.TotalAllowed.Should().Be(5);
        stats.TotalRejected.Should().Be(0);
        stats.EffectiveRate.Should().Be(10);
        stats.UtilizationPercentage.Should().Be(25); // (20-15)/20 * 100
    }

    [Fact]
    public async Task Callbacks_ShouldBeInvoked()
    {
        // Arrange
        var tokensAcquiredCalled = false;
        var rateLimitExceededCalled = false;
        object? acquiredTrigger = null;
        int? remainingTokens = null;

        var options = new RateLimiterOptions
        {
            TokensPerInterval = 5,
            BurstCapacity = 1,
            ThrowWhenExceeded = false,
            OnTokensAcquired = (trigger, remaining) =>
            {
                tokensAcquiredCalled = true;
                acquiredTrigger = trigger;
                remainingTokens = remaining;
            },
            OnRateLimitExceeded = (trigger, available, required) =>
            {
                rateLimitExceededCalled = true;
            }
        };
        var rateLimiter = new RateLimiterComponent<TestState, TestTrigger>(options);
        var stateMachine = CreateTestStateMachine();

        // Act
        await rateLimiter.TryAcquireAsync(TestTrigger.Process, stateMachine);
        await rateLimiter.TryAcquireAsync(TestTrigger.Process, stateMachine);

        // Assert
        tokensAcquiredCalled.Should().BeTrue();
        acquiredTrigger.Should().Be(TestTrigger.Process);
        remainingTokens.Should().Be(0);
        rateLimitExceededCalled.Should().BeTrue();
    }

    [Fact]
    public void Constructor_WithInvalidOptions_ShouldThrow()
    {
        // Arrange & Act & Assert
        Assert.Throws<ArgumentException>(() =>
            new RateLimiterComponent<TestState, TestTrigger>(new RateLimiterOptions
            {
                TokensPerInterval = 0
            }));

        Assert.Throws<ArgumentException>(() =>
            new RateLimiterComponent<TestState, TestTrigger>(new RateLimiterOptions
            {
                TokensPerInterval = 10,
                BurstCapacity = 5 // Less than TokensPerInterval
            }));

        Assert.Throws<ArgumentException>(() =>
            new RateLimiterComponent<TestState, TestTrigger>(new RateLimiterOptions
            {
                RefillInterval = TimeSpan.Zero
            }));
    }

    [Fact]
    public async Task ConcurrentAccess_ShouldBeThreadSafe()
    {
        // Arrange
        var options = new RateLimiterOptions
        {
            TokensPerInterval = 100,
            BurstCapacity = 1000,
            ThrowWhenExceeded = false
        };
        var rateLimiter = new RateLimiterComponent<TestState, TestTrigger>(options);
        var stateMachine = CreateTestStateMachine();

        // Act - Fire many concurrent requests
        var tasks = Enumerable.Range(0, 100)
            .Select(_ => rateLimiter.TryAcquireAsync(TestTrigger.Process, stateMachine))
            .ToList();

        var results = await Task.WhenAll(tasks);

        // Assert
        var successCount = results.Count(r => r);
        successCount.Should().Be(100); // Should all succeed with 1000 capacity
        rateLimiter.AvailableTokens.Should().Be(900);
        rateLimiter.TotalAllowed.Should().Be(100);
    }

    [Fact]
    public void RateLimiterStats_UtilizationAndRejectionRate()
    {
        // Arrange
        var stats = new RateLimiterStats
        {
            AvailableTokens = 25,
            BurstCapacity = 100,
            TotalAllowed = 80,
            TotalRejected = 20
        };

        // Assert
        stats.UtilizationPercentage.Should().Be(75); // (100-25)/100 * 100
        stats.RejectionRate.Should().Be(20); // 20/(80+20) * 100
    }

    [Fact]
    public void RateLimitExceededException_ShouldContainDetails()
    {
        // Arrange & Act
        var exception = new RateLimitExceededException(
            "Rate limit exceeded",
            TestTrigger.Process,
            5,
            10,
            TimeSpan.FromSeconds(2));

        // Assert
        exception.Message.Should().Be("Rate limit exceeded");
        exception.Trigger.Should().Be(TestTrigger.Process);
        exception.AvailableTokens.Should().Be(5);
        exception.RequiredTokens.Should().Be(10);
        exception.RetryAfter.Should().Be(TimeSpan.FromSeconds(2));
    }

    [Fact]
    public void RateLimiterOptions_TokensPerSecond_ShouldCalculateCorrectly()
    {
        // Arrange
        var options = new RateLimiterOptions
        {
            TokensPerInterval = 50,
            RefillInterval = TimeSpan.FromSeconds(2)
        };

        // Assert
        options.TokensPerSecond.Should().Be(25); // 50 tokens / 2 seconds
    }
}
