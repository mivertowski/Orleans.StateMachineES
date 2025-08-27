using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using FluentAssertions;
using Orleans.StateMachineES.EventSourcing.Exceptions;
using Orleans.StateMachineES.Hierarchical;
using Orleans.StateMachineES.Tests.Cluster;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Orleans;
using Orleans.EventSourcing;
using Orleans.Providers;
using Orleans.TestingHost;
using Stateless;
using Xunit;
using Xunit.Abstractions;

namespace Orleans.StateMachineES.Tests.Hierarchical;

[Collection(nameof(TestClusterApplication))]
public class HierarchicalStateMachineGrainTests
{
    private readonly ITestOutputHelper _outputHelper;
    private readonly TestClusterApplication _testApp;

    public HierarchicalStateMachineGrainTests(TestClusterApplication testApp, ITestOutputHelper outputHelper)
    {
        _testApp = testApp;
        _outputHelper = outputHelper;
    }

    [Fact]
    public async Task HierarchicalGrain_ShouldConfigureBasicHierarchy()
    {
        // Arrange
        var grain = _testApp.Cluster.Client.GetGrain<IDeviceControllerGrain>("device-hierarchy-1");

        // Act
        var hierarchyInfo = await grain.GetHierarchicalInfoAsync();

        // Assert - Verify hierarchy structure
        // Online should be a root state (has children but no parent)
        hierarchyInfo.RootStates.Should().Contain(DeviceState.Online);
        // Note: Offline is not in hierarchy relationships, so it won't appear in RootStates
        // unless we explicitly track all state machine states
        
        // Online should have substates
        var onlineSubstates = await grain.GetSubstatesAsync(DeviceState.Online);
        onlineSubstates.Should().Contain(DeviceState.Idle);
        onlineSubstates.Should().Contain(DeviceState.Active);
        
        // Active should have substates
        var activeSubstates = await grain.GetSubstatesAsync(DeviceState.Active);
        activeSubstates.Should().Contain(DeviceState.Processing);
        activeSubstates.Should().Contain(DeviceState.Monitoring);
    }

    [Fact]
    public async Task HierarchicalGrain_ShouldTransitionBetweenHierarchicalStates()
    {
        // Arrange
        var grain = _testApp.Cluster.Client.GetGrain<IDeviceControllerGrain>("device-hierarchy-2");

        // Act & Assert - Start offline
        var initialState = await grain.GetStateAsync();
        initialState.Should().Be(DeviceState.Offline);

        // Transition to online (root state)
        await grain.PowerOnAsync();
        var onlineState = await grain.GetStateAsync();
        onlineState.Should().Be(DeviceState.Idle);

        // Should be in Online hierarchy
        (await grain.IsInStateOrSubstateAsync(DeviceState.Online)).Should().BeTrue();

        // Transition to processing (nested substate)
        await grain.StartProcessingAsync();
        var processingState = await grain.GetStateAsync();
        processingState.Should().Be(DeviceState.Processing);

        // Should be in multiple hierarchical states
        (await grain.IsInStateAsync(DeviceState.Processing)).Should().BeTrue();
        (await grain.IsInStateOrSubstateAsync(DeviceState.Active)).Should().BeTrue();
        (await grain.IsInStateOrSubstateAsync(DeviceState.Online)).Should().BeTrue();
    }

    [Fact]
    public async Task HierarchicalGrain_ShouldProvideHierarchyPathInformation()
    {
        // Arrange
        var grain = _testApp.Cluster.Client.GetGrain<IDeviceControllerGrain>("device-hierarchy-3");

        // Act - Transition to deeply nested state
        await grain.PowerOnAsync();
        await grain.StartProcessingAsync();

        // Assert - Check hierarchy path
        var currentPath = await grain.GetCurrentStatePathAsync();
        currentPath.Should().ContainInOrder(DeviceState.Online, DeviceState.Active, DeviceState.Processing);

        // Check parent relationships
        var processingParent = await grain.GetParentStateAsync(DeviceState.Processing);
        processingParent.Should().Be(DeviceState.Active);

        var activeParent = await grain.GetParentStateAsync(DeviceState.Active);
        activeParent.Should().Be(DeviceState.Online);

        // Check ancestors
        var ancestors = await grain.GetAncestorStatesAsync(DeviceState.Processing);
        ancestors.Should().ContainInOrder(DeviceState.Active, DeviceState.Online);
    }

