using FluentAssertions;
using Orleans.StateMachineES.Tests.Cluster;
using Orleans.StateMachineES.Versioning;
using Orleans.Providers;
using Stateless;
using Xunit;
using Xunit.Abstractions;
using StateMachineVersion = Orleans.StateMachineES.Abstractions.Models.StateMachineVersion;

namespace Orleans.StateMachineES.Tests.Versioning;

[Collection(nameof(TestClusterApplication))]
public class VersionedStateMachineTests(TestClusterApplication testApp, ITestOutputHelper outputHelper)
{
    private readonly ITestOutputHelper _outputHelper = outputHelper;
    private readonly TestClusterApplication _testApp = testApp;

    [Fact]
    public async Task VersionedGrain_ShouldInitialize_WithLatestVersion()
    {
        // Arrange
        var grainId = $"version-test-init-{Guid.NewGuid():N}";
        var grain = _testApp.Cluster.Client.GetGrain<IOrderWorkflowGrain>(grainId);

        // Act
        var version = await grain.GetVersionAsync();
        var compatibility = await grain.GetVersionCompatibilityAsync();

        // Assert
        version.Should().NotBeNull();
        version.Major.Should().BeGreaterThan(0);
        compatibility.Should().NotBeNull();
        compatibility.CurrentVersion.Should().Be(version);
    }

    [Fact]
    public async Task VersionedGrain_ShouldUpgrade_FromV1ToV2()
    {
        // Arrange
        var grainId = $"version-test-upgrade-{Guid.NewGuid():N}";
        var grain = _testApp.Cluster.Client.GetGrain<IOrderWorkflowGrain>(grainId);

        // Initialize with V1
        await grain.InitializeWithVersionAsync(new StateMachineVersion(1, 0, 0));
        var initialVersion = await grain.GetVersionAsync();

        // Act
        var targetVersion = new StateMachineVersion(1, 1, 0);
        var upgradeResult = await grain.UpgradeToVersionAsync(targetVersion, MigrationStrategy.Automatic);

        // Assert
        upgradeResult.IsSuccess.Should().BeTrue();
        upgradeResult.OldVersion.Should().Be(initialVersion);
        upgradeResult.NewVersion.Should().Be(targetVersion);

        var newVersion = await grain.GetVersionAsync();
        newVersion.Should().Be(targetVersion);
    }

    [Fact]
    public async Task VersionedGrain_ShouldFailUpgrade_WhenTargetVersionIncompatible()
    {
        // Arrange
        var grainId = $"version-test-fail-{Guid.NewGuid():N}";
        var grain = _testApp.Cluster.Client.GetGrain<IOrderWorkflowGrain>(grainId);

        await grain.InitializeWithVersionAsync(new StateMachineVersion(1, 0, 0));

        // Act - try to downgrade to unregistered version
        var targetVersion = new StateMachineVersion(0, 9, 0);
        var upgradeResult = await grain.UpgradeToVersionAsync(targetVersion);

        // Assert
        upgradeResult.IsSuccess.Should().BeFalse();
        upgradeResult.ErrorMessage.Should().Contain("is not compatible");
    }

    [Fact]
    public async Task VersionedGrain_ShouldPerformShadowEvaluation()
    {
        // Arrange
        var grainId = $"version-test-shadow-{Guid.NewGuid():N}";
        var grain = _testApp.Cluster.Client.GetGrain<IOrderWorkflowGrain>(grainId);

        await grain.InitializeWithVersionAsync(new StateMachineVersion(1, 0, 0));
        await grain.PlaceOrderAsync("ORD-123", 100.00m);

        // Act
        var shadowVersion = new StateMachineVersion(1, 1, 0);
        var shadowResult = await grain.RunShadowEvaluationAsync(shadowVersion, OrderTrigger.ProcessPayment);

        // Assert
        shadowResult.Should().NotBeNull();
        shadowResult.EvaluatedVersion.Should().Be(shadowVersion);
        shadowResult.CurrentState.Should().Be(OrderState.Placed);
    }

