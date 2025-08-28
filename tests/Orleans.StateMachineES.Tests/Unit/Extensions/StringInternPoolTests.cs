using System;
using System.Linq;
using FluentAssertions;
using Orleans.StateMachineES.Extensions;
using Xunit;

namespace Orleans.StateMachineES.Tests.Unit.Extensions;

public class StringInternPoolTests
{
    [Fact]
    public void StringInternPool_Constructor_WithValidMaxSize_ShouldInitialize()
    {
        // Act
        var pool = new StringInternPool(1000);

        // Assert
        pool.Count.Should().Be(0);
        pool.Capacity.Should().Be(1000);
    }

    [Fact]
    public void StringInternPool_Constructor_WithInvalidMaxSize_ShouldThrow()
    {
        // Act & Assert
        Assert.Throws<ArgumentException>(() => new StringInternPool(0));
        Assert.Throws<ArgumentException>(() => new StringInternPool(-1));
    }

    [Fact]
    public void StringInternPool_Default_ShouldNotBeNull()
    {
        // Assert
        StringInternPool.Default.Should().NotBeNull();
        StringInternPool.Default.Capacity.Should().Be(10000);
    }

    [Fact]
    public void Intern_WithNull_ShouldReturnNull()
    {
        // Arrange
        var pool = new StringInternPool(100);

        // Act
        var result = pool.Intern(null);

        // Assert
        result.Should().BeNull();
    }

    [Fact]
    public void Intern_WithEmptyString_ShouldReturnEmptyString()
    {
        // Arrange
        var pool = new StringInternPool(100);

        // Act
        var result = pool.Intern(string.Empty);

        // Assert
        result.Should().Be(string.Empty);
        result.Should().BeSameAs(string.Empty);
    }

    [Fact]
    public void Intern_WithShortString_ShouldReturnOriginalString()
    {
        // Arrange
        var pool = new StringInternPool(100);
        var shortString = "ab";

        // Act
        var result = pool.Intern(shortString);

        // Assert
        result.Should().BeSameAs(shortString);
        pool.Count.Should().Be(0); // Short strings are not added to pool
    }

    [Fact]
    public void Intern_WithSameString_ShouldReturnSameInstance()
    {
        // Arrange
        var pool = new StringInternPool(100);
        var originalString = "test string";

        // Act
        var result1 = pool.Intern(originalString);
        var result2 = pool.Intern(originalString);

        // Assert
        result1.Should().BeSameAs(result2);
        result1.Should().Be(originalString);
        pool.Count.Should().Be(1);
    }

    [Fact]
    public void Intern_WithDifferentStringsSameValue_ShouldReturnSameInstance()
    {
        // Arrange
        var pool = new StringInternPool(100);
        var string1 = new string("test string".ToCharArray());
        var string2 = new string("test string".ToCharArray());

        // Act
        var result1 = pool.Intern(string1);
        var result2 = pool.Intern(string2);

        // Assert
        result1.Should().BeSameAs(result2);
        result1.Should().Be(string1);
        result1.Should().Be(string2);
        pool.Count.Should().Be(1);
    }

    [Fact]
    public void Intern_WhenPoolFull_ShouldNotAddNewStrings()
    {
        // Arrange
        var pool = new StringInternPool(2);
        
        // Fill the pool
        pool.Intern("string1");
        pool.Intern("string2");

        // Act - Try to add beyond capacity
        var result = pool.Intern("string3");

        // Assert
        result.Should().Be("string3");
        pool.Count.Should().Be(2); // Should not exceed capacity
    }

    [Fact]
    public void InternState_WithCommonStates_ShouldReturnPredefinedInstances()
    {
        // Arrange
        var pool = new StringInternPool(100);

        // Act & Assert
        pool.InternState("Active").Should().Be("Active");
        pool.InternState("Inactive").Should().Be("Inactive");
        pool.InternState("Completed").Should().Be("Completed");
        pool.InternState("Failed").Should().Be("Failed");
        pool.InternState("Initial").Should().Be("Initial");
        pool.InternState("Pending").Should().Be("Pending");
        pool.InternState("Processing").Should().Be("Processing");
        pool.InternState("Suspended").Should().Be("Suspended");
        pool.InternState("Terminated").Should().Be("Terminated");
        
        // All common states should return the same string literal instance
        pool.InternState("Active").Should().BeSameAs("Active");
    }

    [Fact]
    public void InternState_WithNull_ShouldReturnNull()
    {
        // Arrange
        var pool = new StringInternPool(100);

        // Act
        var result = pool.InternState(null);

        // Assert
        result.Should().BeNull();
    }

    [Fact]
    public void InternState_WithCustomState_ShouldUseRegularIntern()
    {
        // Arrange
        var pool = new StringInternPool(100);
        var customState = "CustomState";

        // Act
        var result1 = pool.InternState(customState);
        var result2 = pool.InternState(customState);

        // Assert
        result1.Should().BeSameAs(result2);
        pool.Count.Should().Be(1);
    }

