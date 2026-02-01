using FluentAssertions;
using Orleans.StateMachineES.Persistence;

namespace Orleans.StateMachineES.Tests.Unit.Persistence;

public class InMemoryPersistenceTests
{
    private enum TestState
    {
        Initial,
        Processing,
        Completed,
        Failed
    }

    private enum TestTrigger
    {
        Start,
        Complete,
        Fail,
        Reset
    }

    #region Event Store Tests

    [Fact]
    public async Task AppendEvents_ShouldSucceed()
    {
        // Arrange
        var store = new InMemoryEventStore<TestState, TestTrigger>();
        var events = new List<StoredEvent<TestState, TestTrigger>>
        {
            new("stream-1", 0, TestState.Initial, TestState.Processing, TestTrigger.Start, DateTime.UtcNow)
        };

        // Act
        var result = await store.AppendEventsAsync("stream-1", events, ExpectedVersion.Any);

        // Assert
        result.Success.Should().BeTrue();
        result.NewVersion.Should().Be(1);
        result.EventCount.Should().Be(1);
    }

    [Fact]
    public async Task AppendEvents_WithExpectedVersion_ShouldEnforceConcurrency()
    {
        // Arrange
        var store = new InMemoryEventStore<TestState, TestTrigger>();
        var event1 = new StoredEvent<TestState, TestTrigger>("stream-1", 0, TestState.Initial, TestState.Processing, TestTrigger.Start, DateTime.UtcNow);
        await store.AppendEventsAsync("stream-1", new[] { event1 }, ExpectedVersion.Any);

        // Act - Try to append with wrong expected version
        var event2 = new StoredEvent<TestState, TestTrigger>("stream-1", 1, TestState.Processing, TestState.Completed, TestTrigger.Complete, DateTime.UtcNow);
        var result = await store.AppendEventsAsync("stream-1", new[] { event2 }, 5);

        // Assert
        result.Success.Should().BeFalse();
        result.ErrorMessage.Should().Contain("Expected version");
    }

    [Fact]
    public async Task AppendEvents_WithNoStreamExpected_ShouldFailIfExists()
    {
        // Arrange
        var store = new InMemoryEventStore<TestState, TestTrigger>();
        var event1 = new StoredEvent<TestState, TestTrigger>("stream-1", 0, TestState.Initial, TestState.Processing, TestTrigger.Start, DateTime.UtcNow);
        await store.AppendEventsAsync("stream-1", new[] { event1 }, ExpectedVersion.Any);

        // Act
        var event2 = new StoredEvent<TestState, TestTrigger>("stream-1", 1, TestState.Processing, TestState.Completed, TestTrigger.Complete, DateTime.UtcNow);
        var result = await store.AppendEventsAsync("stream-1", new[] { event2 }, ExpectedVersion.NoStream);

        // Assert
        result.Success.Should().BeFalse();
        result.ErrorMessage.Should().Contain("already exists");
    }

    [Fact]
    public async Task ReadEvents_ShouldReturnEventsInOrder()
    {
        // Arrange
        var store = new InMemoryEventStore<TestState, TestTrigger>();
        var events = new List<StoredEvent<TestState, TestTrigger>>
        {
            new("stream-1", 0, TestState.Initial, TestState.Processing, TestTrigger.Start, DateTime.UtcNow.AddMinutes(-2)),
            new("stream-1", 1, TestState.Processing, TestState.Completed, TestTrigger.Complete, DateTime.UtcNow.AddMinutes(-1)),
            new("stream-1", 2, TestState.Completed, TestState.Initial, TestTrigger.Reset, DateTime.UtcNow)
        };
        await store.AppendEventsAsync("stream-1", events, ExpectedVersion.Any);

        // Act
        var result = await store.ReadEventsAsync("stream-1", 0, 10);

        // Assert
        result.Should().HaveCount(3);
        result[0].FromState.Should().Be(TestState.Initial);
        result[1].FromState.Should().Be(TestState.Processing);
        result[2].FromState.Should().Be(TestState.Completed);
    }

    [Fact]
    public async Task ReadEvents_WithPaging_ShouldReturnCorrectSubset()
    {
        // Arrange
        var store = new InMemoryEventStore<TestState, TestTrigger>();
        var events = new List<StoredEvent<TestState, TestTrigger>>();
        for (int i = 0; i < 10; i++)
        {
            events.Add(new StoredEvent<TestState, TestTrigger>(
                "stream-1", i, TestState.Initial, TestState.Processing, TestTrigger.Start, DateTime.UtcNow.AddMinutes(-i)));
        }
        await store.AppendEventsAsync("stream-1", events, ExpectedVersion.Any);

        // Act
        var page1 = await store.ReadEventsAsync("stream-1", 0, 3);
        var page2 = await store.ReadEventsAsync("stream-1", 3, 3);

        // Assert
        page1.Should().HaveCount(3);
        page2.Should().HaveCount(3);
        page1[0].SequenceNumber.Should().Be(0);
        page2[0].SequenceNumber.Should().Be(3);
    }

