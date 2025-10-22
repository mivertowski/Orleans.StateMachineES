using Orleans.StateMachineES.Timers;
using FluentAssertions;
using Orleans.StateMachineES.Tests.Cluster;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Orleans.Providers;
using Stateless;
using Xunit;
using Xunit.Abstractions;

namespace Orleans.StateMachineES.Tests.Timers;

[Collection(nameof(TestClusterApplication))]
public class TimerEnabledStateMachineGrainTests(TestClusterApplication testApp, ITestOutputHelper outputHelper)
{
    private readonly ITestOutputHelper _outputHelper = outputHelper;
    private readonly TestClusterApplication _testApp = testApp;

    [Fact]
    public async Task TimerGrain_ShouldTransitionOnTimeout()
    {
        // Arrange
        var grain = _testApp.Cluster.Client.GetGrain<ITestTimerGrain>("timer-test-1");

        // Act - Start processing (which has a 2-second timeout)
        await grain.StartProcessingAsync();
        var initialState = await grain.GetStateAsync();
        initialState.Should().Be(ProcessingState.Processing);

        // Check if timer registration was successful
        var timerRegistrationSuccessful = await grain.IsTimerRegistrationSuccessfulAsync();
        var activeTimerCount = await grain.GetActiveTimerCountAsync();
        
        _outputHelper.WriteLine($"Timer registration successful: {timerRegistrationSuccessful}");
        _outputHelper.WriteLine($"Active timer count: {activeTimerCount}");

        // Wait for timeout to occur
        await Task.Delay(TimeSpan.FromSeconds(3));

        // Assert - Should have transitioned to TimedOut
        var finalState = await grain.GetStateAsync();
        var finalActiveTimerCount = await grain.GetActiveTimerCountAsync();
        
        _outputHelper.WriteLine($"Final state: {finalState}");
        _outputHelper.WriteLine($"Final active timer count: {finalActiveTimerCount}");
        
        finalState.Should().Be(ProcessingState.TimedOut);
    }

    [Fact]
    public async Task TimerGrain_ShouldCancelTimerOnStateChange()
    {
        // Arrange
        var grain = _testApp.Cluster.Client.GetGrain<ITestTimerGrain>("timer-test-2");

        // Act - Start processing then complete before timeout
        await grain.StartProcessingAsync();
        await grain.CompleteAsync();
        
        var stateAfterComplete = await grain.GetStateAsync();
        stateAfterComplete.Should().Be(ProcessingState.Completed);

        // Wait longer than timeout would have been
        await Task.Delay(TimeSpan.FromSeconds(3));

        // Assert - Should still be in Completed state (timer was cancelled)
        var finalState = await grain.GetStateAsync();
        finalState.Should().Be(ProcessingState.Completed);
    }

    [Fact]
    public async Task TimerGrain_ShouldHandleRepeatingTimer()
    {
        // Arrange
        var grain = _testApp.Cluster.Client.GetGrain<ITestTimerGrain>("timer-test-3");

        // Act - Start monitoring (which has a repeating timer)
        await grain.StartMonitoringAsync();
        
        // Check initial state
        var initialState = await grain.GetStateAsync();
        _outputHelper.WriteLine($"Initial state after StartMonitoringAsync: {initialState}");
        
        // Wait for multiple timer ticks
        await Task.Delay(TimeSpan.FromSeconds(3.5));

        // Check final state and counts
        var finalState = await grain.GetStateAsync();
        var heartbeatCount = await grain.GetHeartbeatCountAsync();
        var activeTimerCount = await grain.GetActiveTimerCountAsync();
        
        _outputHelper.WriteLine($"Final state: {finalState}");
        _outputHelper.WriteLine($"Heartbeat count: {heartbeatCount}");
        _outputHelper.WriteLine($"Active timer count: {activeTimerCount}");

        // Assert - Should have multiple heartbeats
        heartbeatCount.Should().BeGreaterThan(2);
    }

    [Fact]
    public async Task TimerGrain_ShouldUseDurableReminderForLongTimeouts()
    {
        // Arrange
        var grain = _testApp.Cluster.Client.GetGrain<ITestTimerGrain>("timer-test-4");

        // Act - Start long running task (uses reminder)
        await grain.StartLongRunningAsync();
        var initialState = await grain.GetStateAsync();
        initialState.Should().Be(ProcessingState.LongRunning);

        // Simulate grain deactivation and reactivation
        await grain.DeactivateAsync();
        await Task.Delay(100);

        // Get grain again (will reactivate)
        var grain2 = _testApp.Cluster.Client.GetGrain<ITestTimerGrain>("timer-test-4");
        var reactivatedState = await grain2.GetStateAsync();
        
        // Assert - State should be preserved and reminder should be active
        reactivatedState.Should().Be(ProcessingState.LongRunning);
    }
}

