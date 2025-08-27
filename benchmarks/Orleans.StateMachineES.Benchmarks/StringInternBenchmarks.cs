using System;
using System.Linq;
using BenchmarkDotNet.Attributes;
using BenchmarkDotNet.Jobs;
using Orleans.StateMachineES.Extensions;

namespace Orleans.StateMachineES.Benchmarks;

/// <summary>
/// Benchmarks comparing string interning vs regular string operations.
/// Measures the impact of our string interning optimizations.
/// </summary>
[MemoryDiagnoser]
[SimpleJob(RuntimeMoniker.Net90)]
[CategoriesColumn]
[RankColumn]
public class StringInternBenchmarks
{
    private readonly StringInternPool _internPool = new(1000);
    private readonly string[] _commonStates;
    private readonly string[] _commonTriggers;
    private readonly string[] _dynamicStrings;

    public StringInternBenchmarks()
    {
        _commonStates = new[]
        {
            "Initial", "Active", "Processing", "Completed", "Failed", "Pending",
            "Draft", "Published", "Archived", "Cancelled", "Running", "Stopped"
        };

        _commonTriggers = new[]
        {
            "Start", "Stop", "Process", "Complete", "Fail", "Cancel",
            "Submit", "Approve", "Reject", "Reset", "Initialize", "Finalize"
        };

        _dynamicStrings = Enumerable.Range(0, 1000)
            .Select(i => $"DynamicState{i}")
            .ToArray();
    }

    [Benchmark(Baseline = true)]
    [BenchmarkCategory("States")]
    public string[] States_Regular()
    {
        var results = new string[1000];
        var random = new Random(42);
        
        for (int i = 0; i < 1000; i++)
        {
            var state = _commonStates[random.Next(_commonStates.Length)];
            results[i] = state;
        }
        
        return results;
    }

    [Benchmark]
    [BenchmarkCategory("States")]
    public string[] States_Interned()
    {
        var results = new string[1000];
        var random = new Random(42);
        
        for (int i = 0; i < 1000; i++)
        {
            var state = _commonStates[random.Next(_commonStates.Length)];
            results[i] = _internPool.InternState(state);
        }
        
        return results;
    }

    [Benchmark(Baseline = true)]
    [BenchmarkCategory("Triggers")]
    public string[] Triggers_Regular()
    {
        var results = new string[1000];
        var random = new Random(42);
        
        for (int i = 0; i < 1000; i++)
        {
            var trigger = _commonTriggers[random.Next(_commonTriggers.Length)];
            results[i] = trigger;
        }
        
        return results;
    }

    [Benchmark]
    [BenchmarkCategory("Triggers")]
    public string[] Triggers_Interned()
    {
        var results = new string[1000];
        var random = new Random(42);
        
        for (int i = 0; i < 1000; i++)
        {
            var trigger = _commonTriggers[random.Next(_commonTriggers.Length)];
            results[i] = _internPool.InternTrigger(trigger);
        }
        
        return results;
    }

    [Benchmark(Baseline = true)]
    [BenchmarkCategory("Comparison")]
    public bool String_Comparison_Regular()
    {
        var results = true;
        var random = new Random(42);
        
        for (int i = 0; i < 10000; i++)
        {
            var state1 = _commonStates[random.Next(_commonStates.Length)];
            var state2 = _commonStates[random.Next(_commonStates.Length)];
            results &= state1.Equals(state2, StringComparison.Ordinal);
        }
        
        return results;
    }

    [Benchmark]
    [BenchmarkCategory("Comparison")]
    public bool String_Comparison_Interned()
    {
        var results = true;
        var random = new Random(42);
        
        for (int i = 0; i < 10000; i++)
        {
            var state1 = _internPool.InternState(_commonStates[random.Next(_commonStates.Length)]);
            var state2 = _internPool.InternState(_commonStates[random.Next(_commonStates.Length)]);
            // Interned strings can use reference equality
            results &= ReferenceEquals(state1, state2);
        }
        
        return results;
    }

    [Benchmark(Baseline = true)]
    [BenchmarkCategory("Mixed")]
    public int Mixed_Operations_Regular()
    {
        int count = 0;
        var random = new Random(42);
        
        for (int i = 0; i < 5000; i++)
        {
            var state = _commonStates[random.Next(_commonStates.Length)];
            var trigger = _commonTriggers[random.Next(_commonTriggers.Length)];
            
            // Simulate typical operations
            if (state.Equals("Active", StringComparison.Ordinal))
                count++;
            if (trigger.Equals("Process", StringComparison.Ordinal))
                count++;
            
            // String concatenation
            var combined = state + "_" + trigger;
            count += combined.Length;
        }
        
        return count;
    }

    [Benchmark]
    [BenchmarkCategory("Mixed")]
    public int Mixed_Operations_Interned()
    {
        int count = 0;
        var random = new Random(42);
        var activeState = _internPool.InternState("Active");
        var processTrigger = _internPool.InternTrigger("Process");
        
        for (int i = 0; i < 5000; i++)
        {
            var state = _internPool.InternState(_commonStates[random.Next(_commonStates.Length)]);
            var trigger = _internPool.InternTrigger(_commonTriggers[random.Next(_commonTriggers.Length)]);
            
            // Use reference equality for interned strings
            if (ReferenceEquals(state, activeState))
                count++;
            if (ReferenceEquals(trigger, processTrigger))
                count++;
            
            // String concatenation (still needs allocation, but sources are interned)
            var combined = state + "_" + trigger;
            count += combined.Length;
        }
        
        return count;
    }

    [Benchmark]
    [BenchmarkCategory("Memory")]
    public void Memory_Pressure_Regular()
    {
        var random = new Random(42);
        
        // Create many duplicate strings
        for (int i = 0; i < 10000; i++)
        {
            var state = new string(_commonStates[random.Next(_commonStates.Length)]);
            var trigger = new string(_commonTriggers[random.Next(_commonTriggers.Length)]);
            
            // Use them briefly
            var length = state.Length + trigger.Length;
        }
    }

    [Benchmark(Baseline = true)]
    [BenchmarkCategory("Memory")]
    public void Memory_Pressure_Interned()
    {
        var random = new Random(42);
        
        // Intern many strings - should reuse memory
        for (int i = 0; i < 10000; i++)
        {
            var state = _internPool.InternState(_commonStates[random.Next(_commonStates.Length)]);
            var trigger = _internPool.InternTrigger(_commonTriggers[random.Next(_commonTriggers.Length)]);
            
            // Use them briefly
            var length = state.Length + trigger.Length;
        }
    }

    [Benchmark]
    [BenchmarkCategory("Dynamic")]
    public void Dynamic_Strings_WithPooling()
    {
        // Test with less common strings that may or may not benefit from pooling
        for (int i = 0; i < 1000; i++)
        {
            var str = _dynamicStrings[i];
            var interned = _internPool.Intern(str);
            
            // Use it
            var length = interned.Length;
        }
    }

    [Benchmark(Baseline = true)]
    [BenchmarkCategory("Dynamic")]
    public void Dynamic_Strings_WithoutPooling()
    {
        // Test with less common strings without pooling
        for (int i = 0; i < 1000; i++)
        {
            var str = _dynamicStrings[i];
            
            // Use it directly
            var length = str.Length;
        }
    }
}