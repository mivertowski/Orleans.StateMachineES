using System;
using System.Collections.Generic;
using System.Text;
using System.Threading.Tasks;
using Orleans.StateMachineES.Interfaces;
using Stateless;

namespace Orleans.StateMachineES.Visualization.Web;

/// <summary>
/// Web-based visualization service for state machines with interactive capabilities.
/// Generates HTML pages with JavaScript-based interactive diagrams.
/// </summary>
public class StateMachineWebVisualizer
{
    /// <summary>
    /// Generates an interactive HTML page for visualizing a state machine.
    /// </summary>
    /// <typeparam name="TState">The state type.</typeparam>
    /// <typeparam name="TTrigger">The trigger type.</typeparam>
    /// <param name="stateMachine">The state machine to visualize.</param>
    /// <param name="options">Web visualization options.</param>
    /// <returns>Complete HTML page content.</returns>
    public static string GenerateInteractiveHtml<TState, TTrigger>(
        StateMachine<TState, TTrigger> stateMachine,
        WebVisualizationOptions? options = null)
    {
        options ??= new WebVisualizationOptions();
        
        var analysis = StateMachineVisualizer.AnalyzeStructure(stateMachine);
        var htmlBuilder = new StringBuilder();
        
        // Generate HTML structure
        htmlBuilder.AppendLine("<!DOCTYPE html>");
        htmlBuilder.AppendLine("<html lang=\"en\">");
        htmlBuilder.AppendLine("<head>");
        htmlBuilder.AppendLine("    <meta charset=\"UTF-8\">");
        htmlBuilder.AppendLine("    <meta name=\"viewport\" content=\"width=device-width, initial-scale=1.0\">");
        htmlBuilder.AppendLine($"    <title>{options.Title ?? "State Machine Visualization"}</title>");
        
        // Add CSS styles
        htmlBuilder.AppendLine(GenerateCss(options));
        
        // Add required JavaScript libraries
        if (options.VisualizationLibrary == WebVisualizationLibrary.D3)
        {
            htmlBuilder.AppendLine("    <script src=\"https://d3js.org/d3.v7.min.js\"></script>");
        }
        else if (options.VisualizationLibrary == WebVisualizationLibrary.Cytoscape)
        {
            htmlBuilder.AppendLine("    <script src=\"https://unpkg.com/cytoscape/dist/cytoscape.min.js\"></script>");
        }
        else if (options.VisualizationLibrary == WebVisualizationLibrary.VisJS)
        {
            htmlBuilder.AppendLine("    <script src=\"https://unpkg.com/vis-network/standalone/umd/vis-network.min.js\"></script>");
        }
        
        htmlBuilder.AppendLine("</head>");
        htmlBuilder.AppendLine("<body>");
        
        // Generate HTML body
        htmlBuilder.AppendLine(GenerateHtmlBody(analysis, options));
        
        // Add JavaScript visualization code
        htmlBuilder.AppendLine(GenerateJavaScript(analysis, options));
        
        htmlBuilder.AppendLine("</body>");
        htmlBuilder.AppendLine("</html>");
        
        return htmlBuilder.ToString();
    }

