using Orleans.StateMachineES.Interfaces;

namespace Orleans.StateMachineES.Tests.Cluster.Grains.Interfaces;

// Use enums instead of string constants
public enum TestOrleansContextStates
{
    Initial,
    Active,
    Processing,
    Final
}

public enum TestOrleansContextTriggers
{
    Activate,
    Process,
    Complete,
    Reset,
    Deactivate
}

[Alias("Orleans.StateMachineES.Tests.Cluster.Grains.Interfaces.ITestOrleansContextGrain")]
public interface ITestOrleansContextGrain : IGrainWithStringKey, IStateMachineGrain<TestOrleansContextStates, TestOrleansContextTriggers>
{
    [Alias("GetExecutionLog")]
    Task<List<string>> GetExecutionLog();
    [Alias("ClearLog")]
    Task ClearLog();
}
