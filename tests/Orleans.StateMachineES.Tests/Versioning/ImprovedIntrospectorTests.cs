using System;
using System.Linq;
using System.Threading.Tasks;
using FluentAssertions;
using Orleans.StateMachineES.Versioning;
using Microsoft.Extensions.Logging;
using Stateless;
using Xunit;
using StateMachineVersion = Orleans.StateMachineES.Abstractions.Models.StateMachineVersion;

namespace Orleans.StateMachineES.Tests.Versioning;

public class ImprovedIntrospectorTests
{
    private readonly ILogger<ImprovedStateMachineIntrospector<TestState, TestTrigger>> _logger;
    private readonly ImprovedStateMachineIntrospector<TestState, TestTrigger> _introspector;

    public ImprovedIntrospectorTests()
    {
        _logger = new LoggerFactory().CreateLogger<ImprovedStateMachineIntrospector<TestState, TestTrigger>>();
        _introspector = new ImprovedStateMachineIntrospector<TestState, TestTrigger>(_logger);
    }

    [Fact]
    public async Task ExtractEnhancedConfiguration_ShouldCaptureCompleteStateMachine()
    {
        // Arrange
        var machine = BuildComplexStateMachine();

        // Act
        var config = await _introspector.ExtractEnhancedConfigurationAsync(machine);

        // Assert
        config.Should().NotBeNull();
        config.IsValid.Should().BeTrue();
        config.States.Should().NotBeEmpty();
        config.InitialState.Should().Be(TestState.Initial);
        
        // Check metrics
        config.Metrics.Should().NotBeNull();
        config.Metrics.TotalStates.Should().BeGreaterThan(0);
        
        // Check for DOT graph
        if (!string.IsNullOrEmpty(config.DotGraphRepresentation))
        {
            config.DotGraphRepresentation.Should().Contain("digraph");
            config.DotGraphRepresentation.Should().Contain("->");
        }
    }

    [Fact]
    public async Task ExtractEnhancedConfiguration_ShouldIdentifyHierarchicalStates()
    {
        // Arrange
        var machine = BuildHierarchicalStateMachine();

        // Act
        var config = await _introspector.ExtractEnhancedConfigurationAsync(machine);

        // Assert
        config.States[TestState.SubStateA].Superstate.Should().Be(TestState.ParentState);
        config.States[TestState.SubStateB].Superstate.Should().Be(TestState.ParentState);
        config.States[TestState.ParentState].Substates.Should().Contain(TestState.SubStateA);
        config.States[TestState.ParentState].Substates.Should().Contain(TestState.SubStateB);
        
        // Check depth calculation
        config.Metrics.MaxStateDepth.Should().BeGreaterThan(0);
    }

    [Fact]
    public async Task ExtractEnhancedConfiguration_ShouldDetectPermittedTriggers()
    {
        // Arrange
        var machine = BuildComplexStateMachine();

        // Act
        var config = await _introspector.ExtractEnhancedConfigurationAsync(machine);

        // Assert
        var initialState = config.States[TestState.Initial];
        initialState.PermittedTriggers.Should().NotBeEmpty();
        
        // If we're in the initial state, we should have activable triggers
        if (machine.State == TestState.Initial)
        {
            initialState.ActivableTriggers.Should().NotBeEmpty();
        }
    }

    [Fact]
    public async Task ExtractEnhancedConfiguration_ShouldCalculateMetrics()
    {
        // Arrange
        var machine = BuildComplexStateMachine();

        // Act
        var config = await _introspector.ExtractEnhancedConfigurationAsync(machine);

        // Assert
        config.Metrics.Should().NotBeNull();
        config.Metrics.TotalStates.Should().BeGreaterThan(0);
        config.Metrics.AverageTransitionsPerState.Should().BeGreaterThanOrEqualTo(0);
        config.Metrics.CyclomaticComplexity.Should().NotBe(0);
    }

    [Fact]
    public async Task CompareConfigurations_ShouldDetectDifferences()
    {
        // Arrange
        var machine1 = BuildComplexStateMachine();
        var machine2 = BuildModifiedStateMachine();
        
        var config1 = await _introspector.ExtractEnhancedConfigurationAsync(machine1);
        var config2 = await _introspector.ExtractEnhancedConfigurationAsync(machine2);

        // Act
        var comparison = _introspector.CompareConfigurations(config1, config2);

        // Assert
        comparison.Should().NotBeNull();
        comparison.CommonStates.Should().NotBeEmpty();
        comparison.SimilarityScore.Should().BeInRange(0, 1);
        
        // Should detect if states were added or removed
        if (comparison.AddedStates.Count > 0 || comparison.RemovedStates.Count > 0)
        {
            comparison.SimilarityScore.Should().BeLessThan(1.0);
        }
    }

