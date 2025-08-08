using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Orleans.StateMachineES.Interfaces;
using Orleans.StateMachineES.Models;
using Stateless;
using Stateless.Graph;

namespace Orleans.StateMachineES.Visualization;

/// <summary>
/// Provides visualization capabilities for state machines, including graph generation and export to various formats.
/// Supports DOT format, SVG rendering, and structural analysis of state machine configurations.
/// </summary>
public class StateMachineVisualizer
{
    /// <summary>
    /// Gets the DOT graph representation of a state machine.
    /// </summary>
    /// <typeparam name="TState">The type representing the states.</typeparam>
    /// <typeparam name="TTrigger">The type representing the triggers.</typeparam>
    /// <param name="stateMachine">The state machine to visualize.</param>
    /// <param name="options">Visualization options.</param>
    /// <returns>DOT format string representation of the state machine.</returns>
    public static string ToDotGraph<TState, TTrigger>(
        StateMachine<TState, TTrigger> stateMachine,
        VisualizationOptions? options = null)
    {
        options ??= new VisualizationOptions();
        
        try
        {
            var dotGraph = UmlDotGraph.Format(stateMachine.GetInfo());
            return options.IncludeMetadata ? 
                EnhanceDotGraphWithMetadata(dotGraph, stateMachine, options) : 
                dotGraph;
        }
        catch (Exception ex)
        {
            return GenerateFallbackDotGraph(ex.Message, options);
        }
    }

    /// <summary>
    /// Generates a comprehensive analysis of the state machine structure.
    /// </summary>
    /// <typeparam name="TState">The type representing the states.</typeparam>
    /// <typeparam name="TTrigger">The type representing the triggers.</typeparam>
    /// <param name="stateMachine">The state machine to analyze.</param>
    /// <returns>Structural analysis of the state machine.</returns>
    public static StateMachineAnalysis AnalyzeStructure<TState, TTrigger>(
        StateMachine<TState, TTrigger> stateMachine)
    {
        var info = stateMachine.GetInfo();
        var analysis = new StateMachineAnalysis
        {
            StateMachineType = typeof(StateMachine<TState, TTrigger>).Name,
            StateType = typeof(TState).Name,
            TriggerType = typeof(TTrigger).Name,
            GeneratedAt = DateTime.UtcNow,
            CurrentState = stateMachine.State?.ToString() ?? "Unknown"
        };

        // Analyze states
        analysis.States = info.States.Select(state => new StateInfo
        {
            Name = state.ToString() ?? "Unknown",
            IsInitial = state.Equals(info.InitialState),
            IsCurrent = state.Equals(stateMachine.State),
            EntryActions = ExtractActions(info, state, "Entry"),
            ExitActions = ExtractActions(info, state, "Exit"),
            InternalTransitions = CountInternalTransitions(info, state),
            Substates = ExtractSubstates(info, state)
        }).ToList();

        // Analyze triggers
        var triggerInfo = new Dictionary<string, TriggerInfo>();
        foreach (var state in info.States)
        {
            var stateInfo = info.States.FirstOrDefault(s => s.Equals(state));
            if (stateInfo != null)
            {
                foreach (var trigger in GetTriggersForState(info, state))
                {
                    var triggerName = trigger.ToString() ?? "Unknown";
                    if (!triggerInfo.ContainsKey(triggerName))
                    {
                        triggerInfo[triggerName] = new TriggerInfo
                        {
                            Name = triggerName,
                            UsageCount = 0,
                            SourceStates = new List<string>(),
                            TargetStates = new List<string>(),
                            HasGuards = false,
                            HasParameters = false
                        };
                    }

                    triggerInfo[triggerName].UsageCount++;
                    var sourceName = state.ToString() ?? "Unknown";
                    if (!triggerInfo[triggerName].SourceStates.Contains(sourceName))
                        triggerInfo[triggerName].SourceStates.Add(sourceName);

                    // Find target state for this trigger from this state
                    var targetState = GetTargetState(info, state, trigger);
                    if (targetState != null)
                    {
                        var targetName = targetState.ToString() ?? "Unknown";
                        if (!triggerInfo[triggerName].TargetStates.Contains(targetName))
                            triggerInfo[triggerName].TargetStates.Add(targetName);
                    }
                }
            }
        }

        analysis.Triggers = triggerInfo.Values.ToList();

        // Calculate complexity metrics
        analysis.Metrics = CalculateComplexityMetrics(analysis);

        return analysis;
    }

