using Orleans.TestingHost;

namespace ivlt.Orleans.StateMachineES.Tests.Cluster;

public class TestSiloConfigurations : ISiloConfigurator
{
    public void Configure(ISiloBuilder siloBuilder)
    {
        //siloBuilder.AddOrleansRateLimiting();
    }
}