    [Fact]
    public async Task ReadEventsBackward_ShouldReturnEventsInReverseOrder()
    {
        // Arrange
        var store = new InMemoryEventStore<TestState, TestTrigger>();
        var events = new List<StoredEvent<TestState, TestTrigger>>
        {
            new("stream-1", 0, TestState.Initial, TestState.Processing, TestTrigger.Start, DateTime.UtcNow.AddMinutes(-2)),
            new("stream-1", 1, TestState.Processing, TestState.Completed, TestTrigger.Complete, DateTime.UtcNow.AddMinutes(-1)),
            new("stream-1", 2, TestState.Completed, TestState.Initial, TestTrigger.Reset, DateTime.UtcNow)
        };
        await store.AppendEventsAsync("stream-1", events, ExpectedVersion.Any);

        // Act
        var result = await store.ReadEventsBackwardAsync("stream-1", 2, 3);

        // Assert
        result.Should().HaveCount(3);
        result[0].SequenceNumber.Should().Be(2);
        result[1].SequenceNumber.Should().Be(1);
        result[2].SequenceNumber.Should().Be(0);
    }

    [Fact]
    public async Task GetStreamVersion_ShouldReturnCorrectVersion()
    {
        // Arrange
        var store = new InMemoryEventStore<TestState, TestTrigger>();
        var events = new List<StoredEvent<TestState, TestTrigger>>
        {
            new("stream-1", 0, TestState.Initial, TestState.Processing, TestTrigger.Start, DateTime.UtcNow),
            new("stream-1", 1, TestState.Processing, TestState.Completed, TestTrigger.Complete, DateTime.UtcNow)
        };
        await store.AppendEventsAsync("stream-1", events, ExpectedVersion.Any);

        // Act
        var version = await store.GetStreamVersionAsync("stream-1");

        // Assert
        version.Should().Be(2);
    }

    [Fact]
    public async Task GetStreamVersion_NonExistentStream_ShouldReturnMinusOne()
    {
        // Arrange
        var store = new InMemoryEventStore<TestState, TestTrigger>();

        // Act
        var version = await store.GetStreamVersionAsync("non-existent");

        // Assert
        version.Should().Be(-1);
    }

    [Fact]
    public async Task DeleteStream_ShouldRemoveStream()
    {
        // Arrange
        var store = new InMemoryEventStore<TestState, TestTrigger>();
        var events = new List<StoredEvent<TestState, TestTrigger>>
        {
            new("stream-1", 0, TestState.Initial, TestState.Processing, TestTrigger.Start, DateTime.UtcNow)
        };
        await store.AppendEventsAsync("stream-1", events, ExpectedVersion.Any);

        // Act
        var deleted = await store.DeleteStreamAsync("stream-1", ExpectedVersion.Any);

        // Assert
        deleted.Should().BeTrue();
        (await store.StreamExistsAsync("stream-1")).Should().BeFalse();
    }

    [Fact]
    public async Task Subscribe_ShouldReceiveNewEvents()
    {
        // Arrange
        var store = new InMemoryEventStore<TestState, TestTrigger>();
        var receivedEvents = new List<StoredEvent<TestState, TestTrigger>>();

        await using var subscription = await store.SubscribeAsync(
            "stream-1", 0, evt =>
            {
                receivedEvents.Add(evt);
                return Task.CompletedTask;
            });

        // Act
        var events = new List<StoredEvent<TestState, TestTrigger>>
        {
            new("stream-1", 0, TestState.Initial, TestState.Processing, TestTrigger.Start, DateTime.UtcNow)
        };
        await store.AppendEventsAsync("stream-1", events, ExpectedVersion.Any);

        // Assert
        receivedEvents.Should().HaveCount(1);
        receivedEvents[0].Trigger.Should().Be(TestTrigger.Start);
    }

    #endregion

    #region Snapshot Store Tests

    [Fact]
    public async Task SaveSnapshot_ShouldSucceed()
    {
        // Arrange
        var store = new InMemorySnapshotStore<TestState>();
        var snapshot = new SnapshotInfo<TestState>("stream-1", TestState.Processing, 5, 5);

        // Act
        var result = await store.SaveSnapshotAsync(snapshot);

        // Assert
        result.Should().BeTrue();
    }