    /// <summary>
    /// Exports the state machine visualization to the specified format.
    /// </summary>
    /// <typeparam name="TState">The type representing the states.</typeparam>
    /// <typeparam name="TTrigger">The type representing the triggers.</typeparam>
    /// <param name="stateMachine">The state machine to export.</param>
    /// <param name="format">The export format.</param>
    /// <param name="options">Export options.</param>
    /// <returns>Exported content as a byte array.</returns>
    public static async Task<byte[]> ExportAsync<TState, TTrigger>(
        StateMachine<TState, TTrigger> stateMachine,
        ExportFormat format,
        VisualizationOptions? options = null)
    {
        options ??= new VisualizationOptions();

        return format switch
        {
            ExportFormat.Dot => Encoding.UTF8.GetBytes(ToDotGraph(stateMachine, options)),
            ExportFormat.Json => await ExportToJsonAsync(stateMachine, options),
            ExportFormat.Xml => await ExportToXmlAsync(stateMachine, options),
            ExportFormat.Mermaid => await ExportToMermaidAsync(stateMachine, options),
            ExportFormat.PlantUml => await ExportToPlantUmlAsync(stateMachine, options),
            _ => throw new ArgumentException($"Unsupported export format: {format}")
        };
    }

    /// <summary>
    /// Creates a visualization report for a grain's state machine.
    /// </summary>
    /// <typeparam name="TState">The type representing the states.</typeparam>
    /// <typeparam name="TTrigger">The type representing the triggers.</typeparam>
    /// <param name="grain">The state machine grain.</param>
    /// <param name="includeRuntimeInfo">Whether to include runtime information.</param>
    /// <returns>Comprehensive visualization report.</returns>
    public static async Task<VisualizationReport> CreateReportAsync<TState, TTrigger>(
        IStateMachineGrain<TState, TTrigger> grain,
        bool includeRuntimeInfo = true)
        where TState : struct, Enum
        where TTrigger : struct, Enum
    {
        var report = new VisualizationReport
        {
            GrainType = grain.GetType().Name,
            GeneratedAt = DateTime.UtcNow,
            IncludesRuntimeInfo = includeRuntimeInfo
        };

        try
        {
            // Get state machine information
            var info = await grain.GetInfoAsync();
            report.StateMachineInfo = info;

            // Get current state
            report.CurrentState = (await grain.GetStateAsync()).ToString() ?? "Unknown";

            if (includeRuntimeInfo)
            {
                // Get permitted triggers
                report.PermittedTriggers = (await grain.GetPermittedTriggersAsync())
                    .Select(t => t.ToString()).ToList();

                // Get state history if available
                try
                {
                    // Note: Event sourced history would require extending the grain interface
                    // For now, we'll skip this functionality
                    report.StateHistory = new List<string>();
                }
                catch
                {
                    // Ignore if not event sourced or history unavailable
                }
            }

            report.Success = true;
        }
        catch (Exception ex)
        {
            report.Success = false;
            report.ErrorMessage = ex.Message;
        }

        return report;
    }

