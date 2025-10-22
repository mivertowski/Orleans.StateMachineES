using FluentAssertions;
using Orleans.StateMachineES.Models;
using Stateless;
using Xunit;

namespace Orleans.StateMachineES.Tests.Unit.Core;

/// <summary>
/// Simple unit tests for state machine functionality without requiring Orleans cluster
/// </summary>
public class SimpleStateMachineTests
{
    private enum TestState { Idle, Active, Processing, Completed, Failed }
    private enum TestTrigger { Start, Process, Complete, Fail, Reset }

    [Fact]
    public void StateMachine_BasicTransition_Works()
    {
        // Arrange
        var machine = new StateMachine<TestState, TestTrigger>(TestState.Idle);
        machine.Configure(TestState.Idle)
            .Permit(TestTrigger.Start, TestState.Active);
        
        // Act
        machine.Fire(TestTrigger.Start);
        
        // Assert
        machine.State.Should().Be(TestState.Active);
    }

    [Fact]
    public void StateMachine_CanFire_ReturnsTrueForValidTrigger()
    {
        // Arrange
        var machine = new StateMachine<TestState, TestTrigger>(TestState.Idle);
        machine.Configure(TestState.Idle)
            .Permit(TestTrigger.Start, TestState.Active);
        
        // Act & Assert
        machine.CanFire(TestTrigger.Start).Should().BeTrue();
        machine.CanFire(TestTrigger.Complete).Should().BeFalse();
    }

    [Fact]
    public void StateMachine_PermittedTriggers_ReturnsCorrectTriggers()
    {
        // Arrange
        var machine = new StateMachine<TestState, TestTrigger>(TestState.Idle);
        machine.Configure(TestState.Idle)
            .Permit(TestTrigger.Start, TestState.Active)
            .Permit(TestTrigger.Fail, TestState.Failed);
        
        // Act
        var permitted = machine.PermittedTriggers;
        
        // Assert
        permitted.Should().Contain(TestTrigger.Start);
        permitted.Should().Contain(TestTrigger.Fail);
        permitted.Should().NotContain(TestTrigger.Complete);
    }

    [Fact]
    public void StateMachine_WithGuardCondition_RespectGuard()
    {
        // Arrange
        bool guardCondition = false;
        var machine = new StateMachine<TestState, TestTrigger>(TestState.Idle);
        machine.Configure(TestState.Idle)
            .PermitIf(TestTrigger.Start, TestState.Active, () => guardCondition);
        
        // Act & Assert - Guard is false
        machine.CanFire(TestTrigger.Start).Should().BeFalse();
        
        // Change guard condition
        guardCondition = true;
        machine.CanFire(TestTrigger.Start).Should().BeTrue();
    }

    [Fact]
    public void StateMachine_OnEntry_ExecutesAction()
    {
        // Arrange
        bool entryExecuted = false;
        var machine = new StateMachine<TestState, TestTrigger>(TestState.Idle);
        
        machine.Configure(TestState.Idle)
            .Permit(TestTrigger.Start, TestState.Active);
            
        machine.Configure(TestState.Active)
            .OnEntry(() => entryExecuted = true);
        
        // Act
        machine.Fire(TestTrigger.Start);
        
        // Assert
        entryExecuted.Should().BeTrue();
    }

    [Fact]
    public void StateMachine_OnExit_ExecutesAction()
    {
        // Arrange
        bool exitExecuted = false;
        var machine = new StateMachine<TestState, TestTrigger>(TestState.Idle);
        
        machine.Configure(TestState.Idle)
            .OnExit(() => exitExecuted = true)
            .Permit(TestTrigger.Start, TestState.Active);
        
        // Act
        machine.Fire(TestTrigger.Start);
        
        // Assert
        exitExecuted.Should().BeTrue();
    }