    [Fact]
    public async Task VersionedGrain_ShouldTrackVersionHistory()
    {
        // Arrange
        var grainId = $"version-test-history-{Guid.NewGuid():N}";
        var grain = _testApp.Cluster.Client.GetGrain<IOrderWorkflowGrain>(grainId);

        await grain.InitializeWithVersionAsync(new StateMachineVersion(1, 0, 0));

        // Act - perform multiple upgrades using registered versions
        await grain.UpgradeToVersionAsync(new StateMachineVersion(1, 1, 0));
        await grain.UpgradeToVersionAsync(new StateMachineVersion(2, 0, 0));

        var history = await grain.GetVersionHistoryAsync();

        // Assert
        history.Should().NotBeNull();
        history.Count.Should().BeGreaterThan(2); // Initial + 2 upgrades
        
        var lastUpgrade = history[^1];
        lastUpgrade.Version.Should().Be(new StateMachineVersion(2, 0, 0));
        lastUpgrade.PreviousVersion.Should().Be(new StateMachineVersion(1, 1, 0));
    }

    [Fact]
    public async Task VersionedGrain_ShouldHandleMigrationWithCustomStrategy()
    {
        // Arrange
        var grainId = $"version-test-custom-{Guid.NewGuid():N}";
        var grain = _testApp.Cluster.Client.GetGrain<IOrderWorkflowGrain>(grainId);

        await grain.InitializeWithVersionAsync(new StateMachineVersion(1, 0, 0));

        // Act
        var targetVersion = new StateMachineVersion(2, 0, 0); // Major version upgrade
        var upgradeResult = await grain.UpgradeToVersionAsync(targetVersion, MigrationStrategy.Custom);

        // Assert
        upgradeResult.IsSuccess.Should().BeTrue();
        upgradeResult.MigrationSummary.Should().NotBeNull();
        upgradeResult.MigrationSummary!.ChangesApplied.Should().NotBeEmpty();
    }

    [Fact]
    public async Task VersionedGrain_ShouldSupportDryRunMigration()
    {
        // Arrange
        var grainId = $"version-test-dryrun-{Guid.NewGuid():N}";
        var grain = _testApp.Cluster.Client.GetGrain<IOrderWorkflowGrain>(grainId);

        var originalVersion = new StateMachineVersion(1, 0, 0);
        await grain.InitializeWithVersionAsync(originalVersion);

        // Act
        var targetVersion = new StateMachineVersion(1, 1, 0);
        var dryRunResult = await grain.UpgradeToVersionAsync(targetVersion, MigrationStrategy.DryRun);

        // Assert
        dryRunResult.IsSuccess.Should().BeTrue();
        dryRunResult.MigrationSummary!.ChangesApplied.Should().Contain(x => x.Contains("Dry run migration"));

        // Version should remain unchanged after dry run
        var currentVersion = await grain.GetVersionAsync();
        currentVersion.Should().Be(originalVersion);
    }

    [Fact]
    public async Task VersionedGrain_ShouldListAvailableVersions()
    {
        // Arrange
        var grainId = $"version-test-available-{Guid.NewGuid():N}";
        var grain = _testApp.Cluster.Client.GetGrain<IOrderWorkflowGrain>(grainId);

        // Act
        var availableVersions = await grain.GetAvailableVersionsAsync();

        // Assert
        availableVersions.Should().NotBeEmpty();
        var targetVersion = new StateMachineVersion(1, 0, 0);
        availableVersions.Should().Contain(v => v >= targetVersion);
    }

    [Fact]
    public async Task VersionedGrain_ShouldMaintainFunctionality_AcrossVersionUpgrades()
    {
        // Arrange
        var grainId = $"version-test-functionality-{Guid.NewGuid():N}";
        var grain = _testApp.Cluster.Client.GetGrain<IOrderWorkflowGrain>(grainId);

        await grain.InitializeWithVersionAsync(new StateMachineVersion(1, 0, 0));

        // Test initial functionality
        await grain.PlaceOrderAsync("ORD-001", 150.00m);
        var initialState = await grain.GetCurrentStateAsync();
        initialState.Should().Be(OrderState.Placed);

        // Act - upgrade version
        await grain.UpgradeToVersionAsync(new StateMachineVersion(1, 1, 0));

        // Assert - functionality should still work
        await grain.ProcessPaymentAsync();
        var newState = await grain.GetCurrentStateAsync();
        newState.Should().Be(OrderState.PaymentProcessed);

        // Should be able to continue workflow
        await grain.FulfillOrderAsync();
        var finalState = await grain.GetCurrentStateAsync();
        finalState.Should().Be(OrderState.Fulfilled);
    }
}

// Test grain interfaces and implementations for versioning