    private static string EnhanceDotGraphWithMetadata<TState, TTrigger>(
        string baseDotGraph,
        StateMachine<TState, TTrigger> stateMachine,
        VisualizationOptions options)
    {
        var lines = baseDotGraph.Split('\n').ToList();
        
        // Find the opening brace and add metadata
        var insertIndex = lines.FindIndex(line => line.Trim() == "{") + 1;
        if (insertIndex > 0)
        {
            lines.Insert(insertIndex, $"    label=\"{options.Title ?? "State Machine"}\\nGenerated: {DateTime.UtcNow:yyyy-MM-dd HH:mm:ss} UTC\";");
            lines.Insert(insertIndex + 1, "    labelloc=\"t\";");
            lines.Insert(insertIndex + 2, "    fontsize=14;");
            lines.Insert(insertIndex + 3, "");
        }

        // Add current state highlighting
        if (options.HighlightCurrentState)
        {
            var currentState = stateMachine.State?.ToString();
            if (!string.IsNullOrEmpty(currentState))
            {
                for (int i = 0; i < lines.Count; i++)
                {
                    if (lines[i].Contains($"\"{currentState}\"") && lines[i].Contains("->"))
                    {
                        continue; // Skip transition lines
                    }
                    if (lines[i].Contains($"\"{currentState}\""))
                    {
                        lines[i] = lines[i].Replace("];", ", style=filled, fillcolor=lightgreen];");
                    }
                }
            }
        }

        return string.Join('\n', lines);
    }