    [Fact]
    public void InternTrigger_WithCommonTriggers_ShouldReturnPredefinedInstances()
    {
        // Arrange
        var pool = new StringInternPool(100);

        // Act & Assert
        pool.InternTrigger("Start").Should().Be("Start");
        pool.InternTrigger("Stop").Should().Be("Stop");
        pool.InternTrigger("Pause").Should().Be("Pause");
        pool.InternTrigger("Resume").Should().Be("Resume");
        pool.InternTrigger("Complete").Should().Be("Complete");
        pool.InternTrigger("Cancel").Should().Be("Cancel");
        pool.InternTrigger("Retry").Should().Be("Retry");
        pool.InternTrigger("Timeout").Should().Be("Timeout");
        pool.InternTrigger("Error").Should().Be("Error");
        pool.InternTrigger("Submit").Should().Be("Submit");
        pool.InternTrigger("Approve").Should().Be("Approve");
        pool.InternTrigger("Reject").Should().Be("Reject");
        
        // All common triggers should return the same string literal instance
        pool.InternTrigger("Start").Should().BeSameAs("Start");
    }

    [Fact]
    public void InternTrigger_WithNull_ShouldReturnNull()
    {
        // Arrange
        var pool = new StringInternPool(100);

        // Act
        var result = pool.InternTrigger(null);

        // Assert
        result.Should().BeNull();
    }

    [Fact]
    public void InternTrigger_WithCustomTrigger_ShouldUseRegularIntern()
    {
        // Arrange
        var pool = new StringInternPool(100);
        var customTrigger = "CustomTrigger";

        // Act
        var result1 = pool.InternTrigger(customTrigger);
        var result2 = pool.InternTrigger(customTrigger);

        // Assert
        result1.Should().BeSameAs(result2);
        pool.Count.Should().Be(1);
    }

    [Fact]
    public void Clear_ShouldEmptyPool()
    {
        // Arrange
        var pool = new StringInternPool(100);
        pool.Intern("test1");
        pool.Intern("test2");
        pool.Count.Should().Be(2);

        // Act
        pool.Clear();

        // Assert
        pool.Count.Should().Be(0);
    }

    [Fact]
    public void GetStats_ShouldReturnCorrectStatistics()
    {
        // Arrange
        var pool = new StringInternPool(100);
        pool.Intern("test1");
        pool.Intern("test2");

        // Act
        var stats = pool.GetStats();

        // Assert
        stats.CurrentSize.Should().Be(2);
        stats.MaxSize.Should().Be(100);
        stats.UtilizationPercent.Should().Be(2.0); // 2/100 * 100
    }

    [Fact]
    public void StringInternPoolStats_Properties_ShouldWorkCorrectly()
    {
        // Act
        var stats = new StringInternPoolStats
        {
            CurrentSize = 50,
            MaxSize = 100,
            UtilizationPercent = 50.0
        };

        // Assert
        stats.CurrentSize.Should().Be(50);
        stats.MaxSize.Should().Be(100);
        stats.UtilizationPercent.Should().Be(50.0);
    }

    [Fact]
    public void Intern_ConcurrentAccess_ShouldBeThreadSafe()
    {
        // Arrange
        var pool = new StringInternPool(1000);
        var tasks = new System.Threading.Tasks.Task[10];
        var results = new string[1000];
        var exceptions = new System.Collections.Concurrent.ConcurrentBag<Exception>();

        // Act - Multiple threads interning the same strings
        for (int i = 0; i < 10; i++)
        {
            var taskIndex = i;
            tasks[i] = System.Threading.Tasks.Task.Run(() =>
            {
                try
                {
                    for (int j = 0; j < 100; j++)
                    {
                        var index = taskIndex * 100 + j;
                        results[index] = pool.Intern($"string{j % 10}"); // Reuse some strings
                    }
                }
                catch (Exception ex)
                {
                    exceptions.Add(ex);
                }
            });
        }

        System.Threading.Tasks.Task.WaitAll(tasks);

        // Assert
        exceptions.Should().BeEmpty();
        
        // Verify that same strings return same instances
        var string0Instances = results.Where(r => r == "string0").ToList();
        string0Instances.Should().NotBeEmpty();
        string0Instances.Should().OnlyContain(s => ReferenceEquals(s, string0Instances.First()));
    }

    [Fact]
    public void Intern_Performance_ShouldBeReasonable()
    {
        // Arrange
        var pool = new StringInternPool(10000);
        var testStrings = Enumerable.Range(0, 1000)
            .Select(i => $"test_string_{i}")
            .ToArray();

        // Act - Measure time for first pass (cache misses)
        var sw = System.Diagnostics.Stopwatch.StartNew();
        foreach (var str in testStrings)
        {
            pool.Intern(str);
        }
        var firstPassTime = sw.ElapsedMilliseconds;

        // Act - Measure time for second pass (cache hits)
        sw.Restart();
        foreach (var str in testStrings)
        {
            pool.Intern(str);
        }
        var secondPassTime = sw.ElapsedMilliseconds;

        // Assert - Second pass should be faster or same due to cache hits
        // If both are 0ms (very fast), that's acceptable
        if (firstPassTime > 0)
        {
            secondPassTime.Should().BeLessOrEqualTo(firstPassTime);
        }
        else
        {
            // Both passes were very fast (< 1ms), which is good
            secondPassTime.Should().BeLessOrEqualTo(1);
        }
        pool.Count.Should().Be(1000);
    }
}