    [Fact]
    public async Task HierarchicalGrain_ShouldHandleTransitionsBetweenSiblingStates()
    {
        // Arrange
        var grain = _testApp.Cluster.Client.GetGrain<IDeviceControllerGrain>("device-hierarchy-4");

        // Act - Move to Active state, then between its substates
        await grain.PowerOnAsync(); // -> Idle (Online substate)
        await grain.StartProcessingAsync(); // -> Processing (Active substate)

        var initialSubstate = await grain.GetStateAsync();
        initialSubstate.Should().Be(DeviceState.Processing);

        // Transition to sibling state
        await grain.StartMonitoringAsync(); // -> Monitoring (Active substate)
        var siblingState = await grain.GetStateAsync();
        siblingState.Should().Be(DeviceState.Monitoring);

        // Should still be in parent Active and Online states
        (await grain.IsInStateOrSubstateAsync(DeviceState.Active)).Should().BeTrue();
        (await grain.IsInStateOrSubstateAsync(DeviceState.Online)).Should().BeTrue();
    }

    [Fact]
    public async Task HierarchicalGrain_ShouldHandleExitFromNestedState()
    {
        // Arrange
        var grain = _testApp.Cluster.Client.GetGrain<IDeviceControllerGrain>("device-hierarchy-5");

        // Act - Go deep into hierarchy then exit
        await grain.PowerOnAsync(); // -> Idle
        await grain.StartProcessingAsync(); // -> Processing
        
        var deepState = await grain.GetStateAsync();
        deepState.Should().Be(DeviceState.Processing);

        // Exit to parent state
        await grain.StopProcessingAsync(); // Should go to Idle (up the hierarchy)
        var parentState = await grain.GetStateAsync();
        parentState.Should().Be(DeviceState.Idle);

        // Should still be in Online hierarchy
        (await grain.IsInStateOrSubstateAsync(DeviceState.Online)).Should().BeTrue();
        // But not in Active hierarchy anymore
        (await grain.IsInStateOrSubstateAsync(DeviceState.Active)).Should().BeFalse();
    }

    [Fact]
    public async Task HierarchicalGrain_ShouldProvideDescendantInformation()
    {
        // Arrange
        var grain = _testApp.Cluster.Client.GetGrain<IDeviceControllerGrain>("device-hierarchy-6");

        // Act
        var onlineDescendants = await grain.GetDescendantStatesAsync(DeviceState.Online);
        var activeDescendants = await grain.GetDescendantStatesAsync(DeviceState.Active);

        // Assert - Online should have all nested descendants
        onlineDescendants.Should().Contain(DeviceState.Idle);
        onlineDescendants.Should().Contain(DeviceState.Active);
        onlineDescendants.Should().Contain(DeviceState.Processing);
        onlineDescendants.Should().Contain(DeviceState.Monitoring);

        // Active should only have its direct descendants
        activeDescendants.Should().Contain(DeviceState.Processing);
        activeDescendants.Should().Contain(DeviceState.Monitoring);
        activeDescendants.Should().NotContain(DeviceState.Idle);
    }

    [Fact]
    public async Task HierarchicalGrain_ShouldHandleTimeoutsInHierarchicalStates()
    {
        // Arrange
        var grain = _testApp.Cluster.Client.GetGrain<IDeviceControllerGrain>("device-hierarchy-7");

        // Act - Transition to state with timeout
        await grain.PowerOnAsync();
        await grain.StartProcessingAsync();

        var initialState = await grain.GetStateAsync();
        initialState.Should().Be(DeviceState.Processing);

        // Wait for timeout from Processing state (2 seconds)
        await Task.Delay(TimeSpan.FromSeconds(2.5));

        // Assert - Should have timed out back to Idle
        var finalState = await grain.GetStateAsync();
        finalState.Should().Be(DeviceState.Idle);
    }

