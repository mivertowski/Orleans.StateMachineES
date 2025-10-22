using FluentAssertions;
using Orleans.StateMachineES.Visualization;
using Stateless;
using Xunit;

namespace Orleans.StateMachineES.Tests.Unit.Visualization;

public class BatchVisualizationServiceTests : IDisposable
{
    private readonly string _testOutputDirectory;
    private readonly BatchVisualizationService _service;
    
    // Test state and trigger enums
    private enum TestState { Idle, Active, Processing, Completed }
    private enum TestTrigger { Start, Process, Complete }

    public BatchVisualizationServiceTests()
    {
        _testOutputDirectory = Path.Combine(Path.GetTempPath(), $"BatchVizTest_{Guid.NewGuid()}");
        _service = new BatchVisualizationService(_testOutputDirectory);
    }

    public void Dispose()
    {
        // Clean up test output directory
        if (Directory.Exists(_testOutputDirectory))
        {
            Directory.Delete(_testOutputDirectory, recursive: true);
        }
    }

    private static StateMachine<TestState, TestTrigger> CreateTestStateMachine(TestState initialState = TestState.Idle)
    {
        var machine = new StateMachine<TestState, TestTrigger>(initialState);
        
        machine.Configure(TestState.Idle)
            .Permit(TestTrigger.Start, TestState.Active);
            
        machine.Configure(TestState.Active)
            .Permit(TestTrigger.Process, TestState.Processing);
            
        machine.Configure(TestState.Processing)
            .Permit(TestTrigger.Complete, TestState.Completed);
            
        machine.Configure(TestState.Completed);
        
        return machine;
    }

    [Fact]
    public async Task GenerateBatchVisualizationsAsync_WithMultipleStateMachines_ShouldGenerateFiles()
    {
        // Arrange
        var stateMachines = new Dictionary<string, StateMachine<TestState, TestTrigger>>
        {
            ["machine1"] = CreateTestStateMachine(TestState.Idle),
            ["machine2"] = CreateTestStateMachine(TestState.Active),
            ["machine3"] = CreateTestStateMachine(TestState.Processing)
        };

        var options = new BatchVisualizationOptions
        {
            Formats = [ExportFormat.Dot, ExportFormat.Json],
            FilePrefix = "test",
            IncludeTimestampInFilename = false,
            OutputDirectory = _testOutputDirectory
        };

        // Act
        var result = await _service.GenerateBatchVisualizationsAsync(stateMachines, options);

        // Assert
        result.Should().NotBeNull();
        result.TotalStateMachines.Should().Be(3);
        result.SuccessfulCount.Should().Be(3);
        result.FailedCount.Should().Be(0);
        result.ProcessedAt.Should().BeCloseTo(DateTime.UtcNow, TimeSpan.FromSeconds(5));
        result.OutputDirectory.Should().Be(_testOutputDirectory);
        
        // Verify files were created
        Directory.Exists(_testOutputDirectory).Should().BeTrue();
        var files = Directory.GetFiles(_testOutputDirectory);
        files.Should().NotBeEmpty();
        files.Should().Contain(f => f.Contains("machine1") && f.EndsWith(".dot"));
        files.Should().Contain(f => f.Contains("machine1") && f.EndsWith(".json"));
        
        // Verify summary report was created
        var summaryFile = Path.Combine(_testOutputDirectory, "batch_summary.json");
        File.Exists(summaryFile).Should().BeTrue();
    }

    [Fact]
    public async Task GenerateBatchVisualizationsAsync_WithEmptyDictionary_ShouldReturnEmptyResult()
    {
        // Arrange
        var stateMachines = new Dictionary<string, StateMachine<TestState, TestTrigger>>();

        // Act
        var result = await _service.GenerateBatchVisualizationsAsync(stateMachines);

        // Assert
        result.Should().NotBeNull();
        result.TotalStateMachines.Should().Be(0);
        result.SuccessfulCount.Should().Be(0);
        result.FailedCount.Should().Be(0);
    }

