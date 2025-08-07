using Orleans.StateMachineES.Interfaces;

namespace Orleans.StateMachineES.Tests.Cluster.Grains.Interfaces;

public interface ITestStatelessGrain : IGrainWithStringKey, IStateMachineGrain<string, char>
{
    Task<string> DoSomethingElse(char input);
}

