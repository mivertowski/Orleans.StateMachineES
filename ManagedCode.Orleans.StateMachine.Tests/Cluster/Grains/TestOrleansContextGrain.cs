using System.Collections.Generic;
using System.Threading.Tasks;
using ivlt.Orleans.StateMachineES;
using ivlt.Orleans.StateMachineES.Extensions;
using ivlt.Orleans.StateMachineES.Tests.Cluster.Grains.Interfaces;
using Stateless;

namespace ivlt.Orleans.StateMachineES.Tests.Cluster.Grains;

public class TestOrleansContextGrain : StateMachineGrain<TestOrleansContextStates, TestOrleansContextTriggers>, ITestOrleansContextGrain
{
    private readonly List<string> _executionLog = new();

    public Task<List<string>> GetExecutionLog()
    {
        return Task.FromResult(_executionLog);
    }

    public Task ClearLog()
    {
        _executionLog.Clear();
        return Task.CompletedTask;
    }

    private Task Log(string message)
    {
        _executionLog.Add(message);
        // Simulate async work
        return Task.Delay(1);
    }

    protected override StateMachine<TestOrleansContextStates, TestOrleansContextTriggers> BuildStateMachine()
    {
        var machine = new StateMachine<TestOrleansContextStates, TestOrleansContextTriggers>(TestOrleansContextStates.Initial);

        machine.Configure(TestOrleansContextStates.Initial)
            .OnActivateOrleansContextAsync(() => Log("Initial.Activate"))
            .OnExitOrleansContextAsync(() => Log("Initial.Exit"))
            .Permit(TestOrleansContextTriggers.Activate, TestOrleansContextStates.Active);

        machine.Configure(TestOrleansContextStates.Active)
            .OnEntryOrleansContextAsync(() => Log("Active.Entry"))
            .OnEntryFromOrleansContextAsync(TestOrleansContextTriggers.Activate, () => Log("Active.EntryFrom.Activate"))
            .OnEntryFromOrleansContextAsync(TestOrleansContextTriggers.Reset, (StateMachine<TestOrleansContextStates, TestOrleansContextTriggers>.Transition t) => Log($"Active.EntryFrom.Reset (via {t.Trigger})"))
            .OnExitOrleansContextAsync(() => Log("Active.Exit"))
            .OnActivateOrleansContextAsync(() => Log("Active.Activate"))
            .OnDeactivateOrleansContextAsync(() => Log("Active.Deactivate"))
            .Permit(TestOrleansContextTriggers.Process, TestOrleansContextStates.Processing)
            .Permit(TestOrleansContextTriggers.Deactivate, TestOrleansContextStates.Final);

        machine.Configure(TestOrleansContextStates.Processing)
            .OnEntryOrleansContextAsync((StateMachine<TestOrleansContextStates, TestOrleansContextTriggers>.Transition t) => Log($"Processing.Entry (via {t.Trigger})"))
            .OnEntryFromOrleansContextAsync(TestOrleansContextTriggers.Process, (StateMachine<TestOrleansContextStates, TestOrleansContextTriggers>.Transition t) => Log($"Processing.EntryFrom.Process (id:{t.Parameters?[0] ?? "?"})"))
            .OnExitOrleansContextAsync((StateMachine<TestOrleansContextStates, TestOrleansContextTriggers>.Transition t) => Log($"Processing.Exit (to {t.Destination})"))
            .Permit(TestOrleansContextTriggers.Complete, TestOrleansContextStates.Final)
            .Permit(TestOrleansContextTriggers.Reset, TestOrleansContextStates.Active);

        machine.Configure(TestOrleansContextStates.Final)
            .OnEntryOrleansContextAsync(() => Log("Final.Entry"))
            .OnEntryFromOrleansContextAsync(TestOrleansContextTriggers.Complete, (StateMachine<TestOrleansContextStates, TestOrleansContextTriggers>.Transition t) =>
            {
                var msg = t.Parameters?[0] ?? "?";
                var success = t.Parameters?.Length > 1 && t.Parameters[1] is bool b
                    ? b.ToString().ToLowerInvariant() // Ensure boolean is lowercase for logging
                    : "?";
                return Log($"Final.EntryFrom.Complete (msg:{msg}, success:{success})");
            })
            .Permit(TestOrleansContextTriggers.Reset, TestOrleansContextStates.Active)
            .OnExitOrleansContextAsync(() => Log("Final.Exit"))
            .OnActivateOrleansContextAsync(() => Log("Final.Activate"))
            .OnDeactivateOrleansContextAsync(() => Log("Final.Deactivate"));

        return machine;
    }
}
