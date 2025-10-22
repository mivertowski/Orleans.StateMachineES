using FluentAssertions;
using Orleans.StateMachineES.Composition.Components;
using Stateless;
using Xunit;

namespace Orleans.StateMachineES.Tests.Unit.Components;

public class CircuitBreakerComponentTests
{
    private enum TestState { Idle, Processing, Success, Failed }
    private enum TestTrigger { Start, Complete, Fail, Reset }

    [Fact]
    public void Constructor_WithNullOptions_ShouldThrow()
    {
        // Act & Assert
        Assert.Throws<ArgumentNullException>(() =>
            new CircuitBreakerComponent<TestState, TestTrigger>(null!));
    }

    [Fact]
    public void Constructor_WithValidOptions_ShouldInitializeInClosedState()
    {
        // Arrange
        var options = new CircuitBreakerOptions
        {
            FailureThreshold = 3,
            SuccessThreshold = 2,
            OpenDuration = TimeSpan.FromSeconds(10)
        };

        // Act
        var circuitBreaker = new CircuitBreakerComponent<TestState, TestTrigger>(options);

        // Assert
        circuitBreaker.Should().NotBeNull();
        circuitBreaker.State.Should().Be(CircuitState.Closed);
        circuitBreaker.ConsecutiveFailures.Should().Be(0);
        circuitBreaker.ConsecutiveSuccesses.Should().Be(0);
    }

    [Fact]
    public async Task BeforeFireAsync_InClosedState_ShouldAllowRequest()
    {
        // Arrange
        var options = new CircuitBreakerOptions { FailureThreshold = 3 };
        var circuitBreaker = new CircuitBreakerComponent<TestState, TestTrigger>(options);
        var stateMachine = new StateMachine<TestState, TestTrigger>(TestState.Idle);

        // Act
        var canProceed = await circuitBreaker.BeforeFireAsync(TestTrigger.Start, stateMachine);

        // Assert
        canProceed.Should().BeTrue();
        circuitBreaker.State.Should().Be(CircuitState.Closed);
    }

    [Fact]
    public async Task AfterFireFailureAsync_ReachingThreshold_ShouldOpenCircuit()
    {
        // Arrange
        var options = new CircuitBreakerOptions { FailureThreshold = 3 };
        var circuitBreaker = new CircuitBreakerComponent<TestState, TestTrigger>(options);
        var exception = new Exception("Test failure");

        // Act - Fail 3 times to reach threshold
        await circuitBreaker.AfterFireFailureAsync(TestTrigger.Start, exception);
        await circuitBreaker.AfterFireFailureAsync(TestTrigger.Start, exception);
        await circuitBreaker.AfterFireFailureAsync(TestTrigger.Start, exception);

        // Assert
        circuitBreaker.State.Should().Be(CircuitState.Open);
        circuitBreaker.ConsecutiveFailures.Should().Be(3);
    }

    [Fact]
    public async Task BeforeFireAsync_InOpenState_WithThrowWhenOpen_ShouldThrow()
    {
        // Arrange
        var options = new CircuitBreakerOptions
        {
            FailureThreshold = 1,
            ThrowWhenOpen = true
        };
        var circuitBreaker = new CircuitBreakerComponent<TestState, TestTrigger>(options);
        var stateMachine = new StateMachine<TestState, TestTrigger>(TestState.Idle);

        // Open the circuit
        await circuitBreaker.AfterFireFailureAsync(TestTrigger.Start, new Exception("Test"));

        // Act & Assert
        await Assert.ThrowsAsync<CircuitBreakerOpenException>(async () =>
            await circuitBreaker.BeforeFireAsync(TestTrigger.Start, stateMachine));
    }

