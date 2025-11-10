namespace Orleans.StateMachineES.Visualization;

/// <summary>
/// Configuration for batch visualization operations.
/// </summary>
public class BatchVisualizationOptions
{
    /// <summary>
    /// Formats to export to.
    /// </summary>
    public List<ExportFormat> Formats { get; set; } = [];

    /// <summary>
    /// Base output directory for batch exports.
    /// </summary>
    public string? OutputDirectory { get; set; }

    /// <summary>
    /// Filename prefix for generated files.
    /// </summary>
    public string FilePrefix { get; set; } = "StateMachine";

    /// <summary>
    /// Whether to include timestamp in filenames.
    /// </summary>
    public bool IncludeTimestampInFilename { get; set; } = true;

    /// <summary>
    /// Visualization options to apply to all exports.
    /// </summary>
    public VisualizationOptions VisualizationOptions { get; set; } = new();
}