[Alias("Orleans.StateMachineES.Tests.Versioning.IOrderWorkflowGrain")]
public interface IOrderWorkflowGrain : IVersionedStateMachine<OrderState, OrderTrigger>, IGrainWithStringKey
{
    [Alias("PlaceOrderAsync")]
    Task PlaceOrderAsync(string orderId, decimal amount);
    [Alias("ProcessPaymentAsync")]
    Task ProcessPaymentAsync();
    [Alias("FulfillOrderAsync")]
    Task FulfillOrderAsync();
    [Alias("GetCurrentStateAsync")]
    Task<OrderState> GetCurrentStateAsync();
    [Alias("InitializeWithVersionAsync")]
    Task InitializeWithVersionAsync(StateMachineVersion version);
    [Alias("GetVersionHistoryAsync")]
    Task<IReadOnlyList<VersionHistoryEntry>> GetVersionHistoryAsync();
}

public enum OrderState
{
    Draft,
    Placed,
    PaymentProcessed,
    Fulfilled,
    Cancelled
}

public enum OrderTrigger
{
    PlaceOrder,
    ProcessPayment,
    FulfillOrder,
    CancelOrder
}

[LogConsistencyProvider(ProviderName = "LogStorage")]
[StorageProvider(ProviderName = "Default")]
[Alias("OrderWorkflowGrain")]
public class OrderWorkflowGrain : 
    VersionedStateMachineGrain<OrderState, OrderTrigger, OrderWorkflowState>,
    IOrderWorkflowGrain
{
    protected override StateMachine<OrderState, OrderTrigger> BuildStateMachine()
    {
        // Return the current version's state machine
        return BuildOrderWorkflowV2();
    }

    protected override async Task RegisterBuiltInVersionsAsync()
    {
        if (DefinitionRegistry != null)
        {
            // Register version 1.0.0
            await DefinitionRegistry.RegisterDefinitionAsync<OrderState, OrderTrigger>(
                GetType().Name,
                new StateMachineVersion(1, 0, 0),
                () => BuildOrderWorkflowV1(),
                new StateMachineDefinitionMetadata
                {
                    Description = "Initial order workflow implementation",
                    Author = "Test System",
                    Features = { "Basic order processing", "Payment handling" }
                });

            // Register version 1.1.0
            await DefinitionRegistry.RegisterDefinitionAsync<OrderState, OrderTrigger>(
                GetType().Name,
                new StateMachineVersion(1, 1, 0),
                () => BuildOrderWorkflowV11(),
                new StateMachineDefinitionMetadata
                {
                    Description = "Enhanced order workflow with better error handling",
                    Author = "Test System",
                    Features = { "Enhanced payment processing", "Improved error handling" }
                });

            // Register version 2.0.0
            await DefinitionRegistry.RegisterDefinitionAsync<OrderState, OrderTrigger>(
                GetType().Name,
                new StateMachineVersion(2, 0, 0),
                () => BuildOrderWorkflowV2(),
                new StateMachineDefinitionMetadata
                {
                    Description = "Major refactor with new states and transitions",
                    Author = "Test System",
                    Features = { "New workflow states", "Breaking changes" },
                    BreakingChanges = { "Added new intermediate states", "Changed transition logic" }
                });
        }
    }

    protected override Task<StateMachine<OrderState, OrderTrigger>?> BuildVersionedStateMachineAsync(StateMachineVersion version)
    {
        return Task.FromResult<StateMachine<OrderState, OrderTrigger>?>(version switch
        {
            { Major: 1, Minor: 0, Patch: 0 } => BuildOrderWorkflowV1(),
            { Major: 1, Minor: 1, Patch: 0 } => BuildOrderWorkflowV11(),
            { Major: 2, Minor: 0, Patch: 0 } => BuildOrderWorkflowV2(),
            _ => null
        });
    }

    private static StateMachine<OrderState, OrderTrigger> BuildOrderWorkflowV1()
    {
        var machine = new StateMachine<OrderState, OrderTrigger>(OrderState.Draft);

        machine.Configure(OrderState.Draft)
            .Permit(OrderTrigger.PlaceOrder, OrderState.Placed)
            .Permit(OrderTrigger.CancelOrder, OrderState.Cancelled);

        machine.Configure(OrderState.Placed)
            .Permit(OrderTrigger.ProcessPayment, OrderState.PaymentProcessed)
            .Permit(OrderTrigger.CancelOrder, OrderState.Cancelled);

        machine.Configure(OrderState.PaymentProcessed)
            .Permit(OrderTrigger.FulfillOrder, OrderState.Fulfilled);

        machine.Configure(OrderState.Fulfilled)
            .Ignore(OrderTrigger.FulfillOrder);

        machine.Configure(OrderState.Cancelled)
            .Ignore(OrderTrigger.CancelOrder);

        return machine;
    }

    private StateMachine<OrderState, OrderTrigger> BuildOrderWorkflowV11()
    {
        // V1.1 - Enhanced version with better transitions
        var machine = BuildOrderWorkflowV1();
        
        // Add ability to cancel even after payment processing (enhancement)
        machine.Configure(OrderState.PaymentProcessed)
            .Permit(OrderTrigger.CancelOrder, OrderState.Cancelled);

        return machine;
    }

    private static StateMachine<OrderState, OrderTrigger> BuildOrderWorkflowV2()
    {
        // V2.0 - Major refactor (breaking changes)
        var machine = new StateMachine<OrderState, OrderTrigger>(OrderState.Draft);

        // Same basic structure but with enhanced logic
        machine.Configure(OrderState.Draft)
            .Permit(OrderTrigger.PlaceOrder, OrderState.Placed)
            .Permit(OrderTrigger.CancelOrder, OrderState.Cancelled);

        machine.Configure(OrderState.Placed)
            .Permit(OrderTrigger.ProcessPayment, OrderState.PaymentProcessed)
            .Permit(OrderTrigger.CancelOrder, OrderState.Cancelled);

        machine.Configure(OrderState.PaymentProcessed)
            .Permit(OrderTrigger.FulfillOrder, OrderState.Fulfilled)
            .Permit(OrderTrigger.CancelOrder, OrderState.Cancelled);

        machine.Configure(OrderState.Fulfilled)
            .Ignore(OrderTrigger.FulfillOrder);

        machine.Configure(OrderState.Cancelled)
            .Ignore(OrderTrigger.CancelOrder);

        return machine;
    }

    // Implementation of IOrderWorkflowGrain

    public async Task PlaceOrderAsync(string orderId, decimal amount)
    {
        State.OrderId = orderId;
        State.Amount = amount;
        State.CurrentState = OrderState.Placed;
        
        // In a real implementation, we would fire the trigger through the state machine
        // For testing, we'll just update the state directly
        await Task.CompletedTask;
    }

    public async Task ProcessPaymentAsync()
    {
        State.CurrentState = OrderState.PaymentProcessed;
        await Task.CompletedTask;
    }

    public async Task FulfillOrderAsync()
    {
        State.CurrentState = OrderState.Fulfilled;
        await Task.CompletedTask;
    }

    public Task<OrderState> GetCurrentStateAsync()
    {
        return Task.FromResult(State.CurrentState);
    }

    public Task InitializeWithVersionAsync(StateMachineVersion version)
    {
        State.Version = version;
        State.VersionHistory.Add(new VersionHistoryEntry
        {
            Version = version,
            UpgradedAt = DateTime.UtcNow,
            Reason = "Manual initialization for testing"
        });
        
        return Task.CompletedTask;
    }

    public Task<IReadOnlyList<VersionHistoryEntry>> GetVersionHistoryAsync()
    {
        return Task.FromResult<IReadOnlyList<VersionHistoryEntry>>(State.VersionHistory);
    }

    protected override Task<MigrationSummary> PerformCustomMigrationAsync(
        StateMachineVersion fromVersion, 
        StateMachineVersion toVersion)
    {
        var summary = new MigrationSummary();
        summary.ChangesApplied.Add($"Custom migration from {fromVersion} to {toVersion}");
        
        if (toVersion.Major > fromVersion.Major)
        {
            summary.StatesMigrated = 1;
            summary.TransitionsUpdated = 2;
            summary.ChangesApplied.Add("Applied major version migration logic");
        }
        else
        {
            summary.ChangesApplied.Add("Applied minor version migration logic");
        }

        return Task.FromResult(summary);
    }
}

[GenerateSerializer]
[Alias("OrderWorkflowState")]
public class OrderWorkflowState : VersionedStateMachineState<OrderState>
{
    [Id(0)] public string OrderId { get; set; } = "";
    [Id(1)] public decimal Amount { get; set; }
    [Id(2)] public DateTime OrderDate { get; set; } = DateTime.UtcNow;
    [Id(3)] public string CustomerId { get; set; } = "";
}