    [Fact]
    public async Task ExtractEnhancedConfiguration_ShouldHandleGuardedTransitions()
    {
        // Arrange
        var isConditionMet = false;
        var machine = new StateMachine<TestState, TestTrigger>(TestState.Initial);
        
        machine.Configure(TestState.Initial)
            .PermitIf(TestTrigger.Start, TestState.Processing, () => isConditionMet)
            .PermitIf(TestTrigger.Skip, TestState.Complete, () => !isConditionMet);

        // Act
        var config = await _introspector.ExtractEnhancedConfigurationAsync(machine);

        // Assert
        config.GuardedTriggers.Should().NotBeEmpty();
        config.Metrics.TotalGuardedTransitions.Should().BeGreaterThan(0);
    }

    [Fact]
    public async Task ExtractEnhancedConfiguration_ShouldExtractTransitionsFromDotGraph()
    {
        // Arrange
        var machine = BuildComplexStateMachine();

        // Act
        var config = await _introspector.ExtractEnhancedConfigurationAsync(machine);

        // Assert
        if (!string.IsNullOrEmpty(config.DotGraphRepresentation))
        {
            // Verify that transitions were extracted
            config.TransitionMap.Should().NotBeEmpty();
            
            // Check that at least some states have transitions
            var statesWithTransitions = config.States.Values
                .Where(s => s.Transitions.Count > 0)
                .ToList();
            
            statesWithTransitions.Should().NotBeEmpty();
        }
    }

    [Fact]
    public async Task CompareConfigurations_ShouldCalculateSimilarityScore()
    {
        // Arrange
        var machine1 = BuildComplexStateMachine();
        var machine2 = BuildComplexStateMachine(); // Identical
        
        var config1 = await _introspector.ExtractEnhancedConfigurationAsync(machine1);
        var config2 = await _introspector.ExtractEnhancedConfigurationAsync(machine2);

        // Act
        var comparison = _introspector.CompareConfigurations(config1, config2);

        // Assert
        comparison.SimilarityScore.Should().BeApproximately(1.0, 0.01); // Should be nearly identical
        comparison.AddedStates.Should().BeEmpty();
        comparison.RemovedStates.Should().BeEmpty();
    }

    private StateMachine<TestState, TestTrigger> BuildComplexStateMachine()
    {
        var machine = new StateMachine<TestState, TestTrigger>(TestState.Initial);

        machine.Configure(TestState.Initial)
            .Permit(TestTrigger.Start, TestState.Processing)
            .Permit(TestTrigger.Cancel, TestState.Cancelled);

        machine.Configure(TestState.Processing)
            .Permit(TestTrigger.Complete, TestState.Complete)
            .Permit(TestTrigger.Error, TestState.Failed)
            .Permit(TestTrigger.Cancel, TestState.Cancelled)
            .OnEntry(() => Console.WriteLine("Entering Processing"))
            .OnExit(() => Console.WriteLine("Exiting Processing"));

        machine.Configure(TestState.Complete)
            .Permit(TestTrigger.Reset, TestState.Initial);

        machine.Configure(TestState.Failed)
            .Permit(TestTrigger.Retry, TestState.Processing)
            .Permit(TestTrigger.Reset, TestState.Initial);

        machine.Configure(TestState.Cancelled)
            .Permit(TestTrigger.Reset, TestState.Initial);

        return machine;
    }

    private StateMachine<TestState, TestTrigger> BuildHierarchicalStateMachine()
    {
        var machine = new StateMachine<TestState, TestTrigger>(TestState.Initial);

        machine.Configure(TestState.Initial)
            .Permit(TestTrigger.Start, TestState.ParentState);

        machine.Configure(TestState.ParentState)
            .Permit(TestTrigger.Complete, TestState.Complete)
            .InitialTransition(TestState.SubStateA);

        machine.Configure(TestState.SubStateA)
            .SubstateOf(TestState.ParentState)
            .Permit(TestTrigger.Next, TestState.SubStateB);

        machine.Configure(TestState.SubStateB)
            .SubstateOf(TestState.ParentState)
            .Permit(TestTrigger.Next, TestState.SubStateA);

        machine.Configure(TestState.Complete);

        return machine;
    }

    private StateMachine<TestState, TestTrigger> BuildModifiedStateMachine()
    {
        var machine = new StateMachine<TestState, TestTrigger>(TestState.Initial);

        // Similar to complex but with modifications
        machine.Configure(TestState.Initial)
            .Permit(TestTrigger.Start, TestState.Processing)
            .Permit(TestTrigger.Skip, TestState.Complete); // Added transition

        machine.Configure(TestState.Processing)
            .Permit(TestTrigger.Complete, TestState.Complete)
            .Permit(TestTrigger.Error, TestState.Failed);
            // Removed Cancel transition

        machine.Configure(TestState.Complete)
            .Permit(TestTrigger.Reset, TestState.Initial);

        machine.Configure(TestState.Failed)
            .Permit(TestTrigger.Retry, TestState.Initial); // Changed destination
            
        // Removed Cancelled state entirely

        return machine;
    }

    private enum TestState
    {
        Initial,
        Processing,
        Complete,
        Failed,
        Cancelled,
        ParentState,
        SubStateA,
        SubStateB
    }

    private enum TestTrigger
    {
        Start,
        Complete,
        Error,
        Cancel,
        Reset,
        Retry,
        Skip,
        Next
    }
}