    [Fact]
    public void StateMachine_ParameterizedTrigger_PassesParameter()
    {
        // Arrange
        string? receivedParam = null;
        var machine = new StateMachine<TestState, TestTrigger>(TestState.Idle);
        var paramTrigger = machine.SetTriggerParameters<string>(TestTrigger.Process);
        
        machine.Configure(TestState.Idle)
            .Permit(TestTrigger.Start, TestState.Active);
            
        machine.Configure(TestState.Active)
            .Permit(TestTrigger.Process, TestState.Processing);
            
        machine.Configure(TestState.Processing)
            .OnEntryFrom(paramTrigger, param => receivedParam = param);
        
        // Act
        machine.Fire(TestTrigger.Start);
        machine.Fire(paramTrigger, "test-data");
        
        // Assert
        machine.State.Should().Be(TestState.Processing);
        receivedParam.Should().Be("test-data");
    }

    [Fact]
    public void StateMachine_InternalTransition_DoesNotChangeState()
    {
        // Arrange
        int actionCount = 0;
        var machine = new StateMachine<TestState, TestTrigger>(TestState.Active);
        
        machine.Configure(TestState.Active)
            .InternalTransition(TestTrigger.Process, () => actionCount++);
        
        // Act
        var stateBefore = machine.State;
        machine.Fire(TestTrigger.Process);
        var stateAfter = machine.State;
        
        // Assert
        stateBefore.Should().Be(stateAfter);
        actionCount.Should().Be(1);
    }

    [Fact]
    public void StateMachine_GetInfo_ReturnsCorrectInformation()
    {
        // Arrange
        var machine = new StateMachine<TestState, TestTrigger>(TestState.Idle);
        machine.Configure(TestState.Idle)
            .Permit(TestTrigger.Start, TestState.Active);
        machine.Configure(TestState.Active)
            .Permit(TestTrigger.Process, TestState.Processing);
        
        // Act
        var info = machine.GetInfo();
        
        // Assert
        info.Should().NotBeNull();
        info.InitialState.Should().NotBeNull();
        info.States.Should().NotBeEmpty();
        info.StateType.Should().Be(typeof(TestState));
        info.TriggerType.Should().Be(typeof(TestTrigger));
    }

    [Fact]
    public void StateMachine_ComplexWorkflow_ExecutesCorrectly()
    {
        // Arrange
        var executionLog = new List<string>();
        var machine = new StateMachine<TestState, TestTrigger>(TestState.Idle);
        
        machine.Configure(TestState.Idle)
            .OnEntry(() => executionLog.Add("Enter Idle"))
            .OnExit(() => executionLog.Add("Exit Idle"))
            .Permit(TestTrigger.Start, TestState.Active);
            
        machine.Configure(TestState.Active)
            .OnEntry(() => executionLog.Add("Enter Active"))
            .OnExit(() => executionLog.Add("Exit Active"))
            .Permit(TestTrigger.Process, TestState.Processing)
            .Permit(TestTrigger.Fail, TestState.Failed);
            
        machine.Configure(TestState.Processing)
            .OnEntry(() => executionLog.Add("Enter Processing"))
            .OnExit(() => executionLog.Add("Exit Processing"))
            .Permit(TestTrigger.Complete, TestState.Completed)
            .Permit(TestTrigger.Fail, TestState.Failed);
            
        machine.Configure(TestState.Completed)
            .OnEntry(() => executionLog.Add("Enter Completed"));
            
        machine.Configure(TestState.Failed)
            .OnEntry(() => executionLog.Add("Enter Failed"))
            .Permit(TestTrigger.Reset, TestState.Idle);
        
        // Act - Execute workflow
        machine.Fire(TestTrigger.Start);
        machine.Fire(TestTrigger.Process);
        machine.Fire(TestTrigger.Complete);
        
        // Assert
        machine.State.Should().Be(TestState.Completed);
        executionLog.Should().ContainInOrder(
            "Exit Idle",
            "Enter Active",
            "Exit Active", 
            "Enter Processing",
            "Exit Processing",
            "Enter Completed"
        );
    }

    [Fact]
    public void StateMachine_ReentrantState_HandledCorrectly()
    {
        // Arrange
        var machine = new StateMachine<TestState, TestTrigger>(TestState.Active);
        machine.Configure(TestState.Active)
            .PermitReentry(TestTrigger.Process)
            .Permit(TestTrigger.Complete, TestState.Completed);
        
        // Act
        var initialState = machine.State;
        machine.Fire(TestTrigger.Process); // Reentry
        var afterReentryState = machine.State;
        
        // Assert
        initialState.Should().Be(TestState.Active);
        afterReentryState.Should().Be(TestState.Active);
    }

