using FluentAssertions;
using Orleans.StateMachineES.Persistence;
using Orleans.StateMachineES.Queries;
using Xunit;

namespace Orleans.StateMachineES.Tests.Unit.Queries;

public class StateHistoryQueryTests
{
    private enum TestState
    {
        Initial,
        Processing,
        Completed,
        Failed,
        Cancelled
    }

    private enum TestTrigger
    {
        Start,
        Complete,
        Fail,
        Cancel,
        Reset
    }

    private readonly InMemoryStateMachinePersistence<TestState, TestTrigger> _persistence;
    private readonly string _streamId = "test-stream";
    private readonly DateTime _baseTime;

    public StateHistoryQueryTests()
    {
        _persistence = new InMemoryStateMachinePersistence<TestState, TestTrigger>();
        _baseTime = DateTime.UtcNow.AddDays(-7);
    }

    private async Task SeedEventsAsync()
    {
        var baseTime = _baseTime;
        var events = new List<StoredEvent<TestState, TestTrigger>>
        {
            new(_streamId, 0, TestState.Initial, TestState.Processing, TestTrigger.Start, baseTime,
                correlationId: "corr-1"),
            new(_streamId, 1, TestState.Processing, TestState.Processing, TestTrigger.Start, baseTime.AddHours(1),
                correlationId: "corr-1"),
            new(_streamId, 2, TestState.Processing, TestState.Completed, TestTrigger.Complete, baseTime.AddHours(2),
                correlationId: "corr-1"),
            new(_streamId, 3, TestState.Completed, TestState.Initial, TestTrigger.Reset, baseTime.AddDays(1),
                correlationId: "corr-2"),
            new(_streamId, 4, TestState.Initial, TestState.Processing, TestTrigger.Start, baseTime.AddDays(1).AddHours(1),
                correlationId: "corr-2"),
            new(_streamId, 5, TestState.Processing, TestState.Failed, TestTrigger.Fail, baseTime.AddDays(2),
                correlationId: "corr-2"),
            new(_streamId, 6, TestState.Failed, TestState.Initial, TestTrigger.Reset, baseTime.AddDays(3),
                correlationId: "corr-3"),
            new(_streamId, 7, TestState.Initial, TestState.Cancelled, TestTrigger.Cancel, baseTime.AddDays(4),
                correlationId: "corr-3")
        };

        await _persistence.AppendEventsAsync(_streamId, events, ExpectedVersion.Any);
    }

    #region Basic Query Tests

    [Fact]
    public async Task Execute_ShouldReturnAllEvents()
    {
        // Arrange
        await SeedEventsAsync();

        // Act
        var result = await _persistence.Query(_streamId).ExecuteAsync();

        // Assert
        result.Should().HaveCount(8);
    }

    [Fact]
    public async Task FromState_ShouldFilterBySourceState()
    {
        // Arrange
        await SeedEventsAsync();

        // Act
        var result = await _persistence.Query(_streamId)
            .FromState(TestState.Processing)
            .ExecuteAsync();

        // Assert
        result.Should().HaveCount(3);
        result.Should().OnlyContain(e => e.FromState == TestState.Processing);
    }

    [Fact]
    public async Task ToState_ShouldFilterByTargetState()
    {
        // Arrange
        await SeedEventsAsync();

        // Act
        var result = await _persistence.Query(_streamId)
            .ToState(TestState.Processing)
            .ExecuteAsync();

        // Assert
        result.Should().HaveCount(3);
        result.Should().OnlyContain(e => e.ToState == TestState.Processing);
    }

    [Fact]
    public async Task WithTrigger_ShouldFilterByTrigger()
    {
        // Arrange
        await SeedEventsAsync();

        // Act
        var result = await _persistence.Query(_streamId)
            .WithTrigger(TestTrigger.Start)
            .ExecuteAsync();

        // Assert
        result.Should().HaveCount(3);
        result.Should().OnlyContain(e => e.Trigger == TestTrigger.Start);
    }

    [Fact]
    public async Task WithTriggers_ShouldFilterByMultipleTriggers()
    {
        // Arrange
        await SeedEventsAsync();

        // Act
        var result = await _persistence.Query(_streamId)
            .WithTriggers(TestTrigger.Complete, TestTrigger.Fail)
            .ExecuteAsync();

        // Assert
        result.Should().HaveCount(2);
        result.Should().OnlyContain(e => e.Trigger == TestTrigger.Complete || e.Trigger == TestTrigger.Fail);
    }

    [Fact]
    public async Task WithCorrelationId_ShouldFilterByCorrelation()
    {
        // Arrange
        await SeedEventsAsync();

        // Act
        var result = await _persistence.Query(_streamId)
            .WithCorrelationId("corr-1")
            .ExecuteAsync();

        // Assert
        result.Should().HaveCount(3);
        result.Should().OnlyContain(e => e.CorrelationId == "corr-1");
    }

    #endregion

