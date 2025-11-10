namespace Orleans.StateMachineES.Visualization;

/// <summary>
/// Options for customizing state machine visualization output.
/// </summary>
public class VisualizationOptions
{
    /// <summary>
    /// Title to display on the generated visualization.
    /// </summary>
    public string? Title { get; set; }

    /// <summary>
    /// Whether to include metadata such as generation timestamp.
    /// </summary>
    public bool IncludeMetadata { get; set; } = true;

    /// <summary>
    /// Whether to highlight the current state in the visualization.
    /// </summary>
    public bool HighlightCurrentState { get; set; } = true;

    /// <summary>
    /// Whether to show trigger parameters in the visualization.
    /// </summary>
    public bool ShowTriggerParameters { get; set; } = false;

    /// <summary>
    /// Whether to include guard conditions in the visualization.
    /// </summary>
    public bool ShowGuardConditions { get; set; } = false;

    /// <summary>
    /// Whether to show entry/exit actions for states.
    /// </summary>
    public bool ShowStateActions { get; set; } = false;

    /// <summary>
    /// Color scheme to use for the visualization.
    /// </summary>
    public VisualizationColorScheme ColorScheme { get; set; } = VisualizationColorScheme.Default;

    /// <summary>
    /// Layout direction for the graph.
    /// </summary>
    public GraphLayout Layout { get; set; } = GraphLayout.TopToBottom;
}

/// <summary>
/// Available color schemes for visualization.
/// </summary>
public enum VisualizationColorScheme
{
    Default,
    Professional,
    HighContrast,
    Pastel,
    Monochrome
}

/// <summary>
/// Available graph layout directions.
/// </summary>
public enum GraphLayout
{
    TopToBottom,
    LeftToRight,
    BottomToTop,
    RightToLeft
}

/// <summary>
/// Supported export formats for state machine visualization.
/// </summary>
public enum ExportFormat
{
    Dot,
    Json,
    Xml,
    Mermaid,
    PlantUml
}