    [Fact]
    public async Task BeforeFireAsync_InOpenState_WithoutThrowWhenOpen_ShouldReturnFalse()
    {
        // Arrange
        var options = new CircuitBreakerOptions
        {
            FailureThreshold = 1,
            ThrowWhenOpen = false
        };
        var circuitBreaker = new CircuitBreakerComponent<TestState, TestTrigger>(options);
        var stateMachine = new StateMachine<TestState, TestTrigger>(TestState.Idle);

        // Open the circuit
        await circuitBreaker.AfterFireFailureAsync(TestTrigger.Start, new Exception("Test"));

        // Act
        var canProceed = await circuitBreaker.BeforeFireAsync(TestTrigger.Start, stateMachine);

        // Assert
        canProceed.Should().BeFalse();
    }

    [Fact]
    public async Task BeforeFireAsync_AfterOpenDuration_ShouldTransitionToHalfOpen()
    {
        // Arrange
        var options = new CircuitBreakerOptions
        {
            FailureThreshold = 1,
            OpenDuration = TimeSpan.FromMilliseconds(100)
        };
        var circuitBreaker = new CircuitBreakerComponent<TestState, TestTrigger>(options);
        var stateMachine = new StateMachine<TestState, TestTrigger>(TestState.Idle);

        // Open the circuit
        await circuitBreaker.AfterFireFailureAsync(TestTrigger.Start, new Exception("Test"));
        circuitBreaker.State.Should().Be(CircuitState.Open);

        // Wait for OpenDuration to pass
        await Task.Delay(150);

        // Act
        var canProceed = await circuitBreaker.BeforeFireAsync(TestTrigger.Start, stateMachine);

        // Assert
        canProceed.Should().BeTrue();
        circuitBreaker.State.Should().Be(CircuitState.HalfOpen);
    }

    [Fact]
    public async Task AfterFireSuccessAsync_InHalfOpen_ReachingSuccessThreshold_ShouldCloseCircuit()
    {
        // Arrange
        var options = new CircuitBreakerOptions
        {
            FailureThreshold = 1,
            SuccessThreshold = 2,
            OpenDuration = TimeSpan.FromMilliseconds(10)
        };
        var circuitBreaker = new CircuitBreakerComponent<TestState, TestTrigger>(options);
        var stateMachine = new StateMachine<TestState, TestTrigger>(TestState.Idle);

        // Open the circuit
        await circuitBreaker.AfterFireFailureAsync(TestTrigger.Start, new Exception("Test"));

        // Wait and transition to HalfOpen
        await Task.Delay(20);
        await circuitBreaker.BeforeFireAsync(TestTrigger.Start, stateMachine);
        circuitBreaker.State.Should().Be(CircuitState.HalfOpen);

        // Act - Register successful operations to reach threshold
        await circuitBreaker.AfterFireSuccessAsync(TestTrigger.Start);
        await circuitBreaker.AfterFireSuccessAsync(TestTrigger.Start);

        // Assert
        circuitBreaker.State.Should().Be(CircuitState.Closed);
        circuitBreaker.ConsecutiveSuccesses.Should().Be(0); // Reset after closing
        circuitBreaker.ConsecutiveFailures.Should().Be(0);
    }

    [Fact]
    public async Task AfterFireFailureAsync_InHalfOpen_ShouldReopenCircuit()
    {
        // Arrange
        var options = new CircuitBreakerOptions
        {
            FailureThreshold = 1,
            OpenDuration = TimeSpan.FromMilliseconds(10)
        };
        var circuitBreaker = new CircuitBreakerComponent<TestState, TestTrigger>(options);
        var stateMachine = new StateMachine<TestState, TestTrigger>(TestState.Idle);

        // Open the circuit
        await circuitBreaker.AfterFireFailureAsync(TestTrigger.Start, new Exception("Test"));

        // Wait and transition to HalfOpen
        await Task.Delay(20);
        await circuitBreaker.BeforeFireAsync(TestTrigger.Start, stateMachine);
        circuitBreaker.State.Should().Be(CircuitState.HalfOpen);

        // Act - Fail again in HalfOpen
        await circuitBreaker.AfterFireFailureAsync(TestTrigger.Start, new Exception("Test again"));

        // Assert
        circuitBreaker.State.Should().Be(CircuitState.Open);
    }