    #region Time Range Tests

    [Fact]
    public async Task InTimeRange_ShouldFilterByTimeRange()
    {
        // Arrange
        await SeedEventsAsync();

        // Act
        var result = await _persistence.Query(_streamId)
            .InTimeRange(_baseTime, _baseTime.AddDays(1))
            .ExecuteAsync();

        // Assert
        result.Should().HaveCount(4);
    }

    [Fact]
    public async Task After_ShouldFilterEventsAfterTime()
    {
        // Arrange
        await SeedEventsAsync();
        // Use _baseTime + 2 days (equals now - 5 days when _baseTime = now - 7 days)
        var afterTime = _baseTime.AddDays(2);

        // Act
        var result = await _persistence.Query(_streamId)
            .After(afterTime)
            .ExecuteAsync();

        // Assert
        // After uses > (strictly after), so events at exactly _baseTime + 2 days are excluded
        // Events 6 (_baseTime + 3d) and 7 (_baseTime + 4d) are included
        result.Should().HaveCount(2);
    }

    [Fact]
    public async Task Before_ShouldFilterEventsBeforeTime()
    {
        // Arrange
        await SeedEventsAsync();
        // Use _baseTime + 2 days (equals now - 5 days when _baseTime = now - 7 days)
        var beforeTime = _baseTime.AddDays(2);

        // Act
        var result = await _persistence.Query(_streamId)
            .Before(beforeTime)
            .ExecuteAsync();

        // Assert
        // Events 0-4 are before _baseTime + 2 days (events 0,1,2,3,4 = 5 events)
        result.Should().HaveCount(5);
    }

    #endregion

    #region Version Range Tests

    [Fact]
    public async Task InVersionRange_ShouldFilterByVersion()
    {
        // Arrange
        await SeedEventsAsync();

        // Act
        var result = await _persistence.Query(_streamId)
            .InVersionRange(2, 5)
            .ExecuteAsync();

        // Assert
        result.Should().HaveCount(4);
        result.Should().OnlyContain(e => e.SequenceNumber >= 2 && e.SequenceNumber <= 5);
    }

    #endregion

    #region Ordering Tests

    [Fact]
    public async Task OrderByTime_ShouldSortAscending()
    {
        // Arrange
        await SeedEventsAsync();

        // Act
        var result = await _persistence.Query(_streamId)
            .OrderByTime()
            .ExecuteAsync();

        // Assert
        result.Should().BeInAscendingOrder(e => e.Timestamp);
    }

    [Fact]
    public async Task OrderByTimeDescending_ShouldSortDescending()
    {
        // Arrange
        await SeedEventsAsync();

        // Act
        var result = await _persistence.Query(_streamId)
            .OrderByTimeDescending()
            .ExecuteAsync();

        // Assert
        result.Should().BeInDescendingOrder(e => e.Timestamp);
    }

    #endregion

    #region Pagination Tests

    [Fact]
    public async Task Take_ShouldLimitResults()
    {
        // Arrange
        await SeedEventsAsync();

        // Act
        var result = await _persistence.Query(_streamId)
            .Take(3)
            .ExecuteAsync();

        // Assert
        result.Should().HaveCount(3);
    }

    [Fact]
    public async Task Skip_ShouldOffsetResults()
    {
        // Arrange
        await SeedEventsAsync();

        // Act
        var result = await _persistence.Query(_streamId)
            .OrderByTime()
            .Skip(2)
            .Take(3)
            .ExecuteAsync();

        // Assert
        result.Should().HaveCount(3);
        result[0].SequenceNumber.Should().Be(2);
    }

    #endregion

    #region Aggregate Tests

    [Fact]
    public async Task FirstOrDefault_ShouldReturnFirstMatch()
    {
        // Arrange
        await SeedEventsAsync();

        // Act
        var result = await _persistence.Query(_streamId)
            .OrderByTime()
            .FirstOrDefaultAsync();

        // Assert
        result.Should().NotBeNull();
        result!.SequenceNumber.Should().Be(0);
    }

    [Fact]
    public async Task FirstOrDefault_NoMatch_ShouldReturnNull()
    {
        // Arrange
        await SeedEventsAsync();

        // Act
        var result = await _persistence.Query(_streamId)
            .WithTrigger((TestTrigger)999)
            .FirstOrDefaultAsync();

        // Assert
        result.Should().BeNull();
    }

    [Fact]
    public async Task Count_ShouldReturnMatchCount()
    {
        // Arrange
        await SeedEventsAsync();

        // Act
        var count = await _persistence.Query(_streamId)
            .FromState(TestState.Processing)
            .CountAsync();

        // Assert
        count.Should().Be(3);
    }

    [Fact]
    public async Task Any_WithMatches_ShouldReturnTrue()
    {
        // Arrange
        await SeedEventsAsync();

        // Act
        var any = await _persistence.Query(_streamId)
            .ToState(TestState.Failed)
            .AnyAsync();

        // Assert
        any.Should().BeTrue();
    }

