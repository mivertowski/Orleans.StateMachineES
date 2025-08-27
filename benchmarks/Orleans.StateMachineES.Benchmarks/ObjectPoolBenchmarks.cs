using System;
using System.Collections.Generic;
using BenchmarkDotNet.Attributes;
using BenchmarkDotNet.Jobs;
using Orleans.StateMachineES.Memory;

namespace Orleans.StateMachineES.Benchmarks;

/// <summary>
/// Benchmarks comparing object pooling vs traditional allocation patterns.
/// Measures the impact of our object pool optimizations.
/// </summary>
[MemoryDiagnoser]
[SimpleJob(RuntimeMoniker.Net90)]
[CategoriesColumn]
[RankColumn]
public class ObjectPoolBenchmarks
{
    private const int IterationCount = 10000;

    [Benchmark(Baseline = true)]
    [BenchmarkCategory("StringList")]
    public void StringList_Traditional()
    {
        for (int i = 0; i < IterationCount; i++)
        {
            var list = new List<string>();
            list.Add("State1");
            list.Add("State2");
            list.Add("State3");
            list.Clear();
        }
    }

    [Benchmark]
    [BenchmarkCategory("StringList")]
    public void StringList_Pooled()
    {
        for (int i = 0; i < IterationCount; i++)
        {
            var list = ObjectPools.StringListPool.Get();
            list.Add("State1");
            list.Add("State2");
            list.Add("State3");
            ObjectPools.StringListPool.Return(list);
        }
    }

    [Benchmark]
    [BenchmarkCategory("StringList")]
    public void StringList_PooledDisposable()
    {
        for (int i = 0; i < IterationCount; i++)
        {
            using var pooled = ObjectPools.StringListPool.GetDisposable();
            var list = pooled.Value;
            list.Add("State1");
            list.Add("State2");
            list.Add("State3");
        }
    }

    [Benchmark(Baseline = true)]
    [BenchmarkCategory("Dictionary")]
    public void Dictionary_Traditional()
    {
        for (int i = 0; i < IterationCount; i++)
        {
            var dict = new Dictionary<string, object>();
            dict["key1"] = "value1";
            dict["key2"] = "value2";
            dict["key3"] = "value3";
            dict.Clear();
        }
    }

    [Benchmark]
    [BenchmarkCategory("Dictionary")]
    public void Dictionary_Pooled()
    {
        for (int i = 0; i < IterationCount; i++)
        {
            var dict = ObjectPools.StringObjectDictionaryPool.Get();
            dict["key1"] = "value1";
            dict["key2"] = "value2";
            dict["key3"] = "value3";
            ObjectPools.StringObjectDictionaryPool.Return(dict);
        }
    }

    [Benchmark(Baseline = true)]
    [BenchmarkCategory("HashSet")]
    public void HashSet_Traditional()
    {
        for (int i = 0; i < IterationCount; i++)
        {
            var set = new HashSet<string>();
            set.Add("State1");
            set.Add("State2");
            set.Add("State3");
            set.Clear();
        }
    }

    [Benchmark]
    [BenchmarkCategory("HashSet")]
    public void HashSet_Pooled()
    {
        for (int i = 0; i < IterationCount; i++)
        {
            var set = ObjectPools.StringHashSetPool.Get();
            set.Add("State1");
            set.Add("State2");
            set.Add("State3");
            ObjectPools.StringHashSetPool.Return(set);
        }
    }

    [Benchmark(Baseline = true)]
    [BenchmarkCategory("Mixed")]
    public void MixedCollections_Traditional()
    {
        for (int i = 0; i < IterationCount; i++)
        {
            var list = new List<string>();
            var dict = new Dictionary<string, object>();
            var set = new HashSet<string>();
            
            list.Add("State");
            dict["key"] = "value";
            set.Add("Trigger");
            
            // Use the collections
            var hasState = set.Contains("Trigger");
            var value = dict.TryGetValue("key", out var v);
            var count = list.Count;
        }
    }

    [Benchmark]
    [BenchmarkCategory("Mixed")]
    public void MixedCollections_Pooled()
    {
        for (int i = 0; i < IterationCount; i++)
        {
            var list = ObjectPools.StringListPool.Get();
            var dict = ObjectPools.StringObjectDictionaryPool.Get();
            var set = ObjectPools.StringHashSetPool.Get();
            
            list.Add("State");
            dict["key"] = "value";
            set.Add("Trigger");
            
            // Use the collections
            var hasState = set.Contains("Trigger");
            var value = dict.TryGetValue("key", out var v);
            var count = list.Count;
            
            ObjectPools.StringListPool.Return(list);
            ObjectPools.StringObjectDictionaryPool.Return(dict);
            ObjectPools.StringHashSetPool.Return(set);
        }
    }

    [Benchmark]
    [BenchmarkCategory("ArrayPool")]
    public void ArrayPool_ByteOperations()
    {
        for (int i = 0; i < IterationCount; i++)
        {
            var buffer = ObjectPools.ByteArrayPool.Rent(1024);
            
            // Simulate some operations
            for (int j = 0; j < Math.Min(100, buffer.Length); j++)
            {
                buffer[j] = (byte)(j % 256);
            }
            
            ObjectPools.ByteArrayPool.Return(buffer);
        }
    }

    [Benchmark(Baseline = true)]
    [BenchmarkCategory("ArrayPool")]
    public void Array_ByteOperations_Traditional()
    {
        for (int i = 0; i < IterationCount; i++)
        {
            var buffer = new byte[1024];
            
            // Simulate some operations
            for (int j = 0; j < 100; j++)
            {
                buffer[j] = (byte)(j % 256);
            }
        }
    }
}