    [Fact]
    public async Task GenerateBatchVisualizationsAsync_WithTimestamp_ShouldIncludeTimestampInFilename()
    {
        // Arrange
        var stateMachines = new Dictionary<string, StateMachine<TestState, TestTrigger>>
        {
            ["timestamped"] = CreateTestStateMachine()
        };

        var options = new BatchVisualizationOptions
        {
            Formats = [ExportFormat.Dot],
            IncludeTimestampInFilename = true,
            FilePrefix = "ts"
        };

        // Act
        var result = await _service.GenerateBatchVisualizationsAsync(stateMachines, options);

        // Assert
        result.IndividualResults.Should().HaveCount(1);
        var generatedFiles = result.IndividualResults[0].GeneratedFiles;
        generatedFiles.Should().NotBeEmpty();
        
        // Check that filename contains timestamp pattern (yyyyMMdd_HHmmss)
        var filename = Path.GetFileName(generatedFiles[0]);
        filename.Should().MatchRegex(@"ts_timestamped_\d{8}_\d{6}\.dot");
    }

    [Fact]
    public async Task GenerateComparisonReportAsync_WithMultipleMachines_ShouldGenerateCompleteReport()
    {
        // Arrange
        var stateMachines = new Dictionary<string, StateMachine<TestState, TestTrigger>>
        {
            ["machine1"] = CreateTestStateMachine(TestState.Idle),
            ["machine2"] = CreateTestStateMachine(TestState.Active),
            ["machine3"] = CreateTestStateMachine(TestState.Processing)
        };

        var options = new ComparisonOptions
        {
            Criteria = ComparisonCriteria.All
        };

        // Act
        var report = await _service.GenerateComparisonReportAsync(stateMachines, options);

        // Assert
        report.Should().NotBeNull();
        report.StateMachineCount.Should().Be(3);
        report.GeneratedAt.Should().BeCloseTo(DateTime.UtcNow, TimeSpan.FromSeconds(5));
        report.ComparisonCriteria.Should().Be(ComparisonCriteria.All);
        report.Analyses.Should().HaveCount(3);
        report.Errors.Should().BeEmpty();
        
        // Verify complexity comparison
        report.ComplexityComparison.Should().NotBeNull();
        report.ComplexityComparison!.MostComplex.Should().NotBeNullOrEmpty();
        report.ComplexityComparison.LeastComplex.Should().NotBeNullOrEmpty();
        report.ComplexityComparison.AverageComplexity.Should().BeGreaterThan(0);
        
        // Verify structural comparison
        report.StructuralComparison.Should().NotBeNull();
        report.StructuralComparison!.StateCountRange.Min.Should().BeGreaterThan(0);
        report.StructuralComparison.StateCountRange.Max.Should().BeGreaterThanOrEqualTo(report.StructuralComparison.StateCountRange.Min);
        report.StructuralComparison.CommonStates.Should().NotBeNull();
        
        // Verify similarity matrix
        report.SimilarityMatrix.Should().NotBeNull();
        report.SimilarityMatrix!.Similarities.Should().NotBeEmpty();
        report.SimilarityMatrix.Similarities.Should().HaveCount(3); // 3 pairs from 3 machines
    }

    [Fact]
    public async Task GenerateComparisonReportAsync_WithComplexityOnly_ShouldGenerateComplexityComparison()
    {
        // Arrange
        var stateMachines = new Dictionary<string, StateMachine<TestState, TestTrigger>>
        {
            ["machine1"] = CreateTestStateMachine(),
            ["machine2"] = CreateTestStateMachine()
        };

        var options = new ComparisonOptions
        {
            Criteria = ComparisonCriteria.Complexity
        };

        // Act
        var report = await _service.GenerateComparisonReportAsync(stateMachines, options);

        // Assert
        report.ComplexityComparison.Should().NotBeNull();
        report.StructuralComparison.Should().BeNull();
        report.SimilarityMatrix.Should().BeNull();
    }

    [Fact]
    public async Task GenerateComparisonReportAsync_WithStructureOnly_ShouldGenerateStructuralComparison()
    {
        // Arrange
        var stateMachines = new Dictionary<string, StateMachine<TestState, TestTrigger>>
        {
            ["machine1"] = CreateTestStateMachine(),
            ["machine2"] = CreateTestStateMachine()
        };

        var options = new ComparisonOptions
        {
            Criteria = ComparisonCriteria.Structure
        };

        // Act
        var report = await _service.GenerateComparisonReportAsync(stateMachines, options);

        // Assert
        report.ComplexityComparison.Should().BeNull();
        report.StructuralComparison.Should().NotBeNull();
        report.SimilarityMatrix.Should().BeNull();
    }

