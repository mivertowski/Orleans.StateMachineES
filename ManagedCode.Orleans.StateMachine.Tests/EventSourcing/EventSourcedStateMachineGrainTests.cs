using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using FluentAssertions;
using ivlt.Orleans.StateMachineES.EventSourcing;
using ivlt.Orleans.StateMachineES.EventSourcing.Configuration;
using ivlt.Orleans.StateMachineES.EventSourcing.Events;
using ivlt.Orleans.StateMachineES.EventSourcing.Exceptions;
using ivlt.Orleans.StateMachineES.Tests.Cluster;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Orleans;
using Orleans.EventSourcing;
using Orleans.Providers;
using Orleans.TestingHost;
using Stateless;
using Xunit;
using Xunit.Abstractions;

namespace ivlt.Orleans.StateMachineES.Tests.EventSourcing;

[Collection(nameof(TestClusterApplication))]
public class EventSourcedStateMachineGrainTests
{
    private readonly ITestOutputHelper _outputHelper;
    private readonly TestClusterApplication _testApp;

    public EventSourcedStateMachineGrainTests(TestClusterApplication testApp, ITestOutputHelper outputHelper)
    {
        _testApp = testApp;
        _outputHelper = outputHelper;
    }

    [Fact]
    public async Task EventSourcedGrain_ShouldTransitionStatesAndPersistEvents()
    {
        // Arrange
        var grain = _testApp.Cluster.Client.GetGrain<ITestEventSourcedGrain>("test-es-1");

        // Act - Initial state
        var initialState = await grain.GetStateAsync();
        initialState.Should().Be(DoorState.Closed);

        // Act - Open door
        await grain.OpenAsync();
        var openState = await grain.GetStateAsync();
        openState.Should().Be(DoorState.Open);

        // Act - Close door
        await grain.CloseAsync();
        var closedState = await grain.GetStateAsync();
        closedState.Should().Be(DoorState.Closed);

        // Act - Lock door
        await grain.LockAsync("secret-123");
        var lockedState = await grain.GetStateAsync();
        lockedState.Should().Be(DoorState.Locked);

        // Assert - Check event count
        var eventCount = await grain.GetTransitionCountAsync();
        eventCount.Should().Be(3); // Open, Close, Lock transitions
    }

    [Fact]
    public async Task EventSourcedGrain_ShouldEnforceIdempotency()
    {
        // Arrange
        var grain = _testApp.Cluster.Client.GetGrain<ITestEventSourcedGrain>("test-es-2");
        
        // Act - Open door multiple times (should be idempotent)
        await grain.OpenAsync();
        await grain.OpenAsync(); // Should be ignored due to deduplication
        await grain.OpenAsync(); // Should be ignored due to deduplication

        // Assert
        var state = await grain.GetStateAsync();
        state.Should().Be(DoorState.Open);
        
        var eventCount = await grain.GetTransitionCountAsync();
        eventCount.Should().Be(1); // Only one transition should be recorded
    }

    [Fact]
    public async Task EventSourcedGrain_ShouldRestoreStateFromEvents()
    {
        // Arrange
        var grainId = "test-es-3";
        var grain1 = _testApp.Cluster.Client.GetGrain<ITestEventSourcedGrain>(grainId);

        // Act - Perform transitions
        await grain1.OpenAsync();
        await grain1.CloseAsync();
        await grain1.LockAsync("password");

        // Force grain deactivation (simulate restart)
        await grain1.DeactivateAsync();
        await Task.Delay(100); // Give time for deactivation

        // Get grain again (will replay events)
        var grain2 = _testApp.Cluster.Client.GetGrain<ITestEventSourcedGrain>(grainId);
        
        // Assert - State should be restored
        var restoredState = await grain2.GetStateAsync();
        restoredState.Should().Be(DoorState.Locked);
        
        var eventCount = await grain2.GetTransitionCountAsync();
        eventCount.Should().Be(3);
    }

    [Fact]
    public async Task EventSourcedGrain_ShouldHandleInvalidTransitions()
    {
        // Arrange
        var grain = _testApp.Cluster.Client.GetGrain<ITestEventSourcedGrain>("test-es-4");

        // Act & Assert - Try to lock when open (invalid)
        await grain.OpenAsync();
        
        Func<Task> act = async () => await grain.LockAsync("password");
        await act.Should().ThrowAsync<InvalidStateTransitionException>()
            .WithMessage("*Cannot fire trigger 'Lock'* from state 'Open'*");
    }