    [Fact]
    public async Task LoadSnapshot_ShouldReturnLatest()
    {
        // Arrange
        var store = new InMemorySnapshotStore<TestState>();
        await store.SaveSnapshotAsync(new SnapshotInfo<TestState>("stream-1", TestState.Initial, 0, 0));
        await store.SaveSnapshotAsync(new SnapshotInfo<TestState>("stream-1", TestState.Processing, 5, 5));
        await store.SaveSnapshotAsync(new SnapshotInfo<TestState>("stream-1", TestState.Completed, 10, 10));

        // Act
        var snapshot = await store.LoadSnapshotAsync("stream-1");

        // Assert
        snapshot.Should().NotBeNull();
        snapshot!.State.Should().Be(TestState.Completed);
        snapshot.Version.Should().Be(10);
    }

    [Fact]
    public async Task LoadSnapshot_NonExistent_ShouldReturnNull()
    {
        // Arrange
        var store = new InMemorySnapshotStore<TestState>();

        // Act
        var snapshot = await store.LoadSnapshotAsync("non-existent");

        // Assert
        snapshot.Should().BeNull();
    }

    [Fact]
    public async Task LoadSnapshotAtVersion_ShouldReturnCorrectSnapshot()
    {
        // Arrange
        var store = new InMemorySnapshotStore<TestState>();
        await store.SaveSnapshotAsync(new SnapshotInfo<TestState>("stream-1", TestState.Initial, 0, 0));
        await store.SaveSnapshotAsync(new SnapshotInfo<TestState>("stream-1", TestState.Processing, 5, 5));
        await store.SaveSnapshotAsync(new SnapshotInfo<TestState>("stream-1", TestState.Completed, 10, 10));

        // Act
        var snapshot = await store.LoadSnapshotAtVersionAsync("stream-1", 7);

        // Assert
        snapshot.Should().NotBeNull();
        snapshot!.State.Should().Be(TestState.Processing);
        snapshot.Version.Should().Be(5);
    }

    [Fact]
    public async Task SaveSnapshot_ShouldPruneOldSnapshots()
    {
        // Arrange
        var store = new InMemorySnapshotStore<TestState>();
        var options = new SnapshotSaveOptions { MaxSnapshotsToRetain = 2 };

        // Act
        for (int i = 0; i < 5; i++)
        {
            await store.SaveSnapshotAsync(
                new SnapshotInfo<TestState>("stream-1", TestState.Processing, i, i),
                options);
        }

        // Assert
        var snapshots = await store.GetAllSnapshotsAsync("stream-1");
        snapshots.Should().HaveCount(2);
    }

    [Fact]
    public async Task DeleteSnapshot_ShouldRemoveSpecificSnapshot()
    {
        // Arrange
        var store = new InMemorySnapshotStore<TestState>();
        var snapshot1 = new SnapshotInfo<TestState>("stream-1", TestState.Initial, 0, 0);
        var snapshot2 = new SnapshotInfo<TestState>("stream-1", TestState.Processing, 5, 5);
        await store.SaveSnapshotAsync(snapshot1);
        await store.SaveSnapshotAsync(snapshot2);

        // Act
        var deleted = await store.DeleteSnapshotAsync("stream-1", snapshot1.SnapshotId);

        // Assert
        deleted.Should().BeTrue();
        var remaining = await store.GetAllSnapshotsAsync("stream-1");
        remaining.Should().HaveCount(1);
    }

    #endregion

    #region Combined Persistence Tests

    [Fact]
    public async Task LoadState_ShouldReplayEvents()
    {
        // Arrange
        var persistence = new InMemoryStateMachinePersistence<TestState, TestTrigger>();
        var events = new List<StoredEvent<TestState, TestTrigger>>
        {
            new("stream-1", 0, TestState.Initial, TestState.Processing, TestTrigger.Start, DateTime.UtcNow.AddMinutes(-2)),
            new("stream-1", 1, TestState.Processing, TestState.Completed, TestTrigger.Complete, DateTime.UtcNow)
        };
        await persistence.AppendEventsAsync("stream-1", events, ExpectedVersion.Any);

        // Act
        var state = await persistence.LoadStateAsync("stream-1");

        // Assert
        state.StreamExists.Should().BeTrue();
        state.CurrentState.Should().Be(TestState.Completed);
        state.Version.Should().Be(2);
        state.EventsReplayedCount.Should().Be(2);
    }