    /// <summary>
    /// Generates a real-time visualization page that can connect to live grains.
    /// </summary>
    /// <typeparam name="TState">The state type.</typeparam>
    /// <typeparam name="TTrigger">The trigger type.</typeparam>
    /// <param name="grainType">The grain type name.</param>
    /// <param name="grainId">The grain ID.</param>
    /// <param name="options">Web visualization options.</param>
    /// <returns>HTML page with real-time capabilities.</returns>
    public static string GenerateRealTimeHtml<TState, TTrigger>(
        string grainType,
        string grainId,
        WebVisualizationOptions? options = null)
        where TState : struct, Enum
        where TTrigger : struct, Enum
    {
        options ??= new WebVisualizationOptions();
        
        var htmlBuilder = new StringBuilder();
        
        htmlBuilder.AppendLine("<!DOCTYPE html>");
        htmlBuilder.AppendLine("<html lang=\"en\">");
        htmlBuilder.AppendLine("<head>");
        htmlBuilder.AppendLine("    <meta charset=\"UTF-8\">");
        htmlBuilder.AppendLine("    <meta name=\"viewport\" content=\"width=device-width, initial-scale=1.0\">");
        htmlBuilder.AppendLine($"    <title>Real-time State Machine: {grainType}</title>");
        
        htmlBuilder.AppendLine(GenerateCss(options));
        htmlBuilder.AppendLine("    <script src=\"https://unpkg.com/vis-network/standalone/umd/vis-network.min.js\"></script>");
        
        htmlBuilder.AppendLine("</head>");
        htmlBuilder.AppendLine("<body>");
        
        // Real-time visualization body
        htmlBuilder.AppendLine($"    <div class=\"header\">");
        htmlBuilder.AppendLine($"        <h1>Real-time State Machine Visualization</h1>");
        htmlBuilder.AppendLine($"        <div class=\"grain-info\">");
        htmlBuilder.AppendLine($"            <span><strong>Grain Type:</strong> {grainType}</span>");
        htmlBuilder.AppendLine($"            <span><strong>Grain ID:</strong> {grainId}</span>");
        htmlBuilder.AppendLine($"            <span><strong>Status:</strong> <span id=\"connection-status\">Connecting...</span></span>");
        htmlBuilder.AppendLine($"        </div>");
        htmlBuilder.AppendLine($"    </div>");
        
        htmlBuilder.AppendLine("    <div class=\"controls\">");
        htmlBuilder.AppendLine("        <button onclick=\"refreshState()\">Refresh State</button>");
        htmlBuilder.AppendLine("        <button onclick=\"toggleAutoRefresh()\">Toggle Auto-refresh</button>");
        htmlBuilder.AppendLine("        <span>Current State: <strong id=\"current-state\">Loading...</strong></span>");
        htmlBuilder.AppendLine("    </div>");
        
        htmlBuilder.AppendLine("    <div id=\"visualization\"></div>");
        
        htmlBuilder.AppendLine("    <div class=\"info-panel\">");
        htmlBuilder.AppendLine("        <h3>Available Actions</h3>");
        htmlBuilder.AppendLine("        <div id=\"permitted-triggers\">Loading...</div>");
        htmlBuilder.AppendLine("        <h3>State History</h3>");
        htmlBuilder.AppendLine("        <div id=\"state-history\">Loading...</div>");
        htmlBuilder.AppendLine("    </div>");
        
        // Add real-time JavaScript
        htmlBuilder.AppendLine(GenerateRealTimeJavaScript(grainType, grainId, options));
        
        htmlBuilder.AppendLine("</body>");
        htmlBuilder.AppendLine("</html>");
        
        return htmlBuilder.ToString();
    }