    [Fact]
    public async Task EventSourcedGrain_ShouldTrackCorrelationId()
    {
        // Arrange
        var grain = _testApp.Cluster.Client.GetGrain<ITestEventSourcedGrain>("test-es-5");
        var correlationId = Guid.NewGuid().ToString();

        // Act
        grain.SetCorrelationId(correlationId);
        await grain.OpenAsync();
        await grain.CloseAsync();

        // Assert - Events should have correlation ID
        var lastCorrelationId = await grain.GetLastCorrelationIdAsync();
        lastCorrelationId.Should().Be(correlationId);
    }

    [Fact]
    public async Task EventSourcedGrain_ShouldSupportParameterizedTriggers()
    {
        // Arrange
        var grain = _testApp.Cluster.Client.GetGrain<ITestEventSourcedGrain>("test-es-6");

        // Act - Lock with password (door starts closed)
        await grain.LockAsync("my-password");

        // Assert
        var state = await grain.GetStateAsync();
        state.Should().Be(DoorState.Locked);
        
        // Try to unlock with wrong password
        Func<Task> wrongUnlock = async () => await grain.UnlockAsync("wrong-password");
        await wrongUnlock.Should().ThrowAsync<InvalidOperationException>();

        // Unlock with correct password
        await grain.UnlockAsync("my-password");
        var unlockedState = await grain.GetStateAsync();
        unlockedState.Should().Be(DoorState.Closed);
    }

    [Fact]
    public async Task EventSourcedGrain_ShouldQueryPermittedTriggers()
    {
        // Arrange
        var grain = _testApp.Cluster.Client.GetGrain<ITestEventSourcedGrain>("test-es-7");

        // Act & Assert - Check permitted triggers in each state
        var closedTriggers = await grain.GetPermittedTriggersAsync();
        closedTriggers.Should().Contain(DoorTrigger.Open);
        closedTriggers.Should().Contain(DoorTrigger.Lock);
        closedTriggers.Should().NotContain(DoorTrigger.Close);

        await grain.OpenAsync();
        var openTriggers = await grain.GetPermittedTriggersAsync();
        openTriggers.Should().Contain(DoorTrigger.Close);
        openTriggers.Should().NotContain(DoorTrigger.Open);
        openTriggers.Should().NotContain(DoorTrigger.Lock);
    }

    [Fact]
    public async Task EventSourcedGrain_ShouldCheckIfTriggerCanBeFired()
    {
        // Arrange
        var grain = _testApp.Cluster.Client.GetGrain<ITestEventSourcedGrain>("test-es-8");

        // Act & Assert - Closed state
        (await grain.CanFireAsync(DoorTrigger.Open)).Should().BeTrue();
        (await grain.CanFireAsync(DoorTrigger.Close)).Should().BeFalse();
        (await grain.CanFireAsync(DoorTrigger.Lock)).Should().BeTrue();

        // Move to open state
        await grain.OpenAsync();
        (await grain.CanFireAsync(DoorTrigger.Open)).Should().BeFalse();
        (await grain.CanFireAsync(DoorTrigger.Close)).Should().BeTrue();
        (await grain.CanFireAsync(DoorTrigger.Lock)).Should().BeFalse();
    }

    [Fact]
    public async Task EventSourcedGrain_ShouldMaintainLRUDedupeKeys()
    {
        // Arrange
        var grain = _testApp.Cluster.Client.GetGrain<ITestEventSourcedGrain>("test-es-9");

        // Act - Perform many transitions to test LRU eviction
        for (int i = 0; i < 10; i++)
        {
            await grain.OpenAsync();
            await grain.CloseAsync();
        }

        // Assert - Should only have unique transitions despite multiple attempts
        var state = await grain.GetStateAsync();
        state.Should().Be(DoorState.Closed);
        
        // Event count should be limited by deduplication
        var eventCount = await grain.GetTransitionCountAsync();
        eventCount.Should().Be(2); // Only first Open and Close should be recorded
    }

    [Fact]
    public async Task EventSourcedGrain_ShouldProvideStateMachineInfo()
    {
        // Arrange
        var grain = _testApp.Cluster.Client.GetGrain<ITestEventSourcedGrain>("test-es-10");

        // Act
        var info = await grain.GetInfoAsync();

        // Assert
        info.Should().NotBeNull();
        info.InitialState.UnderlyingState.Should().Be(1); // DoorState.Closed = 1
        info.States.Should().HaveCountGreaterThan(0);
        info.StateType.Should().Contain("DoorState");
        info.TriggerType.Should().Contain("DoorTrigger");
    }
}

// Test grain interfaces and implementations
public enum DoorState
{
    Open,
    Closed,
    Locked
}

