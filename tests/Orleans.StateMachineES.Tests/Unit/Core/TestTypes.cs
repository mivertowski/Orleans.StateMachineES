using System.Threading.Tasks;
using Orleans;
using Orleans.Runtime;
using Orleans.StateMachineES;
using Orleans.StateMachineES.Interfaces;
using Orleans.StateMachineES.Models;
using Stateless;

namespace Orleans.StateMachineES.Tests.Unit.Core;

// Test state and trigger enums
public enum TestState
{
    Idle,
    Active,
    Processing,
    Completed
}

public enum TestTrigger
{
    Start,
    Process,
    Complete
}

// Test grain interface
public interface IUnitTestGrain : IStateMachineGrain<TestState, TestTrigger>, IGrainWithStringKey
{
    Task StartAsync();
    Task ProcessAsync();
    Task CompleteAsync();
}

// Test grain implementation
[GrainType("UnitTestGrain")]
public class UnitTestGrain : StateMachineGrain<TestState, TestTrigger>, IUnitTestGrain
{
    protected override StateMachine<TestState, TestTrigger> BuildStateMachine()
    {
        var machine = new StateMachine<TestState, TestTrigger>(TestState.Idle);
        
        machine.Configure(TestState.Idle)
            .Permit(TestTrigger.Start, TestState.Active);
            
        machine.Configure(TestState.Active)
            .Permit(TestTrigger.Process, TestState.Processing);
            
        machine.Configure(TestState.Processing)
            .Permit(TestTrigger.Complete, TestState.Completed);
            
        machine.Configure(TestState.Completed);
        
        return machine;
    }
    
    public Task StartAsync()
    {
        return FireAsync(TestTrigger.Start);
    }
    
    public Task ProcessAsync()
    {
        return FireAsync(TestTrigger.Process);
    }
    
    public Task CompleteAsync()
    {
        return FireAsync(TestTrigger.Complete);
    }
}