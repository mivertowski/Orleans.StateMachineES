using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Orleans.TestingHost;
using Orleans.Serialization;

namespace Orleans.StateMachineES.Tests.Cluster;

public class TestClientConfigurations : IClientBuilderConfigurator
{
    public void Configure(IConfiguration configuration, IClientBuilder clientBuilder)
    {
        // Configure serialization for test assemblies
        clientBuilder.Services.AddSerializer(serializerBuilder =>
        {
            serializerBuilder.AddJsonSerializer(
                isSupported: type => type.Namespace?.StartsWith("Orleans.StateMachineES") == true);
        });
        
        // Configure memory streams
        clientBuilder.AddMemoryStreams("SMS");
    }
}