    /// <summary>
    /// Generates a dashboard showing multiple state machines.
    /// </summary>
    /// <param name="stateMachines">Dictionary of state machine names and their analyses.</param>
    /// <param name="options">Dashboard options.</param>
    /// <returns>HTML dashboard content.</returns>
    public static string GenerateDashboardHtml(
        Dictionary<string, StateMachineAnalysis> stateMachines,
        DashboardOptions? options = null)
    {
        options ??= new DashboardOptions();
        
        var htmlBuilder = new StringBuilder();
        
        htmlBuilder.AppendLine("<!DOCTYPE html>");
        htmlBuilder.AppendLine("<html lang=\"en\">");
        htmlBuilder.AppendLine("<head>");
        htmlBuilder.AppendLine("    <meta charset=\"UTF-8\">");
        htmlBuilder.AppendLine("    <meta name=\"viewport\" content=\"width=device-width, initial-scale=1.0\">");
        htmlBuilder.AppendLine($"    <title>{options.Title ?? "State Machine Dashboard"}</title>");
        
        htmlBuilder.AppendLine(GenerateDashboardCss(options));
        htmlBuilder.AppendLine("    <script src=\"https://cdn.jsdelivr.net/npm/chart.js\"></script>");
        htmlBuilder.AppendLine("    <script src=\"https://unpkg.com/vis-network/standalone/umd/vis-network.min.js\"></script>");
        
        htmlBuilder.AppendLine("</head>");
        htmlBuilder.AppendLine("<body>");
        
        htmlBuilder.AppendLine("    <div class=\"dashboard\">");
        htmlBuilder.AppendLine($"        <h1>{options.Title ?? "State Machine Dashboard"}</h1>");
        
        // Overview statistics
        htmlBuilder.AppendLine("        <div class=\"overview\">");
        htmlBuilder.AppendLine($"            <div class=\"stat-card\">");
        htmlBuilder.AppendLine($"                <h3>Total State Machines</h3>");
        htmlBuilder.AppendLine($"                <div class=\"stat-value\">{stateMachines.Count}</div>");
        htmlBuilder.AppendLine($"            </div>");
        
        var totalStates = stateMachines.Values.Sum(sm => sm.States.Count);
        var totalTriggers = stateMachines.Values.Sum(sm => sm.Triggers.Count);
        var avgComplexity = stateMachines.Values.Average(sm => sm.Metrics.CyclomaticComplexity);
        
        htmlBuilder.AppendLine($"            <div class=\"stat-card\">");
        htmlBuilder.AppendLine($"                <h3>Total States</h3>");
        htmlBuilder.AppendLine($"                <div class=\"stat-value\">{totalStates}</div>");
        htmlBuilder.AppendLine($"            </div>");
        
        htmlBuilder.AppendLine($"            <div class=\"stat-card\">");
        htmlBuilder.AppendLine($"                <h3>Total Triggers</h3>");
        htmlBuilder.AppendLine($"                <div class=\"stat-value\">{totalTriggers}</div>");
        htmlBuilder.AppendLine($"            </div>");
        
        htmlBuilder.AppendLine($"            <div class=\"stat-card\">");
        htmlBuilder.AppendLine($"                <h3>Avg Complexity</h3>");
        htmlBuilder.AppendLine($"                <div class=\"stat-value\">{avgComplexity:F1}</div>");
        htmlBuilder.AppendLine($"            </div>");
        htmlBuilder.AppendLine("        </div>");
        
        // Charts section
        if (options.ShowCharts)
        {
            htmlBuilder.AppendLine("        <div class=\"charts-section\">");
            htmlBuilder.AppendLine("            <div class=\"chart-container\">");
            htmlBuilder.AppendLine("                <canvas id=\"complexityChart\"></canvas>");
            htmlBuilder.AppendLine("            </div>");
            htmlBuilder.AppendLine("            <div class=\"chart-container\">");
            htmlBuilder.AppendLine("                <canvas id=\"sizeChart\"></canvas>");
            htmlBuilder.AppendLine("            </div>");
            htmlBuilder.AppendLine("        </div>");
        }
        
        // State machines grid
        htmlBuilder.AppendLine("        <div class=\"state-machines-grid\">");
        foreach (var kvp in stateMachines)
        {
            htmlBuilder.AppendLine($"            <div class=\"state-machine-card\" onclick=\"showDetails('{kvp.Key}')\">");
            htmlBuilder.AppendLine($"                <h3>{kvp.Key}</h3>");
            htmlBuilder.AppendLine($"                <div class=\"mini-visualization\" id=\"mini-viz-{kvp.Key.Replace(" ", "_")}\"></div>");
            htmlBuilder.AppendLine($"                <div class=\"card-stats\">");
            htmlBuilder.AppendLine($"                    <span>States: {kvp.Value.States.Count}</span>");
            htmlBuilder.AppendLine($"                    <span>Triggers: {kvp.Value.Triggers.Count}</span>");
            htmlBuilder.AppendLine($"                    <span>Complexity: {kvp.Value.Metrics.ComplexityLevel}</span>");
            htmlBuilder.AppendLine($"                </div>");
            htmlBuilder.AppendLine($"            </div>");
        }
        htmlBuilder.AppendLine("        </div>");
        
        htmlBuilder.AppendLine("    </div>");
        
        // Add dashboard JavaScript
        htmlBuilder.AppendLine(GenerateDashboardJavaScript(stateMachines, options));
        
        htmlBuilder.AppendLine("</body>");
        htmlBuilder.AppendLine("</html>");
        
        return htmlBuilder.ToString();
    }

