using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using FluentAssertions;
using Orleans.StateMachineES.Tests.Cluster;
using Orleans.StateMachineES.Versioning;
using StateMachineVersion = Orleans.StateMachineES.Abstractions.Models.StateMachineVersion;
using RiskLevel = Orleans.StateMachineES.Abstractions.Models.RiskLevel;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Orleans;
using Stateless;
using Xunit;
using Xunit.Abstractions;

namespace Orleans.StateMachineES.Tests.Versioning;

[Collection(nameof(TestClusterApplication))]
[Trait("Category", "Integration")] 
[Trait("Skip", "Versioning integration refactor in progress - will be re-enabled in v1.0.2")]
public class VersionCompatibilityTests
{
    private readonly ITestOutputHelper _outputHelper;
    private readonly TestClusterApplication _testApp;

    public VersionCompatibilityTests(TestClusterApplication testApp, ITestOutputHelper outputHelper)
    {
        _testApp = testApp;
        _outputHelper = outputHelper;
    }

    [Fact(Skip = "Versioning integration in progress - will be re-enabled in v1.0.2")]
    public async Task VersionCompatibility_ShouldIdentifyCompatibleUpgrade()
    {
        // Arrange
        var registry = CreateTestRegistry();
        var checker = new VersionCompatibilityChecker(registry, CreateLogger<VersionCompatibilityChecker>());
        
        await SetupTestVersions(registry);

        // Act
        var result = await checker.CheckCompatibilityAsync(
            "TestGrain", 
            new StateMachineVersion(1, 0, 0), 
            new StateMachineVersion(1, 1, 0));

        // Assert
        result.IsCompatible.Should().BeTrue();
        result.CompatibilityLevel.Should().Be(VersionCompatibilityLevel.Compatible);
        result.BreakingChanges.Should().BeEmpty();
    }

    [Fact(Skip = "Versioning integration refactor in progress - will be re-enabled in v1.0.2")]
    public async Task VersionCompatibility_ShouldIdentifyIncompatibleUpgrade()
    {
        // Arrange
        var registry = CreateTestRegistry();
        var checker = new VersionCompatibilityChecker(registry, CreateLogger<VersionCompatibilityChecker>());
        
        await SetupTestVersions(registry);

        // Act
        var result = await checker.CheckCompatibilityAsync(
            "TestGrain", 
            new StateMachineVersion(1, 0, 0), 
            new StateMachineVersion(2, 0, 0));

        // Assert
        result.IsCompatible.Should().BeFalse();
        result.BreakingChanges.Should().NotBeEmpty();
        result.BreakingChanges.Should().Contain(bc => bc.ChangeType == BreakingChangeType.MajorVersionIncrease);
    }

    [Fact(Skip = "Versioning integration refactor in progress - will be re-enabled in v1.0.2")]
    public async Task VersionCompatibility_ShouldAnalyzeCompatibilityMatrix()
    {
        // Arrange
        var registry = CreateTestRegistry();
        var checker = new VersionCompatibilityChecker(registry, CreateLogger<VersionCompatibilityChecker>());
        
        await SetupTestVersions(registry);

        // Act
        var matrix = await checker.AnalyzeCompatibilityMatrixAsync("TestGrain");

        // Assert
        matrix.GrainTypeName.Should().Be("TestGrain");
        matrix.Versions.Should().HaveCountGreaterThan(0);
        matrix.Statistics.Should().NotBeNull();
        matrix.Statistics.TotalVersions.Should().BeGreaterThan(0);
    }

    [Fact(Skip = "Versioning integration refactor in progress - will be re-enabled in v1.0.2")]
    public async Task VersionCompatibility_ShouldProvideUpgradeRecommendations()
    {
        // Arrange
        var registry = CreateTestRegistry();
        var checker = new VersionCompatibilityChecker(registry, CreateLogger<VersionCompatibilityChecker>());
        
        await SetupTestVersions(registry);

        // Act
        var recommendations = await checker.GetUpgradeRecommendationsAsync(
            "TestGrain", 
            new StateMachineVersion(1, 0, 0));

        // Assert
        recommendations.Should().NotBeEmpty();
        
        var minorUpgrade = recommendations.FirstOrDefault(r => r.ToVersion.Minor > 0 && r.ToVersion.Major == 1);
        minorUpgrade.Should().NotBeNull();
        minorUpgrade!.RecommendationType.Should().BeOneOf(
            UpgradeRecommendationType.Recommended, 
            UpgradeRecommendationType.HighlyRecommended);

        var majorUpgrade = recommendations.FirstOrDefault(r => r.ToVersion.Major > 1);
        if (majorUpgrade != null)
        {
            majorUpgrade.RiskLevel.Should().BeOneOf(RiskLevel.High, RiskLevel.VeryHigh);
        }
    }

