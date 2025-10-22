using BenchmarkDotNet.Configs;
using BenchmarkDotNet.Diagnosers;
using BenchmarkDotNet.Running;
using BenchmarkDotNet.Validators;

namespace Orleans.StateMachineES.Benchmarks;

/// <summary>
/// Entry point for running Orleans StateMachine ES performance benchmarks.
/// </summary>
public class Program
{
    public static void Main(string[] args)
    {
        Console.WriteLine("🚀 Orleans StateMachine ES Performance Benchmarks");
        Console.WriteLine("==================================================");
        Console.WriteLine();

        var config = DefaultConfig.Instance
            .AddDiagnoser(MemoryDiagnoser.Default)
            .AddDiagnoser(new DisassemblyDiagnoser(new DisassemblyDiagnoserConfig()))
            .AddValidator(ExecutionValidator.FailOnError)
            .WithOptions(ConfigOptions.DisableOptimizationsValidator);

        if (args.Length == 0)
        {
            Console.WriteLine("Available benchmark categories:");
            Console.WriteLine("  all           - Run all benchmarks");
            Console.WriteLine("  valuetask     - ValueTask vs Task performance");
            Console.WriteLine("  objectpool    - Object pooling performance");
            Console.WriteLine("  frozen        - FrozenDictionary/FrozenSet performance");
            Console.WriteLine("  stringintern  - String interning performance");
            Console.WriteLine();
            Console.WriteLine("Usage: dotnet run [benchmark-category]");
            Console.WriteLine("Example: dotnet run valuetask");
            return;
        }

        var benchmark = args[0].ToLowerInvariant();

        try
        {
            switch (benchmark)
            {
                case "all":
                    RunAllBenchmarks(config);
                    break;
                    
                case "valuetask":
                    BenchmarkRunner.Run<ValueTaskVsTaskBenchmarks>(config);
                    break;
                    
                case "objectpool":
                    BenchmarkRunner.Run<ObjectPoolBenchmarks>(config);
                    break;
                    
                case "frozen":
                    BenchmarkRunner.Run<FrozenCollectionBenchmarks>(config);
                    break;
                    
                case "stringintern":
                    BenchmarkRunner.Run<StringInternBenchmarks>(config);
                    break;
                    
                default:
                    Console.WriteLine($"Unknown benchmark: {benchmark}");
                    Console.WriteLine("Use 'all', 'valuetask', 'objectpool', 'frozen', or 'stringintern'");
                    return;
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"❌ Error running benchmarks: {ex.Message}");
            Console.WriteLine(ex.StackTrace);
        }

        Console.WriteLine();
        Console.WriteLine("✅ Benchmarks completed! Check the results above and BenchmarkDotNet artifacts folder.");
        Console.WriteLine();
        Console.WriteLine("Performance Summary:");
        Console.WriteLine("• ValueTask optimizations provide zero-allocation benefits for sync completions");
        Console.WriteLine("• Object pooling reduces GC pressure for frequently allocated collections");
        Console.WriteLine("• FrozenDictionary/FrozenSet provide superior lookup performance for static data");
        Console.WriteLine("• String interning reduces memory usage and improves comparison performance");
    }

    private static void RunAllBenchmarks(IConfig config)
    {
        Console.WriteLine("Running all benchmark suites...");
        Console.WriteLine();

        var benchmarkTypes = new[]
        {
            typeof(ValueTaskVsTaskBenchmarks),
            typeof(ObjectPoolBenchmarks),
            typeof(FrozenCollectionBenchmarks),
            typeof(StringInternBenchmarks)
        };

        foreach (var benchmarkType in benchmarkTypes)
        {
            Console.WriteLine($"🔄 Running {benchmarkType.Name}...");
            BenchmarkRunner.Run(benchmarkType, config);
            Console.WriteLine();
        }
    }
}