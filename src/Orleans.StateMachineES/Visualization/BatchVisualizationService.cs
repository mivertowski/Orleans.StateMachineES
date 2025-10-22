using Orleans.StateMachineES.Interfaces;
using Stateless;

namespace Orleans.StateMachineES.Visualization;

/// <summary>
/// Service for batch processing and visualization of multiple state machines.
/// Provides capabilities for generating reports, comparisons, and bulk exports.
/// </summary>
public class BatchVisualizationService(string? outputDirectory = null)
{
    private readonly string _defaultOutputDirectory = outputDirectory ?? Path.Combine(Directory.GetCurrentDirectory(), "StateMachineVisualizations");

    /// <summary>
    /// Generates visualizations for multiple state machines in batch.
    /// </summary>
    /// <param name="stateMachines">Dictionary of state machine name to state machine instance.</param>
    /// <param name="options">Batch visualization options.</param>
    /// <returns>Results of the batch operation.</returns>
    public async Task<BatchVisualizationResult> GenerateBatchVisualizationsAsync<TState, TTrigger>(
        Dictionary<string, StateMachine<TState, TTrigger>> stateMachines,
        BatchVisualizationOptions? options = null)
    {
        options ??= new BatchVisualizationOptions();
        var outputDir = options.OutputDirectory ?? _defaultOutputDirectory;
        
        var result = new BatchVisualizationResult
        {
            OutputDirectory = outputDir,
            ProcessedAt = DateTime.UtcNow,
            TotalStateMachines = stateMachines.Count
        };

        // Ensure output directory exists
        Directory.CreateDirectory(outputDir);

        var tasks = new List<Task<SingleVisualizationResult>>();

        foreach (var kvp in stateMachines)
        {
            var name = kvp.Key;
            var stateMachine = kvp.Value;
            
            tasks.Add(GenerateSingleVisualizationAsync(name, stateMachine, options, outputDir));
        }

        var individualResults = await Task.WhenAll(tasks);
        result.IndividualResults = [.. individualResults];
        result.SuccessfulCount = individualResults.Count(r => r.Success);
        result.FailedCount = result.TotalStateMachines - result.SuccessfulCount;

        // Generate summary report
        await GenerateSummaryReportAsync(individualResults, options, outputDir);

        return result;
    }

    /// <summary>
    /// Generates a comparative analysis of multiple state machines.
    /// </summary>
    /// <param name="stateMachines">State machines to compare.</param>
    /// <param name="options">Comparison options.</param>
    /// <returns>Comparative analysis results.</returns>
    public Task<ComparisonReport> GenerateComparisonReportAsync<TState, TTrigger>(
        Dictionary<string, StateMachine<TState, TTrigger>> stateMachines,
        ComparisonOptions? options = null)
    {
        options ??= new ComparisonOptions();
        
        var report = new ComparisonReport
        {
            GeneratedAt = DateTime.UtcNow,
            StateMachineCount = stateMachines.Count,
            ComparisonCriteria = options.Criteria
        };

        var analyses = new Dictionary<string, StateMachineAnalysis>();
        
        // Analyze each state machine
        foreach (var kvp in stateMachines)
        {
            try
            {
                analyses[kvp.Key] = StateMachineVisualizer.AnalyzeStructure(kvp.Value);
            }
            catch (Exception ex)
            {
                report.Errors.Add($"Failed to analyze {kvp.Key}: {ex.Message}");
            }
        }

        report.Analyses = analyses;

        // Generate comparisons
        if (options.Criteria.HasFlag(ComparisonCriteria.Complexity))
        {
            report.ComplexityComparison = GenerateComplexityComparison(analyses);
        }

        if (options.Criteria.HasFlag(ComparisonCriteria.Structure))
        {
            report.StructuralComparison = GenerateStructuralComparison(analyses);
        }

        if (options.Criteria.HasFlag(ComparisonCriteria.Similarity))
        {
            report.SimilarityMatrix = GenerateSimilarityMatrix(analyses);
        }

        return Task.FromResult(report);
    }

