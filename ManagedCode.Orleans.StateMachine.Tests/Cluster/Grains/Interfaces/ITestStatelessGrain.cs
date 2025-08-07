using ivlt.Orleans.StateMachineES.Interfaces;

namespace ivlt.Orleans.StateMachineES.Tests.Cluster.Grains.Interfaces;

public interface ITestStatelessGrain : IGrainWithStringKey, IStateMachineGrain<string, char>
{
    Task<string> DoSomethingElse(char input);
}