    private static string GenerateCss(WebVisualizationOptions options)
    {
        var theme = options.Theme;
        var css = new StringBuilder();
        
        css.AppendLine("    <style>");
        css.AppendLine("        * { margin: 0; padding: 0; box-sizing: border-box; }");
        css.AppendLine($"        body {{ font-family: 'Segoe UI', Tahoma, Geneva, Verdana, sans-serif; background-color: {theme.BackgroundColor}; color: {theme.TextColor}; }}");
        css.AppendLine("        .header { padding: 20px; border-bottom: 1px solid #ddd; }");
        css.AppendLine("        .header h1 { margin-bottom: 10px; }");
        css.AppendLine("        .grain-info { display: flex; gap: 20px; flex-wrap: wrap; }");
        css.AppendLine("        .controls { padding: 20px; border-bottom: 1px solid #ddd; display: flex; gap: 15px; align-items: center; }");
        css.AppendLine("        .controls button { padding: 8px 16px; border: none; border-radius: 4px; background: #007acc; color: white; cursor: pointer; }");
        css.AppendLine("        .controls button:hover { background: #005a9e; }");
        css.AppendLine("        #visualization { height: 600px; border: 1px solid #ddd; margin: 20px; }");
        css.AppendLine("        .info-panel { padding: 20px; margin: 20px; border: 1px solid #ddd; border-radius: 4px; }");
        css.AppendLine("        .info-panel h3 { margin-bottom: 10px; color: #333; }");
        css.AppendLine("        .trigger-button { padding: 5px 10px; margin: 3px; border: none; border-radius: 3px; background: #28a745; color: white; cursor: pointer; font-size: 12px; }");
        css.AppendLine("        .trigger-button:hover { background: #218838; }");
        css.AppendLine("        .history-item { padding: 5px; margin: 2px 0; background: #f8f9fa; border-radius: 3px; font-family: monospace; }");
        css.AppendLine("        #connection-status { color: #28a745; font-weight: bold; }");
        css.AppendLine("        .offline { color: #dc3545 !important; }");
        css.AppendLine("    </style>");
        
        return css.ToString();
    }

    private static string GenerateHtmlBody(StateMachineAnalysis analysis, WebVisualizationOptions options)
    {
        var body = new StringBuilder();
        
        body.AppendLine("    <div class=\"header\">");
        body.AppendLine($"        <h1>{options.Title ?? "State Machine Visualization"}</h1>");
        body.AppendLine($"        <p>Generated at: {analysis.GeneratedAt:yyyy-MM-dd HH:mm:ss UTC}</p>");
        body.AppendLine("    </div>");
        
        body.AppendLine("    <div id=\"visualization\"></div>");
        
        if (options.ShowStatistics)
        {
            body.AppendLine("    <div class=\"info-panel\">");
            body.AppendLine("        <h3>Statistics</h3>");
            body.AppendLine($"        <p>States: {analysis.States.Count}</p>");
            body.AppendLine($"        <p>Triggers: {analysis.Triggers.Count}</p>");
            body.AppendLine($"        <p>Transitions: {analysis.Metrics.TransitionCount}</p>");
            body.AppendLine($"        <p>Complexity: {analysis.Metrics.ComplexityLevel} (Score: {analysis.Metrics.CyclomaticComplexity})</p>");
            body.AppendLine("    </div>");
        }
        
        return body.ToString();
    }