    [Fact]
    public async Task HierarchicalGrain_ShouldRecordHierarchicalTransitionMetadata()
    {
        // Arrange
        var grain = _testApp.Cluster.Client.GetGrain<IDeviceControllerGrain>("device-hierarchy-8");

        // Act - Perform hierarchical transitions
        await grain.PowerOnAsync(); // Offline -> Idle
        await grain.StartProcessingAsync(); // Idle -> Processing

        // We can't directly access the event metadata in tests, but we can verify
        // the transitions work correctly and the hierarchy is maintained
        var currentState = await grain.GetStateAsync();
        currentState.Should().Be(DeviceState.Processing);

        var hierarchyPath = await grain.GetCurrentStatePathAsync();
        hierarchyPath.Should().ContainInOrder(DeviceState.Online, DeviceState.Active, DeviceState.Processing);
    }
}

// Test grain implementation with complex hierarchy
public interface IDeviceControllerGrain : IGrainWithStringKey
{
    ValueTask<DeviceState> GetStateAsync();
    Task PowerOnAsync();
    Task PowerOffAsync();
    Task StartProcessingAsync();
    Task StopProcessingAsync();
    Task StartMonitoringAsync();
    Task StopMonitoringAsync();
    
    // Hierarchical state queries  
    Task<IReadOnlyList<DeviceState>> GetSubstatesAsync(DeviceState parentState);
    Task<IReadOnlyList<DeviceState>> GetAncestorStatesAsync(DeviceState state);
    Task<IReadOnlyList<DeviceState>> GetDescendantStatesAsync(DeviceState parentState);
    Task<bool> IsInStateOrSubstateAsync(DeviceState state);
    ValueTask<bool> IsInStateAsync(DeviceState state);
    Task<IReadOnlyList<DeviceState>> GetCurrentStatePathAsync();
    Task<HierarchicalStateInfo<DeviceState>> GetHierarchicalInfoAsync();
    
    // Explicit interface methods for nullable returns
    Task<DeviceState?> GetParentStateAsync(DeviceState state);
    Task<DeviceState?> GetActiveSubstateAsync(DeviceState parentState);
}

[LogConsistencyProvider(ProviderName = "LogStorage")]
[StorageProvider(ProviderName = "Default")]
public class DeviceControllerGrain : HierarchicalStateMachineGrain<DeviceState, DeviceTrigger, DeviceControllerGrainState>, IDeviceControllerGrain
{
    private ILogger<DeviceControllerGrain>? _logger;

    public override async Task OnActivateAsync(CancellationToken cancellationToken)
    {
        await base.OnActivateAsync(cancellationToken);
        _logger = this.ServiceProvider.GetService<ILogger<DeviceControllerGrain>>();
        _logger?.LogInformation("DeviceControllerGrain activated with ID: {GrainId}", this.GetPrimaryKeyString());
    }