public enum DoorTrigger
{
    Open,
    Close,
    Lock,
    Unlock
}

public interface ITestEventSourcedGrain : IGrainWithStringKey
{
    Task<DoorState> GetStateAsync();
    Task OpenAsync();
    Task CloseAsync();
    Task LockAsync(string password);
    Task UnlockAsync(string password);
    Task<int> GetTransitionCountAsync();
    Task<string?> GetLastCorrelationIdAsync();
    void SetCorrelationId(string correlationId);
    Task<IEnumerable<DoorTrigger>> GetPermittedTriggersAsync();
    Task<bool> CanFireAsync(DoorTrigger trigger);
    Task<ivlt.Orleans.StateMachineES.Models.OrleansStateMachineInfo> GetInfoAsync();
    Task DeactivateAsync();
}

    public class TestEventSourcedGrainState : EventSourcedStateMachineState<DoorState>
    {
        public string? Password { get; set; }
        public string? LastCorrelationId { get; set; }
    }

    [LogConsistencyProvider(ProviderName = "LogStorage")]
    [StorageProvider(ProviderName = "Default")]
    public class TestEventSourcedGrain : EventSourcedStateMachineGrain<DoorState, DoorTrigger, TestEventSourcedGrainState>, ITestEventSourcedGrain
    {
        private string? _currentPassword;
        private string? _lastCorrelationId;
        private string? _currentCorrelationId;
        private StateMachine<DoorState, DoorTrigger>.TriggerWithParameters<string>? _lockTriggerParam;

        protected override StateMachine<DoorState, DoorTrigger> BuildStateMachine()
        {
            var machine = new StateMachine<DoorState, DoorTrigger>(DoorState.Closed);

            machine.Configure(DoorState.Open)
                .Permit(DoorTrigger.Close, DoorState.Closed);

            machine.Configure(DoorState.Closed)
                .Permit(DoorTrigger.Open, DoorState.Open)
                .Permit(DoorTrigger.Lock, DoorState.Locked);

            _lockTriggerParam = machine.SetTriggerParameters<string>(DoorTrigger.Lock);
            
            // Cache the trigger parameter for reuse
            TriggerParametersCache[DoorTrigger.Lock] = _lockTriggerParam;
            
            machine.Configure(DoorState.Locked)
                .PermitIf(DoorTrigger.Unlock, DoorState.Closed, () => true)
                .OnEntryFrom(_lockTriggerParam, (string password) => 
                {
                    _currentPassword = password;
                    State.Password = password;
                });

            return machine;
        }

        protected override void ConfigureEventSourcing(EventSourcingOptions options)
        {
            options.AutoConfirmEvents = true;
            options.EnableIdempotency = true;
            options.MaxDedupeKeysInMemory = 100;
            options.EnableSnapshots = true;
            options.SnapshotInterval = 10;
        }

        protected override async Task RecordTransitionEvent(DoorState fromState, DoorState toState, DoorTrigger trigger, string? dedupeKey, Dictionary<string, object>? metadata = null)
        {
            _lastCorrelationId = _currentCorrelationId;
            State.LastCorrelationId = _lastCorrelationId;
            await base.RecordTransitionEvent(fromState, toState, trigger, dedupeKey, metadata);
        }

        public async Task OpenAsync()
        {
            await FireAsync(DoorTrigger.Open);
        }

        public async Task CloseAsync()
        {
            await FireAsync(DoorTrigger.Close);
        }

        public async Task LockAsync(string password)
        {
            await FireAsync(DoorTrigger.Lock, password);
        }

        public async Task UnlockAsync(string password)
        {
            if (password != State.Password)
            {
                throw new InvalidOperationException("Invalid password");
            }
            await FireAsync(DoorTrigger.Unlock);
        }

        public Task<int> GetTransitionCountAsync()
        {
            return Task.FromResult(State.TransitionCount);
        }

        public Task<string?> GetLastCorrelationIdAsync()
        {
            return Task.FromResult(State.LastCorrelationId);
        }

        public new void SetCorrelationId(string correlationId)
        {
            base.SetCorrelationId(correlationId);
            _currentCorrelationId = correlationId;
        }

        public async Task<IEnumerable<DoorTrigger>> GetPermittedTriggersAsync()
        {
            return await base.GetPermittedTriggersAsync();
        }

        public new async Task<bool> CanFireAsync(DoorTrigger trigger)
        {
            return await base.CanFireAsync(trigger);
        }

        public new async Task DeactivateAsync()
        {
            await base.DeactivateAsync();
        }
    }