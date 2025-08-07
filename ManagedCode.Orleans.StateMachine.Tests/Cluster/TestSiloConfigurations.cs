using Orleans.TestingHost;
using Orleans.Hosting;

namespace ivlt.Orleans.StateMachineES.Tests.Cluster;

public class TestSiloConfigurations : ISiloConfigurator
{
    public void Configure(ISiloBuilder siloBuilder)
    {
        // Configure event sourcing providers
        siloBuilder.AddLogStorageBasedLogConsistencyProvider();
        siloBuilder.AddStateStorageBasedLogConsistencyProvider();
        siloBuilder.AddMemoryGrainStorage("EventStore");
        siloBuilder.AddMemoryStreams("SMS");
    }
}