    /// <summary>
    /// Generates visualizations for all grains of a specific type.
    /// </summary>
    /// <typeparam name="TGrain">The grain type to visualize.</typeparam>
    /// <typeparam name="TState">The state type.</typeparam>
    /// <typeparam name="TTrigger">The trigger type.</typeparam>
    /// <param name="grainFactory">Orleans grain factory.</param>
    /// <param name="grainIds">List of grain IDs to process.</param>
    /// <param name="options">Batch options.</param>
    /// <returns>Batch processing results.</returns>
    public async Task<BatchVisualizationResult> GenerateGrainVisualizationsAsync<TGrain, TState, TTrigger>(
        IGrainFactory grainFactory,
        IEnumerable<string> grainIds,
        BatchVisualizationOptions? options = null)
        where TGrain : class, IStateMachineGrain<TState, TTrigger>, IGrainWithStringKey
        where TState : struct, Enum
        where TTrigger : struct, Enum
    {
        options ??= new BatchVisualizationOptions();
        var outputDir = options.OutputDirectory ?? _defaultOutputDirectory;
        
        var result = new BatchVisualizationResult
        {
            OutputDirectory = outputDir,
            ProcessedAt = DateTime.UtcNow,
            TotalStateMachines = grainIds.Count()
        };

        Directory.CreateDirectory(outputDir);

        var tasks = new List<Task<SingleVisualizationResult>>();

        foreach (var grainId in grainIds)
        {
            tasks.Add(GenerateGrainVisualizationAsync<TGrain, TState, TTrigger>(
                grainFactory, grainId, options, outputDir));
        }

        var individualResults = await Task.WhenAll(tasks);
        result.IndividualResults = [.. individualResults];
        result.SuccessfulCount = individualResults.Count(r => r.Success);
        result.FailedCount = result.TotalStateMachines - result.SuccessfulCount;

        return result;
    }

    private async Task<SingleVisualizationResult> GenerateSingleVisualizationAsync<TState, TTrigger>(
        string name,
        StateMachine<TState, TTrigger> stateMachine,
        BatchVisualizationOptions options,
        string outputDir)
    {
        var result = new SingleVisualizationResult
        {
            Name = name,
            ProcessedAt = DateTime.UtcNow
        };

        try
        {
            var baseFilename = GenerateFilename(name, options);
            
            foreach (var format in options.Formats)
            {
                var filename = $"{baseFilename}.{format.ToString().ToLower()}";
                var filePath = Path.Combine(outputDir, filename);
                
                var content = await StateMachineVisualizer.ExportAsync(
                    stateMachine, format, options.VisualizationOptions);
                
                await File.WriteAllBytesAsync(filePath, content);
                result.GeneratedFiles.Add(filePath);
            }

            result.Success = true;
        }
        catch (Exception ex)
        {
            result.Success = false;
            result.ErrorMessage = ex.Message;
        }

        return result;
    }

    private async Task<SingleVisualizationResult> GenerateGrainVisualizationAsync<TGrain, TState, TTrigger>(
        IGrainFactory grainFactory,
        string grainId,
        BatchVisualizationOptions options,
        string outputDir)
        where TGrain : class, IStateMachineGrain<TState, TTrigger>, IGrainWithStringKey
        where TState : struct, Enum
        where TTrigger : struct, Enum
    {
        var result = new SingleVisualizationResult
        {
            Name = grainId,
            ProcessedAt = DateTime.UtcNow
        };

        try
        {
            var grain = grainFactory.GetGrain<TGrain>(grainId);
            var report = await StateMachineVisualizer.CreateReportAsync(grain, true);
            
            if (!report.Success)
            {
                result.Success = false;
                result.ErrorMessage = report.ErrorMessage ?? "Unknown error generating report";
                return result;
            }

            var baseFilename = GenerateFilename(grainId, options);
            
            // Generate report as JSON
            var reportJson = System.Text.Json.JsonSerializer.Serialize(report, new System.Text.Json.JsonSerializerOptions
            {
                WriteIndented = true,
                PropertyNamingPolicy = System.Text.Json.JsonNamingPolicy.CamelCase
            });
            
            var reportPath = Path.Combine(outputDir, $"{baseFilename}_report.json");
            await File.WriteAllTextAsync(reportPath, reportJson);
            result.GeneratedFiles.Add(reportPath);

            result.Success = true;
        }
        catch (Exception ex)
        {
            result.Success = false;
            result.ErrorMessage = ex.Message;
        }

        return result;
    }

