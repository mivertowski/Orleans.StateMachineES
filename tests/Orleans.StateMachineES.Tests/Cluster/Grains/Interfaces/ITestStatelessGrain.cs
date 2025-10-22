using Orleans.StateMachineES.Interfaces;

namespace Orleans.StateMachineES.Tests.Cluster.Grains.Interfaces;

[Alias("Orleans.StateMachineES.Tests.Cluster.Grains.Interfaces.ITestStatelessGrain")]
public interface ITestStatelessGrain : IGrainWithStringKey, IStateMachineGrain<string, char>
{
    [Alias("DoSomethingElse")]
    Task<string> DoSomethingElse(char input);
}