    private static string GenerateJavaScript(StateMachineAnalysis analysis, WebVisualizationOptions options)
    {
        var js = new StringBuilder();
        js.AppendLine("    <script>");
        
        // Generate data structures for the visualization
        js.AppendLine("        const stateData = " + GenerateStateDataJson(analysis) + ";");
        js.AppendLine("        const transitionData = " + GenerateTransitionDataJson(analysis) + ";");
        
        // Generate visualization based on selected library
        js.AppendLine(options.VisualizationLibrary switch
        {
            WebVisualizationLibrary.VisJS => GenerateVisJSCode(),
            WebVisualizationLibrary.D3 => GenerateD3Code(),
            WebVisualizationLibrary.Cytoscape => GenerateCytoscapeCode(),
            _ => GenerateVisJSCode() // Default fallback
        });
        
        js.AppendLine("    </script>");
        
        return js.ToString();
    }

    private static string GenerateRealTimeJavaScript(string grainType, string grainId, WebVisualizationOptions options)
    {
        var js = new StringBuilder();
        js.AppendLine("    <script>");
        js.AppendLine("        let autoRefresh = false;");
        js.AppendLine("        let refreshInterval;");
        js.AppendLine("        let network;");
        js.AppendLine("");
        js.AppendLine("        function initializeVisualization() {");
        js.AppendLine("            // Initialize empty network");
        js.AppendLine("            const container = document.getElementById('visualization');");
        js.AppendLine("            const data = { nodes: [], edges: [] };");
        js.AppendLine("            const options = { physics: { enabled: true } };");
        js.AppendLine("            network = new vis.Network(container, data, options);");
        js.AppendLine("            refreshState();");
        js.AppendLine("        }");
        js.AppendLine("");
        js.AppendLine("        function refreshState() {");
        js.AppendLine("            // This would make an API call to get current state");
        js.AppendLine("            // For demo purposes, we'll simulate the response");
        js.AppendLine("            document.getElementById('connection-status').textContent = 'Connected';");
        js.AppendLine("            document.getElementById('current-state').textContent = 'Active';");
        js.AppendLine("        }");
        js.AppendLine("");
        js.AppendLine("        function toggleAutoRefresh() {");
        js.AppendLine("            autoRefresh = !autoRefresh;");
        js.AppendLine("            if (autoRefresh) {");
        js.AppendLine("                refreshInterval = setInterval(refreshState, 5000);");
        js.AppendLine("            } else {");
        js.AppendLine("                clearInterval(refreshInterval);");
        js.AppendLine("            }");
        js.AppendLine("        }");
        js.AppendLine("");
        js.AppendLine("        // Initialize when page loads");
        js.AppendLine("        window.addEventListener('load', initializeVisualization);");
        js.AppendLine("    </script>");
        
        return js.ToString();
    }

