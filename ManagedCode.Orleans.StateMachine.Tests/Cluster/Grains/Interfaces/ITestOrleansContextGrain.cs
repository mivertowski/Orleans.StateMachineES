using System.Collections.Generic;
using System.Threading.Tasks;
using ivlt.Orleans.StateMachineES.Interfaces;
using Orleans;

namespace ivlt.Orleans.StateMachineES.Tests.Cluster.Grains.Interfaces;

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

public interface ITestOrleansContextGrain : IGrainWithStringKey, IStateMachineGrain<TestOrleansContextStates, TestOrleansContextTriggers>
{
    Task<List<string>> GetExecutionLog();
    Task ClearLog();
}
