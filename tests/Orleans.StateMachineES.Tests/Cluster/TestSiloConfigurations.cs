using Orleans.TestingHost;
using Orleans.Hosting;
using Microsoft.Extensions.DependencyInjection;
using Orleans.Serialization;

namespace Orleans.StateMachineES.Tests.Cluster;

public class TestSiloConfigurations : ISiloConfigurator
{
    public void Configure(ISiloBuilder siloBuilder)
    {
        // Configure serialization for test assemblies
        siloBuilder.Services.AddSerializer(serializerBuilder =>
        {
            serializerBuilder.AddJsonSerializer(
                isSupported: type => type.Namespace?.StartsWith("Orleans.StateMachineES") == true);
        });
        
        // Configure default storage for JournaledGrain
        siloBuilder.AddMemoryGrainStorageAsDefault();
        
        // Configure event sourcing providers
        siloBuilder.AddLogStorageBasedLogConsistencyProvider();
        siloBuilder.AddStateStorageBasedLogConsistencyProvider();
        siloBuilder.AddMemoryGrainStorage("EventStore");
        siloBuilder.AddMemoryGrainStorage("LogStorage");
        
        // Configure memory streams with required PubSubStore
        siloBuilder.AddMemoryGrainStorage("PubSubStore");
        siloBuilder.AddMemoryStreams("SMS");
        
        // Configure reminders for timer tests
        siloBuilder.UseInMemoryReminderService();
        
        // Register versioning services
        siloBuilder.Services.AddSingleton<Orleans.StateMachineES.Versioning.IStateMachineDefinitionRegistry, 
            Orleans.StateMachineES.Versioning.StateMachineDefinitionRegistry>();
        siloBuilder.Services.AddSingleton<Orleans.StateMachineES.Versioning.IVersionCompatibilityChecker, 
            Orleans.StateMachineES.Versioning.VersionCompatibilityChecker>();
        siloBuilder.Services.AddSingleton<Orleans.StateMachineES.Versioning.MigrationHookManager>();
    }
}