    private static string GenerateDashboardCss(DashboardOptions options)
    {
        var css = new StringBuilder();
        css.AppendLine("    <style>");
        css.AppendLine("        * { margin: 0; padding: 0; box-sizing: border-box; }");
        css.AppendLine("        body { font-family: 'Segoe UI', Tahoma, Geneva, Verdana, sans-serif; background: #f5f5f5; }");
        css.AppendLine("        .dashboard { max-width: 1200px; margin: 0 auto; padding: 20px; }");
        css.AppendLine("        .dashboard h1 { text-align: center; margin-bottom: 30px; color: #333; }");
        css.AppendLine("        .overview { display: grid; grid-template-columns: repeat(auto-fit, minmax(200px, 1fr)); gap: 20px; margin-bottom: 30px; }");
        css.AppendLine("        .stat-card { background: white; padding: 20px; border-radius: 8px; box-shadow: 0 2px 4px rgba(0,0,0,0.1); text-align: center; }");
        css.AppendLine("        .stat-card h3 { color: #666; margin-bottom: 10px; font-size: 14px; }");
        css.AppendLine("        .stat-value { font-size: 32px; font-weight: bold; color: #007acc; }");
        css.AppendLine("        .charts-section { display: grid; grid-template-columns: 1fr 1fr; gap: 20px; margin-bottom: 30px; }");
        css.AppendLine("        .chart-container { background: white; padding: 20px; border-radius: 8px; box-shadow: 0 2px 4px rgba(0,0,0,0.1); }");
        css.AppendLine("        .state-machines-grid { display: grid; grid-template-columns: repeat(auto-fill, minmax(300px, 1fr)); gap: 20px; }");
        css.AppendLine("        .state-machine-card { background: white; padding: 20px; border-radius: 8px; box-shadow: 0 2px 4px rgba(0,0,0,0.1); cursor: pointer; transition: transform 0.2s; }");
        css.AppendLine("        .state-machine-card:hover { transform: translateY(-2px); box-shadow: 0 4px 8px rgba(0,0,0,0.15); }");
        css.AppendLine("        .state-machine-card h3 { margin-bottom: 15px; color: #333; }");
        css.AppendLine("        .mini-visualization { height: 150px; border: 1px solid #ddd; border-radius: 4px; margin-bottom: 15px; }");
        css.AppendLine("        .card-stats { display: flex; justify-content: space-between; color: #666; font-size: 12px; }");
        css.AppendLine("    </style>");
        
        return css.ToString();
    }

    private static string GenerateDashboardJavaScript(Dictionary<string, StateMachineAnalysis> stateMachines, DashboardOptions options)
    {
        var js = new StringBuilder();
        js.AppendLine("    <script>");
        
        if (options.ShowCharts)
        {
            // Generate chart data
            js.AppendLine("        const complexityData = {");
            js.AppendLine($"            labels: {System.Text.Json.JsonSerializer.Serialize(stateMachines.Keys)},");
            js.AppendLine($"            datasets: [{{");
            js.AppendLine($"                label: 'Cyclomatic Complexity',");
            js.AppendLine($"                data: {System.Text.Json.JsonSerializer.Serialize(stateMachines.Values.Select(sm => sm.Metrics.CyclomaticComplexity))},");
            js.AppendLine($"                backgroundColor: 'rgba(0, 122, 204, 0.5)',");
            js.AppendLine($"                borderColor: 'rgba(0, 122, 204, 1)',");
            js.AppendLine($"                borderWidth: 1");
            js.AppendLine($"            }}]");
            js.AppendLine("        };");
            
            js.AppendLine("        const sizeData = {");
            js.AppendLine($"            labels: {System.Text.Json.JsonSerializer.Serialize(stateMachines.Keys)},");
            js.AppendLine($"            datasets: [{{");
            js.AppendLine($"                label: 'State Count',");
            js.AppendLine($"                data: {System.Text.Json.JsonSerializer.Serialize(stateMachines.Values.Select(sm => sm.States.Count))},");
            js.AppendLine($"                backgroundColor: 'rgba(40, 167, 69, 0.5)',");
            js.AppendLine($"                borderColor: 'rgba(40, 167, 69, 1)',");
            js.AppendLine($"                borderWidth: 1");
            js.AppendLine($"            }}]");
            js.AppendLine("        };");
            
            // Initialize charts
            js.AppendLine("        window.addEventListener('load', () => {");
            js.AppendLine("            new Chart(document.getElementById('complexityChart'), {");
            js.AppendLine("                type: 'bar',");
            js.AppendLine("                data: complexityData,");
            js.AppendLine("                options: { responsive: true, plugins: { title: { display: true, text: 'Complexity Comparison' } } }");
            js.AppendLine("            });");
            js.AppendLine("            new Chart(document.getElementById('sizeChart'), {");
            js.AppendLine("                type: 'bar',");
            js.AppendLine("                data: sizeData,");
            js.AppendLine("                options: { responsive: true, plugins: { title: { display: true, text: 'Size Comparison' } } }");
            js.AppendLine("            });");
            js.AppendLine("        });");
        }
        
        js.AppendLine("        function showDetails(name) {");
        js.AppendLine("            alert('Show details for: ' + name);");
        js.AppendLine("            // This would open a detailed view");
        js.AppendLine("        }");
        
        js.AppendLine("    </script>");
        
        return js.ToString();
    }

