using FluentAssertions;
using Orleans.StateMachineES.Memory;
using Stateless;
using Xunit;

namespace Orleans.StateMachineES.Tests.Unit.Memory;

public class TriggerParameterCacheTests
{
    private enum TestState { Idle, Active, Processing }
    private enum TestTrigger { Start, Stop, Process }

    [Fact]
    public void Constructor_WithNullStateMachine_ShouldThrow()
    {
        // Act & Assert
        Assert.Throws<ArgumentNullException>(() =>
            new TriggerParameterCache<TestState, TestTrigger>(null!));
    }

    [Fact]
    public void Constructor_WithValidStateMachine_ShouldInitialize()
    {
        // Arrange
        var stateMachine = new StateMachine<TestState, TestTrigger>(TestState.Idle);

        // Act
        var cache = new TriggerParameterCache<TestState, TestTrigger>(stateMachine);

        // Assert
        cache.Should().NotBeNull();
        cache.Count.Should().Be(0);
    }

    [Fact]
    public void GetOrCreate_WithSingleParameter_ShouldReturnTriggerWithParameters()
    {
        // Arrange
        var stateMachine = new StateMachine<TestState, TestTrigger>(TestState.Idle);
        var cache = new TriggerParameterCache<TestState, TestTrigger>(stateMachine);

        // Act
        var triggerParam = cache.GetOrCreate<int>(TestTrigger.Process);

        // Assert
        triggerParam.Should().NotBeNull();
        cache.Count.Should().Be(1);
        cache.Contains(TestTrigger.Process).Should().BeTrue();
    }

    [Fact]
    public void GetOrCreate_WithTwoParameters_ShouldReturnTriggerWithParameters()
    {
        // Arrange
        var stateMachine = new StateMachine<TestState, TestTrigger>(TestState.Idle);
        var cache = new TriggerParameterCache<TestState, TestTrigger>(stateMachine);

        // Act
        var triggerParam = cache.GetOrCreate<int, string>(TestTrigger.Process);

        // Assert
        triggerParam.Should().NotBeNull();
        cache.Count.Should().Be(1);
    }

    [Fact]
    public void GetOrCreate_WithThreeParameters_ShouldReturnTriggerWithParameters()
    {
        // Arrange
        var stateMachine = new StateMachine<TestState, TestTrigger>(TestState.Idle);
        var cache = new TriggerParameterCache<TestState, TestTrigger>(stateMachine);

        // Act
        var triggerParam = cache.GetOrCreate<int, string, bool>(TestTrigger.Process);

        // Assert
        triggerParam.Should().NotBeNull();
        cache.Count.Should().Be(1);
    }

    [Fact]
    public void GetOrCreate_CalledTwiceWithSameTrigger_ShouldReturnCachedInstance()
    {
        // Arrange
        var stateMachine = new StateMachine<TestState, TestTrigger>(TestState.Idle);
        var cache = new TriggerParameterCache<TestState, TestTrigger>(stateMachine);

        // Act
        var triggerParam1 = cache.GetOrCreate<int>(TestTrigger.Process);
        var triggerParam2 = cache.GetOrCreate<int>(TestTrigger.Process);

        // Assert
        triggerParam2.Should().BeSameAs(triggerParam1);
        cache.Count.Should().Be(1); // Should still be 1, not 2
    }

    [Fact]
    public void GetOrCreate_WithDifferentTriggers_ShouldCacheEachSeparately()
    {
        // Arrange
        var stateMachine = new StateMachine<TestState, TestTrigger>(TestState.Idle);
        var cache = new TriggerParameterCache<TestState, TestTrigger>(stateMachine);

        // Act
        var startTrigger = cache.GetOrCreate<int>(TestTrigger.Start);
        var stopTrigger = cache.GetOrCreate<int>(TestTrigger.Stop);
        var processTrigger = cache.GetOrCreate<int>(TestTrigger.Process);

        // Assert
        startTrigger.Should().NotBeSameAs(stopTrigger);
        stopTrigger.Should().NotBeSameAs(processTrigger);
        cache.Count.Should().Be(3);
    }