    [Fact]
    public void OrleansStateInfo_Serialization_PreservesData()
    {
        // Arrange
        var machine = new StateMachine<TestState, TestTrigger>(TestState.Active);
        machine.Configure(TestState.Active)
            .Permit(TestTrigger.Process, TestState.Processing);
        
        var stateInfo = machine.GetInfo().States.First();
        
        // Act
        var orleansInfo = new OrleansStateInfo(stateInfo);
        
        // Assert
        orleansInfo.Should().NotBeNull();
        orleansInfo.UnderlyingState.Should().NotBeNull(); // Just check it's not null
    }

    [Fact]
    public void OrleansStateMachineInfo_PreservesStateMachineData()
    {
        // Arrange
        var machine = new StateMachine<TestState, TestTrigger>(TestState.Idle);
        machine.Configure(TestState.Idle)
            .Permit(TestTrigger.Start, TestState.Active);
        
        var info = machine.GetInfo();
        
        // Act
        var orleansInfo = new OrleansStateMachineInfo(info);
        
        // Assert
        orleansInfo.Should().NotBeNull();
        orleansInfo.States.Should().NotBeEmpty();
        orleansInfo.StateType.Should().Contain("TestState");
        orleansInfo.TriggerType.Should().Contain("TestTrigger");
    }

    [Fact]
    public async Task StateMachine_AsyncOperations_WorkCorrectly()
    {
        // Arrange
        var machine = new StateMachine<TestState, TestTrigger>(TestState.Idle);
        var asyncExecuted = false;
        
        machine.Configure(TestState.Idle)
            .Permit(TestTrigger.Start, TestState.Active);
            
        machine.Configure(TestState.Active)
            .OnEntryAsync(async () =>
            {
                await Task.Delay(10);
                asyncExecuted = true;
            });
        
        // Act
        await machine.FireAsync(TestTrigger.Start);
        
        // Assert
        machine.State.Should().Be(TestState.Active);
        asyncExecuted.Should().BeTrue();
    }

    [Fact]
    public void StateMachine_GuardCondition_ControlsTransition()
    {
        // Arrange
        bool guardPassed = false;
        var machine = new StateMachine<TestState, TestTrigger>(TestState.Idle);
        
        machine.Configure(TestState.Idle)
            .PermitIf(TestTrigger.Start, TestState.Active, 
                () => guardPassed, "Guard must pass");
        
        // Act & Assert
        machine.CanFire(TestTrigger.Start).Should().BeFalse();
        
        guardPassed = true;
        machine.CanFire(TestTrigger.Start).Should().BeTrue();
        
        // Fire the trigger
        machine.Fire(TestTrigger.Start);
        machine.State.Should().Be(TestState.Active);
    }

    [Fact]
    public void StateMachine_IgnoredTriggers_DoNotCauseErrors()
    {
        // Arrange
        var machine = new StateMachine<TestState, TestTrigger>(TestState.Active);
        machine.Configure(TestState.Active)
            .Ignore(TestTrigger.Reset)
            .Permit(TestTrigger.Complete, TestState.Completed);
        
        // Act
        var stateBefore = machine.State;
        machine.Fire(TestTrigger.Reset); // Should be ignored
        var stateAfter = machine.State;
        
        // Assert
        stateBefore.Should().Be(stateAfter);
        machine.CanFire(TestTrigger.Reset).Should().BeTrue(); // Can fire but will be ignored
    }

    [Fact]
    public void StateMachine_DynamicTransitions_WorkCorrectly()
    {
        // Arrange
        var targetState = TestState.Active;
        var machine = new StateMachine<TestState, TestTrigger>(TestState.Idle);
        
        machine.Configure(TestState.Idle)
            .PermitDynamic(TestTrigger.Start, () => targetState);
        
        // Act
        machine.Fire(TestTrigger.Start);
        
        // Assert
        machine.State.Should().Be(TestState.Active);
        
        // Change target and fire from new state
        machine.Configure(TestState.Active)
            .PermitDynamic(TestTrigger.Process, () => TestState.Processing);
        
        machine.Fire(TestTrigger.Process);
        machine.State.Should().Be(TestState.Processing);
    }
}