    [Fact]
    public async Task Any_NoMatches_ShouldReturnFalse()
    {
        // Arrange
        await SeedEventsAsync();

        // Act
        var any = await _persistence.Query(_streamId)
            .WithTrigger((TestTrigger)999)
            .AnyAsync();

        // Assert
        any.Should().BeFalse();
    }

    #endregion

    #region Group By Tests

    [Fact]
    public async Task GroupByState_ShouldReturnStateStatistics()
    {
        // Arrange
        await SeedEventsAsync();

        // Act
        var stats = await _persistence.Query(_streamId).GroupByStateAsync();

        // Assert
        stats.Should().ContainKey(TestState.Processing);
        stats[TestState.Processing].EntryCount.Should().BeGreaterThan(0);
        stats[TestState.Processing].ExitCount.Should().BeGreaterThan(0);
    }

    [Fact]
    public async Task GroupByTrigger_ShouldReturnTriggerStatistics()
    {
        // Arrange
        await SeedEventsAsync();

        // Act
        var stats = await _persistence.Query(_streamId).GroupByTriggerAsync();

        // Assert
        stats.Should().ContainKey(TestTrigger.Start);
        stats[TestTrigger.Start].FireCount.Should().Be(3);
        stats.Should().ContainKey(TestTrigger.Reset);
        stats[TestTrigger.Reset].FireCount.Should().Be(2);
    }

    [Fact]
    public async Task GroupByTime_Day_ShouldGroupByDay()
    {
        // Arrange
        await SeedEventsAsync();

        // Act
        var stats = await _persistence.Query(_streamId)
            .GroupByTimeAsync(TimePeriodType.Day);

        // Assert
        stats.Should().NotBeEmpty();
        stats.Should().BeInAscendingOrder(s => s.PeriodStart);
    }

    [Fact]
    public async Task GroupByTime_Hour_ShouldGroupByHour()
    {
        // Arrange
        await SeedEventsAsync();

        // Act
        var stats = await _persistence.Query(_streamId)
            .GroupByTimeAsync(TimePeriodType.Hour);

        // Assert
        stats.Should().NotBeEmpty();
        foreach (var period in stats)
        {
            (period.PeriodEnd - period.PeriodStart).Should().BeLessThan(TimeSpan.FromHours(2));
        }
    }

    #endregion

    #region Extension Method Tests

    [Fact]
    public async Task MostRecent_ShouldReturnMostRecentEvents()
    {
        // Arrange
        await SeedEventsAsync();

        // Act
        var result = await _persistence.Query(_streamId)
            .MostRecent(3)
            .ExecuteAsync();

        // Assert
        result.Should().HaveCount(3);
        result.Should().BeInDescendingOrder(e => e.Timestamp);
    }

    [Fact]
    public async Task LastDays_ShouldFilterRecentEvents()
    {
        // Arrange
        await SeedEventsAsync();

        // Act
        var result = await _persistence.Query(_streamId)
            .LastDays(5)
            .ExecuteAsync();

        // Assert
        // LastDays(5) uses After(now - 5 days) which means > (strictly after)
        // Events 6 (-4 days) and 7 (-3 days) are included
        result.Should().HaveCount(2);
    }

    #endregion

    #region Complex Query Tests

    [Fact]
    public async Task CombinedFilters_ShouldApplyAll()
    {
        // Arrange
        await SeedEventsAsync();

        // Act
        var result = await _persistence.Query(_streamId)
            .FromState(TestState.Processing)
            .WithTrigger(TestTrigger.Complete)
            .InTimeRange(_baseTime, _baseTime.AddDays(2))
            .ExecuteAsync();

        // Assert
        result.Should().HaveCount(1);
        result[0].FromState.Should().Be(TestState.Processing);
        result[0].Trigger.Should().Be(TestTrigger.Complete);
    }

    [Fact]
    public async Task WithMetadata_ShouldFilterByMetadata()
    {
        // Arrange
        var events = new List<StoredEvent<TestState, TestTrigger>>
        {
            new(_streamId, 0, TestState.Initial, TestState.Processing, TestTrigger.Start, DateTime.UtcNow,
                metadata: new Dictionary<string, object> { ["priority"] = "high" }),
            new(_streamId, 1, TestState.Processing, TestState.Completed, TestTrigger.Complete, DateTime.UtcNow,
                metadata: new Dictionary<string, object> { ["priority"] = "low" })
        };
        await _persistence.AppendEventsAsync(_streamId, events, ExpectedVersion.Any);

        // Act
        var result = await _persistence.Query(_streamId)
            .WithMetadata(m => m != null && m.TryGetValue("priority", out var p) && p.ToString() == "high")
            .ExecuteAsync();

        // Assert
        result.Should().HaveCount(1);
        result[0].Metadata!["priority"].Should().Be("high");
    }

    #endregion
}
