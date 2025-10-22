using FluentAssertions;
using Orleans.StateMachineES.Tests.Cluster;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Xunit;
using Xunit.Abstractions;

namespace Orleans.StateMachineES.Tests.Timers;

[Collection(nameof(TestClusterApplication))]
public class SimpleTimerTests(TestClusterApplication testApp, ITestOutputHelper outputHelper)
{
    private readonly ITestOutputHelper _outputHelper = outputHelper;
    private readonly TestClusterApplication _testApp = testApp;

    [Fact]
    public async Task SimpleTimer_ShouldFire()
    {
        // Arrange
        var grain = _testApp.Cluster.Client.GetGrain<ISimpleTimerGrain>("timer-simple-1");

        // Act - Start timer
        await grain.StartTimerAsync();
        
        // Wait for timer to fire
        await Task.Delay(TimeSpan.FromSeconds(2.5));

        // Assert - Timer should have fired at least twice
        var count = await grain.GetTimerCountAsync();
        _outputHelper.WriteLine($"Timer fired {count} times");
        count.Should().BeGreaterThanOrEqualTo(2);
    }
}

[Alias("Orleans.StateMachineES.Tests.Timers.ISimpleTimerGrain")]
public interface ISimpleTimerGrain : IGrainWithStringKey
{
    [Alias("StartTimerAsync")]
    Task StartTimerAsync();
    [Alias("GetTimerCountAsync")]
    Task<int> GetTimerCountAsync();
}

public class SimpleTimerGrain : Grain, ISimpleTimerGrain
{
    private int _timerCount = 0;
    private IGrainTimer? _timer;
    private ILogger<SimpleTimerGrain>? _logger;

    public override Task OnActivateAsync(CancellationToken cancellationToken)
    {
        _logger = this.ServiceProvider.GetService<ILogger<SimpleTimerGrain>>();
        _logger?.LogInformation("SimpleTimerGrain activated");
        return base.OnActivateAsync(cancellationToken);
    }

    public Task StartTimerAsync()
    {
        _logger?.LogInformation("StartTimerAsync called");
        
        if (_timer != null)
        {
            _logger?.LogInformation("Timer already started");
            return Task.CompletedTask;
        }

        _timer = this.RegisterGrainTimer(
            async (cancellationToken) =>
            {
                _timerCount++;
                _logger?.LogInformation("Timer fired! Count: {Count}", _timerCount);
                await Task.CompletedTask;
            },
            new GrainTimerCreationOptions
            {
                DueTime = TimeSpan.FromSeconds(1),
                Period = TimeSpan.FromSeconds(1),
                Interleave = true
            });

        _logger?.LogInformation("Timer registered: {TimerNotNull}", _timer != null);
        return Task.CompletedTask;
    }

    public Task<int> GetTimerCountAsync()
    {
        _logger?.LogInformation("GetTimerCountAsync called, returning {Count}", _timerCount);
        return Task.FromResult(_timerCount);
    }

    public override Task OnDeactivateAsync(DeactivationReason reason, CancellationToken cancellationToken)
    {
        _timer?.Dispose();
        return base.OnDeactivateAsync(reason, cancellationToken);
    }
}