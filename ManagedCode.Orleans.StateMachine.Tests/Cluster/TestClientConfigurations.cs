using Microsoft.Extensions.Configuration;
using Orleans.TestingHost;

namespace ivlt.Orleans.StateMachineES.Tests.Cluster;

public class TestClientConfigurations : IClientBuilderConfigurator
{
    public void Configure(IConfiguration configuration, IClientBuilder clientBuilder)
    {
        //clientBuilder.();
    }
}