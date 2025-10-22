namespace Orleans.StateMachineES.Tests.Cluster.Grains.Interfaces;

[Alias("Orleans.StateMachineES.Tests.Cluster.Grains.Interfaces.ITestGrain")]
public interface ITestGrain : IGrainWithStringKey
{
    [Alias("Do")]
    Task<string> Do(char input);
}