using System;
using System.Collections.Frozen;
using System.Collections.Generic;
using System.Linq;
using BenchmarkDotNet.Attributes;
using BenchmarkDotNet.Jobs;
using Orleans.StateMachineES.Memory;

namespace Orleans.StateMachineES.Benchmarks;

/// <summary>
/// Benchmarks comparing FrozenDictionary/FrozenSet vs traditional collections.
/// Measures the impact of our frozen collection optimizations.
/// </summary>
[MemoryDiagnoser]
[SimpleJob(RuntimeMoniker.Net90)]
[CategoriesColumn]
[RankColumn]
public class FrozenCollectionBenchmarks
{
    private readonly Dictionary<string, string> _regularDict;
    private readonly FrozenDictionary<string, string> _frozenDict;
    private readonly HashSet<string> _regularSet;
    private readonly FrozenSet<string> _frozenSet;
    private readonly string[] _lookupKeys;
    private readonly string[] _lookupValues;

    public FrozenCollectionBenchmarks()
    {
        // Create test data
        var testData = Enumerable.Range(0, 1000)
            .Select(i => new KeyValuePair<string, string>($"Key{i}", $"Value{i}"))
            .ToArray();

        _regularDict = new Dictionary<string, string>(testData);
        _frozenDict = System.Collections.Frozen.FrozenDictionary.ToFrozenDictionary(testData);

        _regularSet = new HashSet<string>(testData.Select(kvp => kvp.Key));
        _frozenSet = System.Collections.Frozen.FrozenSet.ToFrozenSet(testData.Select(kvp => kvp.Key));

        // Keys for lookup tests
        _lookupKeys = testData.Take(100).Select(kvp => kvp.Key).ToArray();
        _lookupValues = testData.Take(100).Select(kvp => kvp.Value).ToArray();
    }

    [Benchmark(Baseline = true)]
    [BenchmarkCategory("Dictionary")]
    public void Dictionary_Lookup_Regular()
    {
        foreach (var key in _lookupKeys)
        {
            _regularDict.TryGetValue(key, out var value);
        }
    }

    [Benchmark]
    [BenchmarkCategory("Dictionary")]
    public void Dictionary_Lookup_Frozen()
    {
        foreach (var key in _lookupKeys)
        {
            _frozenDict.TryGetValue(key, out var value);
        }
    }

    [Benchmark]
    [BenchmarkCategory("Dictionary")]
    public void Dictionary_Lookup_StateMachineFrozen()
    {
        // Use our pre-built frozen error messages
        var keys = new[] { "INVALID_TRANSITION", "STATE_NOT_FOUND", "GUARD_CONDITION_FAILED", "SERIALIZATION_ERROR" };
        foreach (var key in keys)
        {
            StateMachineFrozenCollections.CommonErrorMessages.TryGetValue(key, out var value);
        }
    }

    [Benchmark(Baseline = true)]
    [BenchmarkCategory("Set")]
    public void Set_Lookup_Regular()
    {
        foreach (var key in _lookupKeys)
        {
            _regularSet.Contains(key);
        }
    }

    [Benchmark]
    [BenchmarkCategory("Set")]
    public void Set_Lookup_Frozen()
    {
        foreach (var key in _lookupKeys)
        {
            _frozenSet.Contains(key);
        }
    }

    [Benchmark]
    [BenchmarkCategory("Set")]
    public void Set_Lookup_StateMachineFrozen()
    {
        // Use our pre-built frozen state names
        var states = new[] { "Initial", "Active", "Processing", "Completed", "Failed", "Pending" };
        foreach (var state in states)
        {
            StateMachineFrozenCollections.CommonStateNames.Contains(state);
        }
    }

    [Benchmark(Baseline = true)]
    [BenchmarkCategory("Iteration")]
    public int Dictionary_Iteration_Regular()
    {
        int count = 0;
        foreach (var kvp in _regularDict)
        {
            count += kvp.Key.Length + kvp.Value.Length;
        }
        return count;
    }

    [Benchmark]
    [BenchmarkCategory("Iteration")]
    public int Dictionary_Iteration_Frozen()
    {
        int count = 0;
        foreach (var kvp in _frozenDict)
        {
            count += kvp.Key.Length + kvp.Value.Length;
        }
        return count;
    }

    [Benchmark(Baseline = true)]
    [BenchmarkCategory("Creation")]
    public Dictionary<string, string> Dictionary_Creation_Regular()
    {
        var dict = new Dictionary<string, string>();
        for (int i = 0; i < 100; i++)
        {
            dict[$"Key{i}"] = $"Value{i}";
        }
        return dict;
    }

    [Benchmark]
    [BenchmarkCategory("Creation")]
    public FrozenDictionary<string, string> Dictionary_Creation_Frozen()
    {
        var builder = new FrozenDictionaryBuilder<string, string>(100);
        for (int i = 0; i < 100; i++)
        {
            builder.Add($"Key{i}", $"Value{i}");
        }
        return builder.Build();
    }

    [Benchmark]
    [BenchmarkCategory("Creation")]
    public FrozenDictionary<string, string> Dictionary_Creation_FrozenDirect()
    {
        var pairs = Enumerable.Range(0, 100)
            .Select(i => new KeyValuePair<string, string>($"Key{i}", $"Value{i}"));
        return System.Collections.Frozen.FrozenDictionary.ToFrozenDictionary(pairs);
    }

    [Benchmark(Baseline = true)]
    [BenchmarkCategory("Mixed")]
    public bool Mixed_Operations_Regular()
    {
        // Simulate typical state machine lookups
        var hasError = _regularDict.ContainsKey("INVALID_TRANSITION");
        var hasState = _regularSet.Contains("Active");
        var errorMsg = _regularDict.TryGetValue("GUARD_CONDITION_FAILED", out var msg);
        
        return hasError && hasState && errorMsg;
    }

    [Benchmark]
    [BenchmarkCategory("Mixed")]
    public bool Mixed_Operations_Frozen()
    {
        // Use our optimized frozen collections
        var hasError = StateMachineFrozenCollections.CommonErrorMessages.ContainsKey("INVALID_TRANSITION");
        var hasState = StateMachineFrozenCollections.CommonStateNames.Contains("Active");
        var errorMsg = StateMachineFrozenCollections.CommonErrorMessages.TryGetValue("GUARD_CONDITION_FAILED", out var msg);
        
        return hasError && hasState && errorMsg;
    }

    [Benchmark]
    [BenchmarkCategory("Http")]
    public string Http_Status_Mapping_Frozen()
    {
        var statuses = new[] { 200, 201, 400, 401, 404, 500, 502, 503 };
        var lastState = "";
        
        foreach (var status in statuses)
        {
            if (StateMachineFrozenCollections.HttpStatusToStateMapping.TryGetValue(status, out var state))
            {
                lastState = state;
            }
        }
        
        return lastState;
    }

    [Benchmark(Baseline = true)]
    [BenchmarkCategory("Http")]
    public string Http_Status_Mapping_Regular()
    {
        var statusMapping = new Dictionary<int, string>
        {
            [200] = "Completed", [201] = "Created", [400] = "Invalid", [401] = "Unauthorized",
            [404] = "NotFound", [500] = "Error", [502] = "ServiceUnavailable", [503] = "Unavailable"
        };
        
        var statuses = new[] { 200, 201, 400, 401, 404, 500, 502, 503 };
        var lastState = "";
        
        foreach (var status in statuses)
        {
            if (statusMapping.TryGetValue(status, out var state))
            {
                lastState = state;
            }
        }
        
        return lastState;
    }
}