    [Fact]
    public async Task GenerateComparisonReportAsync_WithSimilarityOnly_ShouldGenerateSimilarityMatrix()
    {
        // Arrange
        var stateMachines = new Dictionary<string, StateMachine<TestState, TestTrigger>>
        {
            ["machine1"] = CreateTestStateMachine(),
            ["machine2"] = CreateTestStateMachine()
        };

        var options = new ComparisonOptions
        {
            Criteria = ComparisonCriteria.Similarity
        };

        // Act
        var report = await _service.GenerateComparisonReportAsync(stateMachines, options);

        // Assert
        report.ComplexityComparison.Should().BeNull();
        report.StructuralComparison.Should().BeNull();
        report.SimilarityMatrix.Should().NotBeNull();
        report.SimilarityMatrix!.Similarities.Should().HaveCount(1);
        
        var similarity = report.SimilarityMatrix.Similarities.First().Value;
        similarity.Should().BeInRange(0.0, 1.0);
    }

    [Fact]
    public void Constructor_WithNullOutputDirectory_ShouldUseDefaultDirectory()
    {
        // Arrange & Act
        var service = new BatchVisualizationService();
        
        // Assert
        service.Should().NotBeNull();
        // The service should still work with default directory
    }

    [Fact]
    public void BatchVisualizationResult_Properties_ShouldWorkCorrectly()
    {
        // Arrange & Act
        var result = new BatchVisualizationResult
        {
            OutputDirectory = "/test/dir",
            ProcessedAt = DateTime.UtcNow,
            TotalStateMachines = 5,
            SuccessfulCount = 4,
            FailedCount = 1,
            IndividualResults =
            [
                new() { Name = "test1", Success = true },
                new() { Name = "test2", Success = false, ErrorMessage = "Failed" }
            ]
        };

        // Assert
        result.OutputDirectory.Should().Be("/test/dir");
        result.TotalStateMachines.Should().Be(5);
        result.SuccessfulCount.Should().Be(4);
        result.FailedCount.Should().Be(1);
        result.IndividualResults.Should().HaveCount(2);
    }

    [Fact]
    public void SingleVisualizationResult_Properties_ShouldWorkCorrectly()
    {
        // Arrange & Act
        var result = new SingleVisualizationResult
        {
            Name = "TestMachine",
            ProcessedAt = DateTime.UtcNow,
            Success = true,
            ErrorMessage = null,
            GeneratedFiles = ["file1.dot", "file2.json"]
        };

        // Assert
        result.Name.Should().Be("TestMachine");
        result.Success.Should().BeTrue();
        result.ErrorMessage.Should().BeNull();
        result.GeneratedFiles.Should().HaveCount(2);
    }

    [Fact]
    public void ComparisonCriteria_FlagsEnum_ShouldWorkCorrectly()
    {
        // Assert
        var all = ComparisonCriteria.All;
        all.Should().HaveFlag(ComparisonCriteria.Complexity);
        all.Should().HaveFlag(ComparisonCriteria.Structure);
        all.Should().HaveFlag(ComparisonCriteria.Similarity);
        
        var combined = ComparisonCriteria.Complexity | ComparisonCriteria.Structure;
        combined.Should().HaveFlag(ComparisonCriteria.Complexity);
        combined.Should().HaveFlag(ComparisonCriteria.Structure);
        combined.Should().NotHaveFlag(ComparisonCriteria.Similarity);
    }

    [Fact]
    public void Range_Record_ShouldWorkCorrectly()
    {
        // Arrange & Act
        var range = new Orleans.StateMachineES.Visualization.Range(5, 15);

        // Assert
        range.Min.Should().Be(5);
        range.Max.Should().Be(15);
        
        // Test record equality
        var sameRange = new Orleans.StateMachineES.Visualization.Range(5, 15);
        range.Should().Be(sameRange);
        
        var differentRange = new Orleans.StateMachineES.Visualization.Range(5, 20);
        range.Should().NotBe(differentRange);
    }
}