    [Fact(Skip = "Versioning integration refactor in progress - will be re-enabled in v1.0.2")]
    public async Task VersionCompatibility_ShouldValidateDeploymentCompatibility()
    {
        // Arrange
        var registry = CreateTestRegistry();
        var checker = new VersionCompatibilityChecker(registry, CreateLogger<VersionCompatibilityChecker>());
        
        await SetupTestVersions(registry);

        var existingVersions = new[]
        {
            new StateMachineVersion(1, 0, 0)
        };

        // Act - compatible deployment using registered version (minor upgrade)
        var compatibleResult = await checker.ValidateDeploymentCompatibilityAsync(
            "TestGrain",
            new StateMachineVersion(1, 1, 0),
            existingVersions);

        // Assert
        compatibleResult.CanDeploy.Should().BeTrue();
        compatibleResult.Issues.Should().BeEmpty();

        // Act - incompatible deployment
        var incompatibleResult = await checker.ValidateDeploymentCompatibilityAsync(
            "TestGrain",
            new StateMachineVersion(2, 0, 0),
            existingVersions);

        // Assert
        incompatibleResult.CanDeploy.Should().BeFalse();
        incompatibleResult.RecommendedStrategy.Should().Be(DeploymentStrategy.Blocked);
    }

    [Fact(Skip = "Versioning integration refactor in progress - will be re-enabled in v1.0.2")]
    public async Task ShadowEvaluator_ShouldCompareVersions()
    {
        // Arrange
        var evaluator = new ShadowEvaluator<TestState, TestTrigger>(CreateLogger<ShadowEvaluator<TestState, TestTrigger>>());
        
        var versionedMachines = new Dictionary<StateMachineVersion, StateMachine<TestState, TestTrigger>>
        {
            [new StateMachineVersion(1, 0, 0)] = CreateTestStateMachineV1(),
            [new StateMachineVersion(1, 1, 0)] = CreateTestStateMachineV11()
        };

        // Act
        var comparison = await evaluator.EvaluateAcrossVersionsAsync(
            TestState.Initial,
            TestTrigger.Start,
            versionedMachines,
            new StateMachineVersion(1, 0, 0));

        // Assert
        comparison.Should().NotBeNull();
        comparison.EvaluationResults.Should().HaveCount(2);
        comparison.ConsensusResult.Should().NotBeNull();
    }

    private IStateMachineDefinitionRegistry CreateTestRegistry()
    {
        var logger = CreateLogger<StateMachineDefinitionRegistry>();
        return new StateMachineDefinitionRegistry(logger);
    }

    private async Task SetupTestVersions(IStateMachineDefinitionRegistry registry)
    {
        // Register version 1.0.0
        await registry.RegisterDefinitionAsync<TestState, TestTrigger>(
            "TestGrain",
            new StateMachineVersion(1, 0, 0),
            CreateTestStateMachineV1,
            new StateMachineDefinitionMetadata
            {
                Description = "Initial version",
                Features = { "Basic functionality" }
            });

        // Register version 1.1.0
        await registry.RegisterDefinitionAsync<TestState, TestTrigger>(
            "TestGrain",
            new StateMachineVersion(1, 1, 0),
            CreateTestStateMachineV11,
            new StateMachineDefinitionMetadata
            {
                Description = "Enhanced version",
                Features = { "Basic functionality", "Enhanced features" }
            });

        // Register version 2.0.0
        await registry.RegisterDefinitionAsync<TestState, TestTrigger>(
            "TestGrain",
            new StateMachineVersion(2, 0, 0),
            CreateTestStateMachineV2,
            new StateMachineDefinitionMetadata
            {
                Description = "Major rewrite",
                Features = { "New architecture", "Breaking changes" },
                BreakingChanges = { "State model changed", "New required parameters" }
            });
    }

    private static StateMachine<TestState, TestTrigger> CreateTestStateMachineV1()
    {
        var machine = new StateMachine<TestState, TestTrigger>(TestState.Initial);
        
        machine.Configure(TestState.Initial)
            .Permit(TestTrigger.Start, TestState.Active);

        machine.Configure(TestState.Active)
            .Permit(TestTrigger.Complete, TestState.Completed)
            .Permit(TestTrigger.Cancel, TestState.Cancelled);

        machine.Configure(TestState.Completed)
            .Ignore(TestTrigger.Complete);

        machine.Configure(TestState.Cancelled)
            .Ignore(TestTrigger.Cancel);

        return machine;
    }