    private static async Task GenerateSummaryReportAsync(
        IEnumerable<SingleVisualizationResult> results,
        BatchVisualizationOptions options,
        string outputDir)
    {
        var summary = new
        {
            GeneratedAt = DateTime.UtcNow,
            TotalProcessed = results.Count(),
            Successful = results.Count(r => r.Success),
            Failed = results.Count(r => !r.Success),
            OutputDirectory = outputDir,
            Results = results.Select(r => new
            {
                r.Name,
                r.Success,
                r.ErrorMessage,
                FileCount = r.GeneratedFiles.Count,
                Files = r.GeneratedFiles.Select(Path.GetFileName)
            })
        };

        var summaryJson = System.Text.Json.JsonSerializer.Serialize(summary, new System.Text.Json.JsonSerializerOptions
        {
            WriteIndented = true,
            PropertyNamingPolicy = System.Text.Json.JsonNamingPolicy.CamelCase
        });

        var summaryPath = Path.Combine(outputDir, "batch_summary.json");
        await File.WriteAllTextAsync(summaryPath, summaryJson);
    }

    private static string GenerateFilename(string baseName, BatchVisualizationOptions options)
    {
        var filename = $"{options.FilePrefix}_{baseName}";
        
        if (options.IncludeTimestampInFilename)
        {
            filename += $"_{DateTime.UtcNow:yyyyMMdd_HHmmss}";
        }

        return filename;
    }

    private static ComplexityComparison GenerateComplexityComparison(Dictionary<string, StateMachineAnalysis> analyses)
    {
        return new ComplexityComparison
        {
            MostComplex = analyses.OrderByDescending(a => a.Value.Metrics.CyclomaticComplexity).First().Key,
            LeastComplex = analyses.OrderBy(a => a.Value.Metrics.CyclomaticComplexity).First().Key,
            AverageComplexity = analyses.Values.Average(a => a.Metrics.CyclomaticComplexity),
            ComplexityDistribution = analyses.GroupBy(a => a.Value.Metrics.ComplexityLevel)
                .ToDictionary(g => g.Key, g => g.Count())
        };
    }

    private StructuralComparison GenerateStructuralComparison(Dictionary<string, StateMachineAnalysis> analyses)
    {
        return new StructuralComparison
        {
            StateCountRange = new Range(
                analyses.Values.Min(a => a.States.Count),
                analyses.Values.Max(a => a.States.Count)),
            TriggerCountRange = new Range(
                analyses.Values.Min(a => a.Triggers.Count),
                analyses.Values.Max(a => a.Triggers.Count)),
            TransitionCountRange = new Range(
                analyses.Values.Min(a => a.Metrics.TransitionCount),
                analyses.Values.Max(a => a.Metrics.TransitionCount)),
            CommonStates = FindCommonElements(analyses.Values.Select(a => a.States.Select(s => s.Name))),
            CommonTriggers = FindCommonElements(analyses.Values.Select(a => a.Triggers.Select(t => t.Name)))
        };
    }

    private SimilarityMatrix GenerateSimilarityMatrix(Dictionary<string, StateMachineAnalysis> analyses)
    {
        var matrix = new SimilarityMatrix();
        var names = analyses.Keys.ToList();

        for (int i = 0; i < names.Count; i++)
        {
            for (int j = i + 1; j < names.Count; j++)
            {
                var similarity = CalculateSimilarity(analyses[names[i]], analyses[names[j]]);
                matrix.Similarities[$"{names[i]} vs {names[j]}"] = similarity;
            }
        }

        return matrix;
    }