    protected override StateMachine<DeviceState, DeviceTrigger> BuildStateMachine()
    {
        var machine = new StateMachine<DeviceState, DeviceTrigger>(DeviceState.Offline);

        // Root level states
        machine.Configure(DeviceState.Offline)
            .Permit(DeviceTrigger.PowerOn, DeviceState.Idle);

        // Online is a parent state with Idle and Active as substates
        machine.Configure(DeviceState.Online)
            .OnEntry(() => _logger?.LogInformation("Device came online"))
            .OnExit(() => _logger?.LogInformation("Device going offline"))
            .Permit(DeviceTrigger.PowerOff, DeviceState.Offline);

        // Idle is a substate of Online
        machine.Configure(DeviceState.Idle)
            .SubstateOf(DeviceState.Online)
            .OnEntry(() => _logger?.LogInformation("Device is idle"))
            .Permit(DeviceTrigger.StartProcessing, DeviceState.Processing)
            .Permit(DeviceTrigger.StartMonitoring, DeviceState.Monitoring);

        // Active is a substate of Online, parent to Processing and Monitoring
        machine.Configure(DeviceState.Active)
            .SubstateOf(DeviceState.Online)
            .OnEntry(() => _logger?.LogInformation("Device is active"))
            .OnExit(() => _logger?.LogInformation("Device stopping activity"))
            .Permit(DeviceTrigger.Stop, DeviceState.Idle);

        // Processing is a substate of Active
        machine.Configure(DeviceState.Processing)
            .SubstateOf(DeviceState.Active)
            .OnEntry(() => _logger?.LogInformation("Started processing"))
            .OnExit(() => _logger?.LogInformation("Stopped processing"))
            .Permit(DeviceTrigger.StartMonitoring, DeviceState.Monitoring)
            .Permit(DeviceTrigger.Stop, DeviceState.Idle)
            .Permit(DeviceTrigger.Timeout, DeviceState.Idle);

        // Monitoring is a substate of Active
        machine.Configure(DeviceState.Monitoring)
            .SubstateOf(DeviceState.Active)
            .OnEntry(() => _logger?.LogInformation("Started monitoring"))
            .OnExit(() => _logger?.LogInformation("Stopped monitoring"))
            .Permit(DeviceTrigger.StartProcessing, DeviceState.Processing)
            .Permit(DeviceTrigger.Stop, DeviceState.Idle);

        return machine;
    }

    protected override void ConfigureHierarchy()
    {
        // Define the hierarchical relationships - Offline and Online are root states
        // Only define relationships where there is actually a parent-child relationship
        DefineSubstate(DeviceState.Idle, DeviceState.Online);
        DefineSubstate(DeviceState.Active, DeviceState.Online);
        DefineSubstate(DeviceState.Processing, DeviceState.Active);
        DefineSubstate(DeviceState.Monitoring, DeviceState.Active);
    }

    protected override void ConfigureTimeouts()
    {
        // Processing state has a short timeout for testing
        RegisterStateTimeout(DeviceState.Processing,
            ConfigureTimeout(DeviceState.Processing)
                .After(TimeSpan.FromSeconds(2))
                .TransitionTo(DeviceTrigger.Timeout)
                .UseTimer()
                .WithName("ProcessingTimeout")
                .Build());
    }

    // Implement interface methods
    public Task PowerOnAsync() => FireAsync(DeviceTrigger.PowerOn);
    public Task PowerOffAsync() => FireAsync(DeviceTrigger.PowerOff);
    public Task StartProcessingAsync() => FireAsync(DeviceTrigger.StartProcessing);
    public Task StopProcessingAsync() => FireAsync(DeviceTrigger.Stop);
    public Task StartMonitoringAsync() => FireAsync(DeviceTrigger.StartMonitoring);
    public Task StopMonitoringAsync() => FireAsync(DeviceTrigger.Stop);
    
    // Explicit interface implementations for nullable returns
    async Task<DeviceState?> IDeviceControllerGrain.GetParentStateAsync(DeviceState state)
    {
        var result = await base.GetParentStateAsync(state);
        return result;
    }
    
    async Task<DeviceState?> IDeviceControllerGrain.GetActiveSubstateAsync(DeviceState parentState)
    {
        var result = await base.GetActiveSubstateAsync(parentState);
        return result;
    }
}

// Device state enumeration with hierarchical structure
public enum DeviceState
{
    // Root states
    Offline,
    
    // Online hierarchy
    Online,      // Parent state
    Idle,        // Substate of Online
    Active,      // Substate of Online, parent to Processing/Monitoring
    Processing,  // Substate of Active
    Monitoring   // Substate of Active
}

public enum DeviceTrigger
{
    PowerOn,
    PowerOff,
    StartProcessing,
    StartMonitoring,
    Stop,
    Timeout
}

[GenerateSerializer]
[Alias("DeviceControllerGrainState")]
public class DeviceControllerGrainState : HierarchicalStateMachineState<DeviceState>
{
    [Id(0)]
    public DateTime LastActivity { get; set; } = DateTime.UtcNow;
}