    [Fact]
    public async Task AfterFireSuccessAsync_InClosed_ShouldResetFailureCount()
    {
        // Arrange
        var options = new CircuitBreakerOptions { FailureThreshold = 5 };
        var circuitBreaker = new CircuitBreakerComponent<TestState, TestTrigger>(options);

        // Accumulate some failures (but not enough to open)
        await circuitBreaker.AfterFireFailureAsync(TestTrigger.Start, new Exception("Test"));
        await circuitBreaker.AfterFireFailureAsync(TestTrigger.Start, new Exception("Test"));
        circuitBreaker.ConsecutiveFailures.Should().Be(2);

        // Act - Success should reset failure count
        await circuitBreaker.AfterFireSuccessAsync(TestTrigger.Start);

        // Assert
        circuitBreaker.State.Should().Be(CircuitState.Closed);
        circuitBreaker.ConsecutiveFailures.Should().Be(0);
    }

    [Fact]
    public async Task MonitoredTriggers_OnlyMonitoredTriggers_ShouldAffectCircuit()
    {
        // Arrange
        var options = new CircuitBreakerOptions
        {
            FailureThreshold = 2,
            MonitoredTriggers = new[] { TestTrigger.Start } // Only monitor Start trigger
        };
        var circuitBreaker = new CircuitBreakerComponent<TestState, TestTrigger>(options);

        // Act - Fail with non-monitored trigger
        await circuitBreaker.AfterFireFailureAsync(TestTrigger.Complete, new Exception("Test"));
        await circuitBreaker.AfterFireFailureAsync(TestTrigger.Complete, new Exception("Test"));
        await circuitBreaker.AfterFireFailureAsync(TestTrigger.Complete, new Exception("Test"));

        // Assert - Circuit should still be closed (Complete trigger not monitored)
        circuitBreaker.State.Should().Be(CircuitState.Closed);
        circuitBreaker.ConsecutiveFailures.Should().Be(0);

        // Act - Fail with monitored trigger
        await circuitBreaker.AfterFireFailureAsync(TestTrigger.Start, new Exception("Test"));
        await circuitBreaker.AfterFireFailureAsync(TestTrigger.Start, new Exception("Test"));

        // Assert - Now circuit should open
        circuitBreaker.State.Should().Be(CircuitState.Open);
    }

    [Fact]
    public async Task OnCircuitOpened_Callback_ShouldBeInvoked()
    {
        // Arrange
        bool callbackInvoked = false;
        int failureCount = 0;

        var options = new CircuitBreakerOptions
        {
            FailureThreshold = 2,
            OnCircuitOpened = (state, failures) =>
            {
                callbackInvoked = true;
                failureCount = failures;
            }
        };
        var circuitBreaker = new CircuitBreakerComponent<TestState, TestTrigger>(options);

        // Act - Reach failure threshold
        await circuitBreaker.AfterFireFailureAsync(TestTrigger.Start, new Exception("Test"));
        await circuitBreaker.AfterFireFailureAsync(TestTrigger.Start, new Exception("Test"));

        // Assert
        callbackInvoked.Should().BeTrue();
        failureCount.Should().Be(2);
        circuitBreaker.State.Should().Be(CircuitState.Open);
    }

    [Fact]
    public async Task OnCircuitClosed_Callback_ShouldBeInvoked()
    {
        // Arrange
        bool callbackInvoked = false;

        var options = new CircuitBreakerOptions
        {
            FailureThreshold = 1,
            SuccessThreshold = 1,
            OpenDuration = TimeSpan.FromMilliseconds(10),
            OnCircuitClosed = (state) =>
            {
                callbackInvoked = true;
            }
        };
        var circuitBreaker = new CircuitBreakerComponent<TestState, TestTrigger>(options);
        var stateMachine = new StateMachine<TestState, TestTrigger>(TestState.Idle);

        // Open the circuit
        await circuitBreaker.AfterFireFailureAsync(TestTrigger.Start, new Exception("Test"));

        // Transition to HalfOpen
        await Task.Delay(20);
        await circuitBreaker.BeforeFireAsync(TestTrigger.Start, stateMachine);

        // Act - Close the circuit
        await circuitBreaker.AfterFireSuccessAsync(TestTrigger.Start);

        // Assert
        callbackInvoked.Should().BeTrue();
        circuitBreaker.State.Should().Be(CircuitState.Closed);
    }

