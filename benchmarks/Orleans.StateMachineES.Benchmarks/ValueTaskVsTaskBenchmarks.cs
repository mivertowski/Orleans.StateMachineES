using System;
using System.Threading.Tasks;
using BenchmarkDotNet.Attributes;
using BenchmarkDotNet.Jobs;
using Orleans.StateMachineES.Extensions;

namespace Orleans.StateMachineES.Benchmarks;

/// <summary>
/// Benchmarks comparing ValueTask vs Task performance for our core operations.
/// Measures the impact of our ValueTask optimizations.
/// </summary>
[MemoryDiagnoser]
[SimpleJob(RuntimeMoniker.Net90)]
[CategoriesColumn]
[RankColumn]
public class ValueTaskVsTaskBenchmarks
{
    private readonly TestState _state = TestState.Active;
    private readonly TestTrigger _trigger = TestTrigger.Process;

    [Benchmark(Baseline = true)]
    [BenchmarkCategory("State")]
    public async Task<TestState> GetState_Task()
    {
        return await Task.FromResult(_state);
    }

    [Benchmark]
    [BenchmarkCategory("State")]
    public async ValueTask<TestState> GetState_ValueTask()
    {
        return await ValueTaskExtensions.FromResult(_state);
    }

    [Benchmark]
    [BenchmarkCategory("State")]
    public ValueTask<TestState> GetState_ValueTask_Sync()
    {
        return ValueTaskExtensions.FromResult(_state);
    }

    [Benchmark(Baseline = true)]
    [BenchmarkCategory("Bool")]
    public async Task<bool> IsInState_Task()
    {
        return await Task.FromResult(_state == TestState.Active);
    }

    [Benchmark]
    [BenchmarkCategory("Bool")]
    public async ValueTask<bool> IsInState_ValueTask()
    {
        return await ValueTaskExtensions.FromResult(_state == TestState.Active);
    }

    [Benchmark]
    [BenchmarkCategory("Bool")]
    public ValueTask<bool> IsInState_ValueTask_Sync()
    {
        return ValueTaskExtensions.FromResult(_state == TestState.Active);
    }

    [Benchmark(Baseline = true)]
    [BenchmarkCategory("String")]
    public async Task<string> ToString_Task()
    {
        return await Task.FromResult(_state.ToString());
    }

    [Benchmark]
    [BenchmarkCategory("String")]
    public async ValueTask<string> ToString_ValueTask()
    {
        return await ValueTaskExtensions.FromResult(_state.ToString());
    }

    [Benchmark]
    [BenchmarkCategory("String")]
    public ValueTask<string> ToString_ValueTask_Sync()
    {
        return ValueTaskExtensions.FromResult(_state.ToString());
    }

    [Benchmark(Baseline = true)]
    [BenchmarkCategory("Bulk")]
    public async Task BulkOperations_Task()
    {
        for (int i = 0; i < 1000; i++)
        {
            var state = await Task.FromResult(_state);
            var canFire = await Task.FromResult(_trigger == TestTrigger.Process);
            var stateStr = await Task.FromResult(state.ToString());
        }
    }

    [Benchmark]
    [BenchmarkCategory("Bulk")]
    public async Task BulkOperations_ValueTask()
    {
        for (int i = 0; i < 1000; i++)
        {
            var state = await ValueTaskExtensions.FromResult(_state);
            var canFire = await ValueTaskExtensions.FromResult(_trigger == TestTrigger.Process);
            var stateStr = await ValueTaskExtensions.FromResult(state.ToString());
        }
    }

    [Benchmark]
    [BenchmarkCategory("Bulk")]
    public async Task BulkOperations_ValueTask_Sync()
    {
        for (int i = 0; i < 1000; i++)
        {
            var state = ValueTaskExtensions.FromResult(_state);
            var canFire = ValueTaskExtensions.FromResult(_trigger == TestTrigger.Process);
            var stateStr = ValueTaskExtensions.FromResult(state.Result.ToString());
            
            // Simulate some async work
            await Task.Yield();
        }
    }
}

public enum TestState
{
    Initial,
    Active,
    Processing,
    Completed,
    Failed
}

public enum TestTrigger
{
    Start,
    Process,
    Complete,
    Fail,
    Reset
}