    private static StateMachine<TestState, TestTrigger> CreateTestStateMachineV11()
    {
        var machine = CreateTestStateMachineV1();
        
        // Add restart capability (backward compatible enhancement)
        machine.Configure(TestState.Completed)
            .Permit(TestTrigger.Start, TestState.Active);

        return machine;
    }

    private static StateMachine<TestState, TestTrigger> CreateTestStateMachineV2()
    {
        var machine = new StateMachine<TestState, TestTrigger>(TestState.Initial);
        
        // V2 has different state transitions (breaking changes)
        machine.Configure(TestState.Initial)
            .Permit(TestTrigger.Start, TestState.Processing); // Different target state

        machine.Configure(TestState.Processing) // New state
            .Permit(TestTrigger.Complete, TestState.Active);

        machine.Configure(TestState.Active)
            .Permit(TestTrigger.Complete, TestState.Completed);

        return machine;
    }

    private ILogger<T> CreateLogger<T>()
    {
        return _testApp.Cluster.ServiceProvider.GetRequiredService<ILogger<T>>();
    }

    public enum TestState
    {
        Initial,
        Active,
        Processing,
        Completed,
        Cancelled
    }

    public enum TestTrigger
    {
        Start,
        Complete,
        Cancel
    }
}

[Collection(nameof(TestClusterApplication))]
public class MigrationHooksTests
{
    private readonly ITestOutputHelper _outputHelper;
    private readonly TestClusterApplication _testApp;

    public MigrationHooksTests(TestClusterApplication testApp, ITestOutputHelper outputHelper)
    {
        _testApp = testApp;
        _outputHelper = outputHelper;
    }

    [Fact(Skip = "Versioning integration refactor in progress - will be re-enabled in v1.0.2")]
    public void MigrationHookManager_ShouldRegisterAndOrderHooks()
    {
        // Arrange
        var logger = _testApp.Cluster.ServiceProvider.GetRequiredService<ILogger<MigrationHookManager>>();
        var manager = new MigrationHookManager(logger);
        
        var hook1 = new TestMigrationHook("Hook1", 20);
        var hook2 = new TestMigrationHook("Hook2", 10);
        var hook3 = new TestMigrationHook("Hook3", 30);

        // Act
        manager.RegisterHook(hook1);
        manager.RegisterHook(hook2);
        manager.RegisterHook(hook3);

        var registeredHooks = manager.GetRegisteredHooks();

        // Assert
        registeredHooks.Should().HaveCount(3);
        registeredHooks[0].HookName.Should().Be("Hook2"); // Priority 10
        registeredHooks[1].HookName.Should().Be("Hook1"); // Priority 20
        registeredHooks[2].HookName.Should().Be("Hook3"); // Priority 30
    }

    [Fact(Skip = "Versioning integration refactor in progress - will be re-enabled in v1.0.2")]
    public async Task MigrationHookManager_ShouldExecuteBeforeMigrationHooks()
    {
        // Arrange
        var logger = _testApp.Cluster.ServiceProvider.GetRequiredService<ILogger<MigrationHookManager>>();
        var manager = new MigrationHookManager(logger);
        
        var hook1 = new TestMigrationHook("Hook1", 10);
        var hook2 = new TestMigrationHook("Hook2", 20);
        
        manager.RegisterHook(hook1);
        manager.RegisterHook(hook2);

        var context = new MigrationContext
        {
            GrainId = "test-grain",
            GrainTypeName = "TestGrain",
            FromVersion = new StateMachineVersion(1, 0, 0),
            ToVersion = new StateMachineVersion(1, 1, 0)
        };

        // Act
        var result = await manager.ExecuteBeforeMigrationHooksAsync(context);

        // Assert
        result.Should().BeTrue();
        hook1.BeforeMigrationCalled.Should().BeTrue();
        hook2.BeforeMigrationCalled.Should().BeTrue();
        context.ExecutedHooks.Should().Contain("Hook1_BeforeMigration");
        context.ExecutedHooks.Should().Contain("Hook2_BeforeMigration");
    }