    [Fact]
    public void Clear_ShouldRemoveAllCachedTriggers()
    {
        // Arrange
        var stateMachine = new StateMachine<TestState, TestTrigger>(TestState.Idle);
        var cache = new TriggerParameterCache<TestState, TestTrigger>(stateMachine);
        cache.GetOrCreate<int>(TestTrigger.Start);
        cache.GetOrCreate<int>(TestTrigger.Stop);
        cache.GetOrCreate<int>(TestTrigger.Process);

        // Act
        cache.Clear();

        // Assert
        cache.Count.Should().Be(0);
        cache.Contains(TestTrigger.Start).Should().BeFalse();
        cache.Contains(TestTrigger.Stop).Should().BeFalse();
        cache.Contains(TestTrigger.Process).Should().BeFalse();
    }

    [Fact]
    public void Contains_WithCachedTrigger_ShouldReturnTrue()
    {
        // Arrange
        var stateMachine = new StateMachine<TestState, TestTrigger>(TestState.Idle);
        var cache = new TriggerParameterCache<TestState, TestTrigger>(stateMachine);
        cache.GetOrCreate<int>(TestTrigger.Process);

        // Act & Assert
        cache.Contains(TestTrigger.Process).Should().BeTrue();
        cache.Contains(TestTrigger.Start).Should().BeFalse();
    }

    [Fact]
    public void Count_ShouldReflectNumberOfCachedTriggers()
    {
        // Arrange
        var stateMachine = new StateMachine<TestState, TestTrigger>(TestState.Idle);
        var cache = new TriggerParameterCache<TestState, TestTrigger>(stateMachine);

        // Assert initial state
        cache.Count.Should().Be(0);

        // Act - Add triggers
        cache.GetOrCreate<int>(TestTrigger.Start);
        cache.Count.Should().Be(1);

        cache.GetOrCreate<string>(TestTrigger.Stop);
        cache.Count.Should().Be(2);

        cache.GetOrCreate<bool>(TestTrigger.Process);
        cache.Count.Should().Be(3);

        // Act - Get cached trigger (shouldn't increase count)
        cache.GetOrCreate<int>(TestTrigger.Start);
        cache.Count.Should().Be(3);
    }

    [Fact]
    public void GetOrCreate_WithDifferentParameterTypes_ShouldCacheByTriggerOnly()
    {
        // Arrange
        var stateMachine = new StateMachine<TestState, TestTrigger>(TestState.Idle);
        var cache = new TriggerParameterCache<TestState, TestTrigger>(stateMachine);

        // Act - Get with int parameter first
        var trigger1 = cache.GetOrCreate<int>(TestTrigger.Process);

        // Act - Get with string parameter (should return cached version)
        var trigger2 = cache.GetOrCreate<string>(TestTrigger.Process);

        // Assert - Both should reference same cached trigger (casted differently)
        cache.Count.Should().Be(1);
        // Note: This tests the current behavior where cache is keyed by trigger only
    }

    [Fact]
    public void GetOrCreate_MultipleThreads_ShouldBeThreadSafe()
    {
        // Arrange
        var stateMachine = new StateMachine<TestState, TestTrigger>(TestState.Idle);
        var cache = new TriggerParameterCache<TestState, TestTrigger>(stateMachine);
        var tasks = new System.Collections.Generic.List<System.Threading.Tasks.Task>();
        var exceptions = new System.Collections.Generic.List<Exception>();

        // Act - Simulate concurrent access
        for (int i = 0; i < 10; i++)
        {
            tasks.Add(System.Threading.Tasks.Task.Run(() =>
            {
                try
                {
                    for (int j = 0; j < 100; j++)
                    {
                        var trigger = cache.GetOrCreate<int>(TestTrigger.Process);
                        trigger.Should().NotBeNull();
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

        System.Threading.Tasks.Task.WaitAll([.. tasks]);

        // Assert
        exceptions.Should().BeEmpty();
        cache.Count.Should().Be(1); // Should only have one cached trigger despite concurrent access
    }

    [Fact]
    public void GetOrCreate_AfterClear_ShouldRecreateCache()
    {
        // Arrange
        var stateMachine = new StateMachine<TestState, TestTrigger>(TestState.Idle);
        var cache = new TriggerParameterCache<TestState, TestTrigger>(stateMachine);
        var trigger1 = cache.GetOrCreate<int>(TestTrigger.Process);

        // Act
        cache.Clear();
        var trigger2 = cache.GetOrCreate<int>(TestTrigger.Process);

        // Assert
        trigger2.Should().NotBeSameAs(trigger1); // Should be a new instance after clear
        cache.Count.Should().Be(1);
    }
}