// Test grain interfaces and implementation
public enum ProcessingState
{
    Idle,
    Processing,
    Monitoring,
    LongRunning,
    Completed,
    TimedOut,
    Failed
}

public enum ProcessingTrigger
{
    Start,
    StartMonitoring,
    StartLongRunning,
    Complete,
    Fail,
    Timeout,
    Heartbeat,
    Cancel
}

[Alias("Orleans.StateMachineES.Tests.Timers.ITestTimerGrain")]
public interface ITestTimerGrain : IGrainWithStringKey
{
    [Alias("GetStateAsync")]
    ValueTask<ProcessingState> GetStateAsync();
    [Alias("StartProcessingAsync")]
    Task StartProcessingAsync();
    [Alias("StartMonitoringAsync")]
    Task StartMonitoringAsync();
    [Alias("StartLongRunningAsync")]
    Task StartLongRunningAsync();
    [Alias("CompleteAsync")]
    Task CompleteAsync();
    [Alias("FailAsync")]
    Task FailAsync();
    [Alias("GetHeartbeatCountAsync")]
    Task<int> GetHeartbeatCountAsync();
    [Alias("DeactivateAsync")]
    Task DeactivateAsync();
    [Alias("GetActiveTimerCountAsync")]
    Task<int> GetActiveTimerCountAsync();
    [Alias("IsTimerRegistrationSuccessfulAsync")]
    Task<bool> IsTimerRegistrationSuccessfulAsync();
}

[LogConsistencyProvider(ProviderName = "LogStorage")]
[StorageProvider(ProviderName = "Default")]
public class TestTimerGrain : TimerEnabledStateMachineGrain<ProcessingState, ProcessingTrigger, TestTimerGrainState>, ITestTimerGrain
{
    private int _heartbeatCount = 0;
    private ILogger<TestTimerGrain>? _logger;
    private IGrainTimer? _testTimer;
    private bool _timerRegistrationSuccessful = false;

    public override async Task OnActivateAsync(CancellationToken cancellationToken)
    {
        await base.OnActivateAsync(cancellationToken);
        _logger = this.ServiceProvider.GetService<ILogger<TestTimerGrain>>();
        _logger?.LogInformation("TestTimerGrain activated with ID: {GrainId}", this.GetPrimaryKeyString());
        
        // Test direct timer registration to verify it works
        _testTimer = this.RegisterGrainTimer(
            async (cancellationToken) => 
            {
                _logger?.LogInformation("Direct test timer fired!");
                await Task.CompletedTask;
            },
            new GrainTimerCreationOptions
            {
                DueTime = TimeSpan.FromSeconds(1),
                Period = TimeSpan.FromSeconds(1),
                Interleave = true
            });
        _logger?.LogInformation("Direct test timer registered: {TimerNotNull}", _testTimer != null);
    }

    protected override StateMachine<ProcessingState, ProcessingTrigger> BuildStateMachine()
    {
        var machine = new StateMachine<ProcessingState, ProcessingTrigger>(ProcessingState.Idle);

        machine.Configure(ProcessingState.Idle)
            .Permit(ProcessingTrigger.Start, ProcessingState.Processing)
            .Permit(ProcessingTrigger.StartMonitoring, ProcessingState.Monitoring)
            .Permit(ProcessingTrigger.StartLongRunning, ProcessingState.LongRunning);

        machine.Configure(ProcessingState.Processing)
            .Permit(ProcessingTrigger.Complete, ProcessingState.Completed)
            .Permit(ProcessingTrigger.Fail, ProcessingState.Failed)
            .Permit(ProcessingTrigger.Timeout, ProcessingState.TimedOut);

        machine.Configure(ProcessingState.Monitoring)
            .Permit(ProcessingTrigger.Cancel, ProcessingState.Idle)
            .Permit(ProcessingTrigger.Fail, ProcessingState.Failed)
            .Ignore(ProcessingTrigger.Heartbeat)
            .OnEntry(() => _heartbeatCount = 0);

        machine.Configure(ProcessingState.LongRunning)
            .Permit(ProcessingTrigger.Complete, ProcessingState.Completed)
            .Permit(ProcessingTrigger.Timeout, ProcessingState.TimedOut);

        machine.Configure(ProcessingState.Completed);
        machine.Configure(ProcessingState.TimedOut);
        machine.Configure(ProcessingState.Failed);

        return machine;
    }