    [Fact(Skip = "Versioning integration refactor in progress - will be re-enabled in v1.0.2")]
    public async Task MigrationHookManager_ShouldAbortOnHookFailure()
    {
        // Arrange
        var logger = _testApp.Cluster.ServiceProvider.GetRequiredService<ILogger<MigrationHookManager>>();
        var manager = new MigrationHookManager(logger);
        
        var hook1 = new TestMigrationHook("Hook1", 10);
        var failingHook = new TestMigrationHook("FailingHook", 20, shouldFail: true);
        var hook3 = new TestMigrationHook("Hook3", 30);
        
        manager.RegisterHook(hook1);
        manager.RegisterHook(failingHook);
        manager.RegisterHook(hook3);

        var context = new MigrationContext
        {
            GrainId = "test-grain",
            GrainTypeName = "TestGrain",
            FromVersion = new StateMachineVersion(1, 0, 0),
            ToVersion = new StateMachineVersion(1, 1, 0)
        };

        // Act
        var result = await manager.ExecuteBeforeMigrationHooksAsync(context);

        // Assert
        result.Should().BeFalse();
        hook1.BeforeMigrationCalled.Should().BeTrue();
        failingHook.BeforeMigrationCalled.Should().BeTrue();
        hook3.BeforeMigrationCalled.Should().BeFalse(); // Should not execute after failure
    }

    [Fact(Skip = "Versioning integration refactor in progress - will be re-enabled in v1.0.2")]
    public async Task BuiltInHooks_ShouldValidateStateCompatibility()
    {
        // Arrange
        var logger = _testApp.Cluster.ServiceProvider.GetRequiredService<ILogger<BuiltInMigrationHooks.StateCompatibilityValidationHook>>();
        var hook = new BuiltInMigrationHooks.StateCompatibilityValidationHook();
        
        var context = new MigrationContext
        {
            GrainId = "test-grain",
            GrainTypeName = "TestGrain",
            FromVersion = new StateMachineVersion(1, 0, 0),
            ToVersion = new StateMachineVersion(1, 1, 0)
        };
        
        context.SetStateValue("CurrentState", "Active");

        // Act
        var result = await hook.BeforeMigrationAsync(context);

        // Assert
        result.Should().BeTrue();
        context.Metadata.Should().ContainKey("StateValidation");
    }

    [Fact(Skip = "Versioning integration refactor in progress - will be re-enabled in v1.0.2")]
    public async Task BuiltInHooks_ShouldBackupAndRestoreState()
    {
        // Arrange
        var hook = new BuiltInMigrationHooks.StateBackupHook();
        
        var context = new MigrationContext
        {
            GrainId = "test-grain",
            GrainTypeName = "TestGrain",
            FromVersion = new StateMachineVersion(1, 0, 0),
            ToVersion = new StateMachineVersion(1, 1, 0)
        };
        
        context.SetStateValue("TestProperty", "TestValue");
        context.SetStateValue("AnotherProperty", 42);

        // Act - Create backup
        var backupResult = await hook.BeforeMigrationAsync(context);
        
        // Modify state to simulate migration
        context.SetStateValue("TestProperty", "ModifiedValue");

        // Simulate rollback
        var rollbackResult = hook.OnMigrationRollbackAsync(context, new Exception("Test error"));

        // Assert
        backupResult.Should().BeTrue();
        context.Metadata.Should().ContainKey("StateBackup");
        
        await rollbackResult;
        context.GetStateValue<string>("TestProperty").Should().Be("TestValue");
        context.GrainState["AnotherProperty"].Should().Be(42);
        context.Metadata.Should().ContainKey("StateRestored");
    }

    private class TestMigrationHook : IMigrationHook
    {
        private readonly bool _shouldFail;

        public TestMigrationHook(string name, int priority, bool shouldFail = false)
        {
            HookName = name;
            Priority = priority;
            _shouldFail = shouldFail;
        }

        public string HookName { get; }
        public int Priority { get; }
        public bool BeforeMigrationCalled { get; private set; }
        public bool AfterMigrationCalled { get; private set; }
        public bool RollbackCalled { get; private set; }

        public Task<bool> BeforeMigrationAsync(MigrationContext context)
        {
            BeforeMigrationCalled = true;
            
            if (_shouldFail)
                throw new InvalidOperationException($"Hook {HookName} failed");
                
            return Task.FromResult(true);
        }

        public Task AfterMigrationAsync(MigrationContext context)
        {
            AfterMigrationCalled = true;
            return Task.CompletedTask;
        }

        public Task OnMigrationRollbackAsync(MigrationContext context, Exception error)
        {
            RollbackCalled = true;
            return Task.CompletedTask;
        }
    }
}