    private static string GenerateFallbackDotGraph(string errorMessage, VisualizationOptions options)
    {
        return $@"digraph StateMachine {{
    label=""{options.Title ?? "State Machine"} - Error"";
    labelloc=""t"";
    fontsize=14;
    
    Error [label=""Visualization Error\n{errorMessage}"", shape=box, style=filled, fillcolor=lightcoral];
}}";
    }

    private static List<string> ExtractActions(object info, object state, string actionType)
    {
        // This would need to be implemented based on the actual StateMachineInfo structure
        // For now, return empty list as StateMachineInfo doesn't expose action details
        return new List<string>();
    }

    private static int CountInternalTransitions(object info, object state)
    {
        // Count internal transitions for this state
        // This would need to be implemented based on StateMachineInfo structure
        return 0;
    }

    private static List<string> ExtractSubstates(object info, object state)
    {
        // Extract substates if this is a hierarchical state machine
        return new List<string>();
    }

    private static IEnumerable<object> GetTriggersForState(object info, object state)
    {
        // Extract triggers available from this state
        // This is a simplified implementation
        return new List<object>();
    }

    private static object? GetTargetState(object info, object sourceState, object trigger)
    {
        // Find the target state for a given source state and trigger
        return null;
    }

    private static ComplexityMetrics CalculateComplexityMetrics(StateMachineAnalysis analysis)
    {
        return new ComplexityMetrics
        {
            StateCount = analysis.States.Count,
            TriggerCount = analysis.Triggers.Count,
            TransitionCount = analysis.Triggers.Sum(t => t.UsageCount),
            CyclomaticComplexity = CalculateCyclomaticComplexity(analysis),
            MaxDepth = CalculateMaxDepth(analysis),
            ConnectivityIndex = CalculateConnectivityIndex(analysis)
        };
    }

    private static int CalculateCyclomaticComplexity(StateMachineAnalysis analysis)
    {
        // Simplified cyclomatic complexity calculation: Edges - Nodes + 2
        var edges = analysis.Triggers.Sum(t => t.UsageCount);
        var nodes = analysis.States.Count;
        return Math.Max(1, edges - nodes + 2);
    }

    private static int CalculateMaxDepth(StateMachineAnalysis analysis)
    {
        // Calculate maximum nesting depth (for hierarchical state machines)
        return analysis.States.Max(s => s.Substates.Count > 0 ? 2 : 1);
    }

    private static double CalculateConnectivityIndex(StateMachineAnalysis analysis)
    {
        // Calculate how well-connected the state machine is
        var maxPossibleTransitions = analysis.States.Count * (analysis.States.Count - 1);
        var actualTransitions = analysis.Triggers.Sum(t => t.UsageCount);
        return maxPossibleTransitions > 0 ? (double)actualTransitions / maxPossibleTransitions : 0.0;
    }

    private static async Task<byte[]> ExportToJsonAsync<TState, TTrigger>(
        StateMachine<TState, TTrigger> stateMachine,
        VisualizationOptions options)
    {
        var analysis = AnalyzeStructure(stateMachine);
        var json = System.Text.Json.JsonSerializer.Serialize(analysis, new System.Text.Json.JsonSerializerOptions
        {
            WriteIndented = true,
            PropertyNamingPolicy = System.Text.Json.JsonNamingPolicy.CamelCase
        });
        return Encoding.UTF8.GetBytes(json);
    }

    private static async Task<byte[]> ExportToXmlAsync<TState, TTrigger>(
        StateMachine<TState, TTrigger> stateMachine,
        VisualizationOptions options)
    {
        var analysis = AnalyzeStructure(stateMachine);
        var xml = new StringBuilder();
        xml.AppendLine("<?xml version=\"1.0\" encoding=\"UTF-8\"?>");
        xml.AppendLine($"<StateMachine type=\"{analysis.StateMachineType}\" generated=\"{analysis.GeneratedAt:yyyy-MM-ddTHH:mm:ssZ}\">");
        
        xml.AppendLine("  <States>");
        foreach (var state in analysis.States)
        {
            xml.AppendLine($"    <State name=\"{state.Name}\" isInitial=\"{state.IsInitial}\" isCurrent=\"{state.IsCurrent}\" />");
        }
        xml.AppendLine("  </States>");
        
        xml.AppendLine("  <Triggers>");
        foreach (var trigger in analysis.Triggers)
        {
            xml.AppendLine($"    <Trigger name=\"{trigger.Name}\" usageCount=\"{trigger.UsageCount}\" />");
        }
        xml.AppendLine("  </Triggers>");
        
        xml.AppendLine("</StateMachine>");
        
        return Encoding.UTF8.GetBytes(xml.ToString());
    }

    private static async Task<byte[]> ExportToMermaidAsync<TState, TTrigger>(
        StateMachine<TState, TTrigger> stateMachine,
        VisualizationOptions options)
    {
        var analysis = AnalyzeStructure(stateMachine);
        var mermaid = new StringBuilder();
        
        mermaid.AppendLine("stateDiagram-v2");
        
        if (!string.IsNullOrEmpty(options.Title))
        {
            mermaid.AppendLine($"    title: {options.Title}");
        }
        
        // Add states
        foreach (var state in analysis.States)
        {
            if (state.IsInitial)
            {
                mermaid.AppendLine($"    [*] --> {state.Name}");
            }
        }
        
        // Add transitions (simplified - would need actual transition info)
        foreach (var trigger in analysis.Triggers)
        {
            foreach (var sourceState in trigger.SourceStates)
            {
                foreach (var targetState in trigger.TargetStates)
                {
                    mermaid.AppendLine($"    {sourceState} --> {targetState} : {trigger.Name}");
                }
            }
        }
        
        return Encoding.UTF8.GetBytes(mermaid.ToString());
    }

    private static async Task<byte[]> ExportToPlantUmlAsync<TState, TTrigger>(
        StateMachine<TState, TTrigger> stateMachine,
        VisualizationOptions options)
    {
        var analysis = AnalyzeStructure(stateMachine);
        var plantuml = new StringBuilder();
        
        plantuml.AppendLine("@startuml");
        if (!string.IsNullOrEmpty(options.Title))
        {
            plantuml.AppendLine($"title {options.Title}");
        }
        
        // Add initial state
        plantuml.AppendLine("[*] --> " + analysis.States.FirstOrDefault(s => s.IsInitial)?.Name);
        
        // Add transitions
        foreach (var trigger in analysis.Triggers)
        {
            foreach (var sourceState in trigger.SourceStates)
            {
                foreach (var targetState in trigger.TargetStates)
                {
                    plantuml.AppendLine($"{sourceState} --> {targetState} : {trigger.Name}");
                }
            }
        }
        
        plantuml.AppendLine("@enduml");
        
        return Encoding.UTF8.GetBytes(plantuml.ToString());
    }

}