    [Fact]
    public async Task LoadState_WithSnapshot_ShouldUseSnapshotAndReplayRemainingEvents()
    {
        // Arrange
        var options = new PersistenceOptions { EnableSnapshots = true };
        var persistence = new InMemoryStateMachinePersistence<TestState, TestTrigger>(options);

        // Add events
        var events = new List<StoredEvent<TestState, TestTrigger>>
        {
            new("stream-1", 0, TestState.Initial, TestState.Processing, TestTrigger.Start, DateTime.UtcNow.AddMinutes(-5)),
            new("stream-1", 1, TestState.Processing, TestState.Processing, TestTrigger.Start, DateTime.UtcNow.AddMinutes(-4)),
            new("stream-1", 2, TestState.Processing, TestState.Processing, TestTrigger.Start, DateTime.UtcNow.AddMinutes(-3)),
            new("stream-1", 3, TestState.Processing, TestState.Completed, TestTrigger.Complete, DateTime.UtcNow.AddMinutes(-2)),
            new("stream-1", 4, TestState.Completed, TestState.Initial, TestTrigger.Reset, DateTime.UtcNow)
        };
        await persistence.AppendEventsAsync("stream-1", events, ExpectedVersion.Any);

        // Save snapshot at version 3
        var snapshot = new SnapshotInfo<TestState>("stream-1", TestState.Completed, 3, 3);
        await persistence.SaveSnapshotAsync(snapshot);

        // Act
        var state = await persistence.LoadStateAsync("stream-1");

        // Assert
        state.StreamExists.Should().BeTrue();
        state.CurrentState.Should().Be(TestState.Initial);
        state.LoadedFromSnapshot.Should().BeTrue();
        state.SnapshotVersion.Should().Be(3);
        state.EventsReplayedCount.Should().Be(1); // Only event 4 after snapshot
    }

    [Fact]
    public async Task LoadStateAtTime_ShouldReturnStateAtPointInTime()
    {
        // Arrange
        var persistence = new InMemoryStateMachinePersistence<TestState, TestTrigger>();
        var baseTime = DateTime.UtcNow.AddHours(-1);
        var events = new List<StoredEvent<TestState, TestTrigger>>
        {
            new("stream-1", 0, TestState.Initial, TestState.Processing, TestTrigger.Start, baseTime),
            new("stream-1", 1, TestState.Processing, TestState.Completed, TestTrigger.Complete, baseTime.AddMinutes(30)),
            new("stream-1", 2, TestState.Completed, TestState.Failed, TestTrigger.Fail, baseTime.AddMinutes(45))
        };
        await persistence.AppendEventsAsync("stream-1", events, ExpectedVersion.Any);

        // Act - Get state at 35 minutes (after Complete but before Fail)
        var state = await persistence.LoadStateAtTimeAsync("stream-1", baseTime.AddMinutes(35));

        // Assert
        state.CurrentState.Should().Be(TestState.Completed);
    }

    [Fact]
    public async Task GetHistory_ShouldReturnCompleteHistory()
    {
        // Arrange
        var persistence = new InMemoryStateMachinePersistence<TestState, TestTrigger>();
        var events = new List<StoredEvent<TestState, TestTrigger>>
        {
            new("stream-1", 0, TestState.Initial, TestState.Processing, TestTrigger.Start, DateTime.UtcNow.AddMinutes(-2)),
            new("stream-1", 1, TestState.Processing, TestState.Completed, TestTrigger.Complete, DateTime.UtcNow.AddMinutes(-1)),
            new("stream-1", 2, TestState.Completed, TestState.Initial, TestTrigger.Reset, DateTime.UtcNow)
        };
        await persistence.AppendEventsAsync("stream-1", events, ExpectedVersion.Any);

        // Act
        var history = await persistence.GetHistoryAsync("stream-1");

        // Assert
        history.StreamId.Should().Be("stream-1");
        history.InitialState.Should().Be(TestState.Initial);
        history.CurrentState.Should().Be(TestState.Initial);
        history.TransitionCount.Should().Be(3);
        history.DistinctStates.Should().HaveCount(3);
        history.DistinctTriggers.Should().HaveCount(3);
    }