    private static double CalculateSimilarity(StateMachineAnalysis a1, StateMachineAnalysis a2)
    {
        // Simple similarity calculation based on structural properties
        var stateOverlap = a1.States.Select(s => s.Name).Intersect(a2.States.Select(s => s.Name)).Count();
        var triggerOverlap = a1.Triggers.Select(t => t.Name).Intersect(a2.Triggers.Select(t => t.Name)).Count();
        
        var totalStates = Math.Max(a1.States.Count, a2.States.Count);
        var totalTriggers = Math.Max(a1.Triggers.Count, a2.Triggers.Count);
        
        var stateSimilarity = totalStates > 0 ? (double)stateOverlap / totalStates : 1.0;
        var triggerSimilarity = totalTriggers > 0 ? (double)triggerOverlap / totalTriggers : 1.0;
        
        return (stateSimilarity + triggerSimilarity) / 2.0;
    }

    private static List<string> FindCommonElements(IEnumerable<IEnumerable<string>> collections)
    {
        return [.. collections.Aggregate((acc, next) => acc.Intersect(next))];
    }
}

/// <summary>
/// Result of a batch visualization operation.
/// </summary>
public class BatchVisualizationResult
{
    public string OutputDirectory { get; set; } = string.Empty;
    public DateTime ProcessedAt { get; set; }
    public int TotalStateMachines { get; set; }
    public int SuccessfulCount { get; set; }
    public int FailedCount { get; set; }
    public List<SingleVisualizationResult> IndividualResults { get; set; } = [];
}

/// <summary>
/// Result of processing a single state machine visualization.
/// </summary>
public class SingleVisualizationResult
{
    public string Name { get; set; } = string.Empty;
    public DateTime ProcessedAt { get; set; }
    public bool Success { get; set; }
    public string? ErrorMessage { get; set; }
    public List<string> GeneratedFiles { get; set; } = [];
}

/// <summary>
/// Options for comparing multiple state machines.
/// </summary>
public class ComparisonOptions
{
    public ComparisonCriteria Criteria { get; set; } = ComparisonCriteria.All;
}

/// <summary>
/// Criteria for comparing state machines.
/// </summary>
[Flags]
public enum ComparisonCriteria
{
    Complexity = 1,
    Structure = 2,
    Similarity = 4,
    All = Complexity | Structure | Similarity
}

/// <summary>
/// Report comparing multiple state machines.
/// </summary>
public class ComparisonReport
{
    public DateTime GeneratedAt { get; set; }
    public int StateMachineCount { get; set; }
    public ComparisonCriteria ComparisonCriteria { get; set; }
    public Dictionary<string, StateMachineAnalysis> Analyses { get; set; } = [];
    public ComplexityComparison? ComplexityComparison { get; set; }
    public StructuralComparison? StructuralComparison { get; set; }
    public SimilarityMatrix? SimilarityMatrix { get; set; }
    public List<string> Errors { get; set; } = [];
}

/// <summary>
/// Complexity comparison results.
/// </summary>
public class ComplexityComparison
{
    public string MostComplex { get; set; } = string.Empty;
    public string LeastComplex { get; set; } = string.Empty;
    public double AverageComplexity { get; set; }
    public Dictionary<ComplexityLevel, int> ComplexityDistribution { get; set; } = [];
}

/// <summary>
/// Structural comparison results.
/// </summary>
public class StructuralComparison
{
    public Range StateCountRange { get; set; } = new Range(0, 0);
    public Range TriggerCountRange { get; set; } = new Range(0, 0);
    public Range TransitionCountRange { get; set; } = new Range(0, 0);
    public List<string> CommonStates { get; set; } = [];
    public List<string> CommonTriggers { get; set; } = [];
}

/// <summary>
/// Similarity matrix between state machines.
/// </summary>
public class SimilarityMatrix
{
    public Dictionary<string, double> Similarities { get; set; } = [];
}

/// <summary>
/// Represents a range of values.
/// </summary>
public record Range(int Min, int Max);