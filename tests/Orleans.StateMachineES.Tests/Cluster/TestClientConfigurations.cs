using Microsoft.Extensions.Configuration;
using Orleans.TestingHost;

namespace Orleans.StateMachineES.Tests.Cluster;

public class TestClientConfigurations : IClientBuilderConfigurator
{
    public void Configure(IConfiguration configuration, IClientBuilder clientBuilder)
    {
        //clientBuilder.();
    }
}