    private static string GenerateStateDataJson(StateMachineAnalysis analysis)
    {
        var states = analysis.States.Select(s => new
        {
            id = s.Name,
            label = s.Name,
            isInitial = s.IsInitial,
            isCurrent = s.IsCurrent,
            color = s.IsCurrent ? "#90EE90" : (s.IsInitial ? "#FFE4B5" : "#E0E0E0")
        });
        
        return System.Text.Json.JsonSerializer.Serialize(states);
    }

    private static string GenerateTransitionDataJson(StateMachineAnalysis analysis)
    {
        var transitions = new List<object>();
        
        foreach (var trigger in analysis.Triggers)
        {
            foreach (var source in trigger.SourceStates)
            {
                foreach (var target in trigger.TargetStates)
                {
                    transitions.Add(new
                    {
                        from = source,
                        to = target,
                        label = trigger.Name,
                        arrows = "to"
                    });
                }
            }
        }
        
        return System.Text.Json.JsonSerializer.Serialize(transitions);
    }

    private static string GenerateVisJSCode()
    {
        return @"
        const nodes = new vis.DataSet(stateData);
        const edges = new vis.DataSet(transitionData);
        const data = { nodes: nodes, edges: edges };
        const options = {
            physics: { enabled: true, stabilization: { iterations: 200 } },
            layout: { randomSeed: 2 },
            nodes: { shape: 'box', margin: 10, font: { size: 14 } },
            edges: { arrows: { to: { enabled: true } }, font: { size: 12 } }
        };
        const container = document.getElementById('visualization');
        const network = new vis.Network(container, data, options);";
    }

    private static string GenerateD3Code()
    {
        return @"
        // D3.js implementation would go here
        // This is a placeholder for D3-based visualization
        console.log('D3 visualization not implemented in this demo');";
    }

    private static string GenerateCytoscapeCode()
    {
        return @"
        // Cytoscape.js implementation would go here
        // This is a placeholder for Cytoscape-based visualization
        console.log('Cytoscape visualization not implemented in this demo');";
    }
}

/// <summary>
/// Options for web-based visualization.
/// </summary>
public class WebVisualizationOptions
{
    public string? Title { get; set; }
    public bool ShowStatistics { get; set; } = true;
    public WebVisualizationLibrary VisualizationLibrary { get; set; } = WebVisualizationLibrary.VisJS;
    public VisualizationTheme Theme { get; set; } = new();
}

/// <summary>
/// Options for dashboard generation.
/// </summary>
public class DashboardOptions
{
    public string? Title { get; set; }
    public bool ShowCharts { get; set; } = true;
    public bool ShowMiniVisualizations { get; set; } = true;
}

/// <summary>
/// Available JavaScript visualization libraries.
/// </summary>
public enum WebVisualizationLibrary
{
    VisJS,
    D3,
    Cytoscape
}

/// <summary>
/// Theme configuration for visualizations.
/// </summary>
public class VisualizationTheme
{
    public string BackgroundColor { get; set; } = "#ffffff";
    public string TextColor { get; set; } = "#333333";
    public string PrimaryColor { get; set; } = "#007acc";
    public string SecondaryColor { get; set; } = "#28a745";
    public string AccentColor { get; set; } = "#dc3545";
}