    [Fact]
    public async Task GetHistoryPaged_ShouldReturnPagedResults()
    {
        // Arrange
        var persistence = new InMemoryStateMachinePersistence<TestState, TestTrigger>();
        var events = new List<StoredEvent<TestState, TestTrigger>>();
        for (int i = 0; i < 25; i++)
        {
            events.Add(new StoredEvent<TestState, TestTrigger>(
                "stream-1", i, TestState.Initial, TestState.Processing, TestTrigger.Start, DateTime.UtcNow.AddMinutes(-i)));
        }
        await persistence.AppendEventsAsync("stream-1", events, ExpectedVersion.Any);

        // Act
        var page1 = await persistence.GetHistoryPagedAsync("stream-1", 0, 10);
        var page2 = await persistence.GetHistoryPagedAsync("stream-1", 1, 10);
        var page3 = await persistence.GetHistoryPagedAsync("stream-1", 2, 10);

        // Assert
        page1.Events.Should().HaveCount(10);
        page1.Page.Should().Be(0);
        page1.TotalEvents.Should().Be(25);
        page1.TotalPages.Should().Be(3);
        page1.HasNextPage.Should().BeTrue();
        page1.HasPreviousPage.Should().BeFalse();

        page2.Events.Should().HaveCount(10);
        page2.HasNextPage.Should().BeTrue();
        page2.HasPreviousPage.Should().BeTrue();

        page3.Events.Should().HaveCount(5);
        page3.HasNextPage.Should().BeFalse();
        page3.HasPreviousPage.Should().BeTrue();
    }

    [Fact]
    public async Task CompactStream_ShouldCreateSnapshot()
    {
        // Arrange
        var options = new PersistenceOptions { EnableSnapshots = true };
        var persistence = new InMemoryStateMachinePersistence<TestState, TestTrigger>(options);
        var events = new List<StoredEvent<TestState, TestTrigger>>
        {
            new("stream-1", 0, TestState.Initial, TestState.Processing, TestTrigger.Start, DateTime.UtcNow.AddMinutes(-2)),
            new("stream-1", 1, TestState.Processing, TestState.Completed, TestTrigger.Complete, DateTime.UtcNow)
        };
        await persistence.AppendEventsAsync("stream-1", events, ExpectedVersion.Any);

        // Act
        var result = await persistence.CompactStreamAsync("stream-1");

        // Assert
        result.Success.Should().BeTrue();
        result.SnapshotVersion.Should().Be(2);

        var hasSnapshot = await persistence.HasSnapshotAsync("stream-1");
        hasSnapshot.Should().BeTrue();
    }

    [Fact]
    public async Task IsHealthy_ShouldReturnTrue()
    {
        // Arrange
        var persistence = new InMemoryStateMachinePersistence<TestState, TestTrigger>();

        // Act
        var isHealthy = await persistence.IsHealthyAsync();

        // Assert
        isHealthy.Should().BeTrue();
    }

    [Fact]
    public async Task Clear_ShouldRemoveAllData()
    {
        // Arrange
        var persistence = new InMemoryStateMachinePersistence<TestState, TestTrigger>();
        var events = new List<StoredEvent<TestState, TestTrigger>>
        {
            new("stream-1", 0, TestState.Initial, TestState.Processing, TestTrigger.Start, DateTime.UtcNow)
        };
        await persistence.AppendEventsAsync("stream-1", events, ExpectedVersion.Any);
        await persistence.SaveSnapshotAsync(new SnapshotInfo<TestState>("stream-1", TestState.Processing, 1, 1));

        // Act
        await persistence.ClearAsync();

        // Assert
        (await persistence.StreamExistsAsync("stream-1")).Should().BeFalse();
        (await persistence.HasSnapshotAsync("stream-1")).Should().BeFalse();
    }

    #endregion

    #region Exception Tests

    [Fact]
    public void ConcurrencyException_ShouldContainVersionInfo()
    {
        // Act
        var exception = new ConcurrencyException("stream-1", 5, 10);

        // Assert
        exception.ExpectedVersion.Should().Be(5);
        exception.ActualVersion.Should().Be(10);
        exception.Message.Should().Contain("stream-1");
        exception.Message.Should().Contain("5");
        exception.Message.Should().Contain("10");
    }

    [Fact]
    public void StreamNotFoundException_ShouldContainStreamId()
    {
        // Act
        var exception = new StreamNotFoundException("test-stream");

        // Assert
        exception.StreamId.Should().Be("test-stream");
        exception.Message.Should().Contain("test-stream");
    }

    [Fact]
    public async Task LoadSnapshot_WithRequireSnapshot_ShouldThrowIfNotFound()
    {
        // Arrange
        var store = new InMemorySnapshotStore<TestState>();
        var options = new SnapshotLoadOptions { RequireSnapshot = true };

        // Act & Assert
        await Assert.ThrowsAsync<SnapshotStoreException>(
            () => store.LoadSnapshotAsync("non-existent", options));
    }

    #endregion
}