    [Fact]
    public async Task OnCircuitHalfOpened_Callback_ShouldBeInvoked()
    {
        // Arrange
        bool callbackInvoked = false;

        var options = new CircuitBreakerOptions
        {
            FailureThreshold = 1,
            OpenDuration = TimeSpan.FromMilliseconds(10),
            OnCircuitHalfOpened = (state) =>
            {
                callbackInvoked = true;
            }
        };
        var circuitBreaker = new CircuitBreakerComponent<TestState, TestTrigger>(options);
        var stateMachine = new StateMachine<TestState, TestTrigger>(TestState.Idle);

        // Open the circuit
        await circuitBreaker.AfterFireFailureAsync(TestTrigger.Start, new Exception("Test"));

        // Act - Wait and transition to HalfOpen
        await Task.Delay(20);
        await circuitBreaker.BeforeFireAsync(TestTrigger.Start, stateMachine);

        // Assert
        callbackInvoked.Should().BeTrue();
        circuitBreaker.State.Should().Be(CircuitState.HalfOpen);
    }

    [Fact]
    public async Task Reset_ShouldCloseCircuitAndResetCounters()
    {
        // Arrange
        var options = new CircuitBreakerOptions { FailureThreshold = 1 };
        var circuitBreaker = new CircuitBreakerComponent<TestState, TestTrigger>(options);

        // Open the circuit
        await circuitBreaker.AfterFireFailureAsync(TestTrigger.Start, new Exception("Test"));
        circuitBreaker.State.Should().Be(CircuitState.Open);

        // Act
        circuitBreaker.Reset();

        // Assert
        circuitBreaker.State.Should().Be(CircuitState.Closed);
        circuitBreaker.ConsecutiveFailures.Should().Be(0);
        circuitBreaker.ConsecutiveSuccesses.Should().Be(0);
    }

    [Fact]
    public async Task ConcurrentOperations_ShouldBeThreadSafe()
    {
        // Arrange
        var options = new CircuitBreakerOptions
        {
            FailureThreshold = 100, // High threshold to avoid opening during test
            SuccessThreshold = 2
        };
        var circuitBreaker = new CircuitBreakerComponent<TestState, TestTrigger>(options);
        var stateMachine = new StateMachine<TestState, TestTrigger>(TestState.Idle);
        var tasks = new System.Collections.Generic.List<Task>();
        var exceptions = new System.Collections.Generic.List<Exception>();

        // Act - Simulate concurrent successes and failures
        for (int i = 0; i < 10; i++)
        {
            tasks.Add(Task.Run(async () =>
            {
                try
                {
                    for (int j = 0; j < 50; j++)
                    {
                        await circuitBreaker.BeforeFireAsync(TestTrigger.Start, stateMachine);
                        if (j % 2 == 0)
                        {
                            await circuitBreaker.AfterFireSuccessAsync(TestTrigger.Start);
                        }
                        else
                        {
                            await circuitBreaker.AfterFireFailureAsync(TestTrigger.Start, new Exception("Test"));
                        }
                    }
                }
                catch (Exception ex)
                {
                    lock (exceptions)
                    {
                        exceptions.Add(ex);
                    }
                }
            }));
        }

        await Task.WhenAll(tasks);

        // Assert
        exceptions.Should().BeEmpty();
        circuitBreaker.State.Should().Be(CircuitState.Closed); // Should still be closed due to high threshold
    }
}
