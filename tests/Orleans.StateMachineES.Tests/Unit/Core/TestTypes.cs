using Orleans.StateMachineES.Interfaces;
using Stateless;

namespace Orleans.StateMachineES.Tests.Unit.Core;

// Test state and trigger enums
public enum UnitTestState
{
    Idle,
    Active,
    Processing,
    Completed
}

public enum UnitTestTrigger
{
    Start,
    Process,
    Complete
}

// Test grain interface
[Alias("Orleans.StateMachineES.Tests.Unit.Core.IUnitTestGrain")]
public interface IUnitTestGrain : IStateMachineGrain<UnitTestState, UnitTestTrigger>, IGrainWithStringKey
{
    [Alias("StartAsync")]
    Task StartAsync();
    [Alias("ProcessAsync")]
    Task ProcessAsync();
    [Alias("CompleteAsync")]
    Task CompleteAsync();
}

// Test grain implementation
[GrainType("UnitTestGrain")]
public class UnitTestGrain : StateMachineGrain<UnitTestState, UnitTestTrigger>, IUnitTestGrain
{
    protected override StateMachine<UnitTestState, UnitTestTrigger> BuildStateMachine()
    {
        var machine = new StateMachine<UnitTestState, UnitTestTrigger>(UnitTestState.Idle);
        
        machine.Configure(UnitTestState.Idle)
            .Permit(UnitTestTrigger.Start, UnitTestState.Active);
            
        machine.Configure(UnitTestState.Active)
            .Permit(UnitTestTrigger.Process, UnitTestState.Processing);
            
        machine.Configure(UnitTestState.Processing)
            .Permit(UnitTestTrigger.Complete, UnitTestState.Completed);
            
        machine.Configure(UnitTestState.Completed);
        
        return machine;
    }
    
    public Task StartAsync()
    {
        return FireAsync(UnitTestTrigger.Start);
    }
    
    public Task ProcessAsync()
    {
        return FireAsync(UnitTestTrigger.Process);
    }
    
    public Task CompleteAsync()
    {
        return FireAsync(UnitTestTrigger.Complete);
    }
}