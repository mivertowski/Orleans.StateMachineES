using Orleans.TestingHost;
using Orleans.Hosting;

namespace ivlt.Orleans.StateMachineES.Tests.Cluster;

public class TestSiloConfigurations : ISiloConfigurator
{
    public void Configure(ISiloBuilder siloBuilder)
    {
        // Configure default storage for JournaledGrain
        siloBuilder.AddMemoryGrainStorageAsDefault();
        
        // Configure event sourcing providers
        siloBuilder.AddLogStorageBasedLogConsistencyProvider();
        siloBuilder.AddStateStorageBasedLogConsistencyProvider();
        siloBuilder.AddMemoryGrainStorage("EventStore");
        
        // Configure memory streams with required PubSubStore
        siloBuilder.AddMemoryGrainStorage("PubSubStore");
        siloBuilder.AddMemoryStreams("SMS");
    }
}