    protected override void ConfigureTimeouts()
    {
        _logger?.LogInformation("ConfigureTimeouts called");
        
        try
        {
            // Processing state times out after 2 seconds
            RegisterStateTimeout(ProcessingState.Processing,
                ConfigureTimeout(ProcessingState.Processing)
                    .After(TimeSpan.FromSeconds(2))
                    .TransitionTo(ProcessingTrigger.Timeout)
                    .UseTimer()
                    .WithName("ProcessingTimeout")
                    .Build());
                    
            _timerRegistrationSuccessful = true;
            _logger?.LogInformation("Configured timeout for Processing state successfully");
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "Failed to configure timeout for Processing state");
        }

        // Monitoring state has a repeating heartbeat every 1 second
        RegisterStateTimeout(ProcessingState.Monitoring,
            ConfigureTimeout(ProcessingState.Monitoring)
                .After(TimeSpan.FromSeconds(1))
                .TransitionTo(ProcessingTrigger.Heartbeat)
                .UseTimer()
                .Repeat()
                .WithName("MonitoringHeartbeat")
                .Build());

        // Long running uses reminder (simulating > 5 minute timeout)
        RegisterStateTimeout(ProcessingState.LongRunning,
            ConfigureTimeout(ProcessingState.LongRunning)
                .After(TimeSpan.FromMinutes(10))
                .TransitionTo(ProcessingTrigger.Timeout)
                .UseDurableReminder()
                .WithName("LongRunningTimeout")
                .Build());
    }

    protected override string GenerateDedupeKey(ProcessingTrigger trigger, params object[] args)
    {
        // For heartbeat triggers, include timestamp to prevent deduplication
        if (trigger == ProcessingTrigger.Heartbeat)
        {
            var timestamp = DateTimeOffset.UtcNow.Ticks;
            return $"{this.GetPrimaryKeyString()}:{trigger}:{timestamp}";
        }
        
        // Use base implementation for other triggers
        return base.GenerateDedupeKey(trigger, args);
    }

    protected override async Task RecordTransitionEvent(ProcessingState fromState, ProcessingState toState, ProcessingTrigger trigger, string? dedupeKey, Dictionary<string, object>? metadata = null)
    {
        _logger?.LogInformation("RecordTransitionEvent: {FromState} -> {ToState} via {Trigger}", fromState, toState, trigger);
        
        // Track heartbeats
        if (trigger == ProcessingTrigger.Heartbeat)
        {
            _heartbeatCount++;
            State.HeartbeatCount = _heartbeatCount;
            _logger?.LogInformation("Heartbeat #{Count} recorded", _heartbeatCount);
        }

        await base.RecordTransitionEvent(fromState, toState, trigger, dedupeKey, metadata);
    }

    public async Task StartProcessingAsync()
    {
        _logger?.LogInformation("StartProcessingAsync called");
        _logger?.LogInformation("Current state before firing: {State}", StateMachine.State);
        await FireAsync(ProcessingTrigger.Start);
        _logger?.LogInformation("Current state after firing: {State}", StateMachine.State);
    }

    public async Task StartMonitoringAsync()
    {
        _logger?.LogInformation("StartMonitoringAsync called");
        await FireAsync(ProcessingTrigger.StartMonitoring);
    }

    public async Task StartLongRunningAsync()
    {
        await FireAsync(ProcessingTrigger.StartLongRunning);
    }

    public async Task CompleteAsync()
    {
        await FireAsync(ProcessingTrigger.Complete);
    }

    public async Task FailAsync()
    {
        await FireAsync(ProcessingTrigger.Fail);
    }

    public Task<int> GetHeartbeatCountAsync()
    {
        return Task.FromResult(_heartbeatCount);
    }

    public new async Task DeactivateAsync()
    {
        await base.DeactivateAsync();
    }

    public Task<int> GetActiveTimerCountAsync()
    {
        // Access the private field through reflection for debugging
        var activeTimersField = typeof(TimerEnabledStateMachineGrain<ProcessingState, ProcessingTrigger, TestTimerGrainState>)
            .GetField("_activeTimers", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
        
        if (activeTimersField?.GetValue(this) is Dictionary<string, IGrainTimer> activeTimers)
        {
            return Task.FromResult(activeTimers.Count);
        }
        
        return Task.FromResult(-1);
    }

    public Task<bool> IsTimerRegistrationSuccessfulAsync()
    {
        return Task.FromResult(_timerRegistrationSuccessful);
    }
}

[GenerateSerializer]
[Alias("Orleans.StateMachineES.Tests.Timers.TestTimerGrainState")]
public class TestTimerGrainState : TimerEnabledStateMachineState<ProcessingState>
{
    [Id(0)]
    public int HeartbeatCount { get; set; }
}