using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Diagnostics;

namespace Orleans.StateMachineES.Generators.Analyzers;

/// <summary>
/// Analyzer that detects circular state transitions with no exit path.
/// Warns about potential infinite loops in state machines.
/// </summary>
[DiagnosticAnalyzer(LanguageNames.CSharp)]
public class CircularTransitionAnalyzer : DiagnosticAnalyzer
{
    public const string DiagnosticId = "OSMES007";

    private static readonly LocalizableString Title =
        "Circular state transitions detected with no exit path";
    private static readonly LocalizableString MessageFormat =
        "States {0} form a circular transition chain with no exit path. Consider adding terminal states or exit transitions";
    private static readonly LocalizableString Description =
        "Circular state transitions (e.g., A→B→C→A) without any exit path can lead to infinite loops. " +
        "Ensure at least one state in the cycle has a transition to a state outside the cycle.";

    private const string Category = "Design";

    private static readonly DiagnosticDescriptor Rule = new DiagnosticDescriptor(
        DiagnosticId,
        Title,
        MessageFormat,
        Category,
        DiagnosticSeverity.Warning,
        isEnabledByDefault: true,
        description: Description,
        helpLinkUri: "https://github.com/mivertowski/Orleans.StateMachineES/docs/analyzers/OSMES007.md");

    public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics =>
        ImmutableArray.Create(Rule);

    public override void Initialize(AnalysisContext context)
    {
        context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);
        context.EnableConcurrentExecution();
        context.RegisterSyntaxNodeAction(AnalyzeBuildStateMachineMethod, SyntaxKind.MethodDeclaration);
    }

    private void AnalyzeBuildStateMachineMethod(SyntaxNodeAnalysisContext context)
    {
        var methodDeclaration = (MethodDeclarationSyntax)context.Node;

        // Check if this is BuildStateMachine method
        if (!AnalyzerHelpers.IsBuildStateMachineMethod(methodDeclaration))
            return;

        var semanticModel = context.SemanticModel;

        // Build transition graph
        var transitions = new Dictionary<string, HashSet<string>>();
        var stateLocations = new Dictionary<string, Location>();

        foreach (var statement in methodDeclaration.Body!.Statements)
        {
            if (statement is ExpressionStatementSyntax exprStmt)
            {
                AnalyzeStatementForTransitions(exprStmt.Expression, transitions, stateLocations, semanticModel);
            }
        }

        // Detect cycles using DFS
        var cycles = DetectCycles(transitions);

        // Report cycles with no exit paths
        foreach (var cycle in cycles)
        {
            if (!HasExitPath(cycle, transitions))
            {
                var cycleString = string.Join(" → ", cycle.Concat(new[] { cycle.First() }));
                var location = stateLocations.ContainsKey(cycle.First())
                    ? stateLocations[cycle.First()]
                    : Location.None;

                var diagnostic = Diagnostic.Create(Rule, location, cycleString);
                context.ReportDiagnostic(diagnostic);
            }
        }
    }

    private void AnalyzeStatementForTransitions(
        ExpressionSyntax expression,
        Dictionary<string, HashSet<string>> transitions,
        Dictionary<string, Location> stateLocations,
        SemanticModel semanticModel)
    {
        if (expression is not InvocationExpressionSyntax invocation)
            return;

        var methodName = AnalyzerHelpers.GetMethodName(invocation);

        if (methodName == "Configure")
        {
            var stateName = AnalyzerHelpers.ExtractStateFromConfigureCall(invocation);
            if (!string.IsNullOrEmpty(stateName))
            {
                if (!transitions.ContainsKey(stateName))
                {
                    transitions[stateName] = new HashSet<string>();
                }

                if (!stateLocations.ContainsKey(stateName))
                {
                    stateLocations[stateName] = invocation.GetLocation();
                }

                // Analyze chained Permit calls to find target states
                AnalyzeChainedPermits(invocation, stateName, transitions);
            }
        }
    }

    private void AnalyzeChainedPermits(
        InvocationExpressionSyntax invocation,
        string fromState,
        Dictionary<string, HashSet<string>> transitions)
    {
        var current = invocation.Parent;
        while (current is MemberAccessExpressionSyntax memberAccess)
        {
            if (memberAccess.Parent is InvocationExpressionSyntax parentInvocation)
            {
                var methodName = memberAccess.Name.ToString();

                if (methodName.StartsWith("Permit") && methodName != "PermitReentry")
                {
                    var targetState = AnalyzerHelpers.ExtractTargetStateFromPermit(parentInvocation);
                    if (!string.IsNullOrEmpty(targetState) && targetState != fromState)
                    {
                        transitions[fromState].Add(targetState);
                    }
                }

                current = parentInvocation.Parent;
            }
            else
            {
                break;
            }
        }
    }

    private List<List<string>> DetectCycles(Dictionary<string, HashSet<string>> transitions)
    {
        var cycles = new List<List<string>>();
        var visited = new HashSet<string>();
        var recStack = new HashSet<string>();
        var path = new List<string>();

        foreach (var state in transitions.Keys)
        {
            if (!visited.Contains(state))
            {
                DetectCyclesDFS(state, transitions, visited, recStack, path, cycles);
            }
        }

        return cycles;
    }

    private bool DetectCyclesDFS(
        string state,
        Dictionary<string, HashSet<string>> transitions,
        HashSet<string> visited,
        HashSet<string> recStack,
        List<string> path,
        List<List<string>> cycles)
    {
        visited.Add(state);
        recStack.Add(state);
        path.Add(state);

        if (transitions.ContainsKey(state))
        {
            foreach (var neighbor in transitions[state])
            {
                if (!visited.Contains(neighbor))
                {
                    if (DetectCyclesDFS(neighbor, transitions, visited, recStack, path, cycles))
                    {
                        return true;
                    }
                }
                else if (recStack.Contains(neighbor))
                {
                    // Found a cycle - extract it
                    var cycleStart = path.IndexOf(neighbor);
                    if (cycleStart >= 0)
                    {
                        var cycle = path.Skip(cycleStart).ToList();
                        // Only add if we haven't seen this cycle before (in any rotation)
                        if (!CycleExists(cycles, cycle))
                        {
                            cycles.Add(cycle);
                        }
                    }
                }
            }
        }

        path.RemoveAt(path.Count - 1);
        recStack.Remove(state);
        return false;
    }

    private bool CycleExists(List<List<string>> cycles, List<string> newCycle)
    {
        foreach (var existingCycle in cycles)
        {
            if (AreCyclesEquivalent(existingCycle, newCycle))
            {
                return true;
            }
        }
        return false;
    }

    private bool AreCyclesEquivalent(List<string> cycle1, List<string> cycle2)
    {
        if (cycle1.Count != cycle2.Count)
            return false;

        // Check all rotations
        for (int i = 0; i < cycle1.Count; i++)
        {
            bool match = true;
            for (int j = 0; j < cycle1.Count; j++)
            {
                if (cycle1[j] != cycle2[(i + j) % cycle2.Count])
                {
                    match = false;
                    break;
                }
            }
            if (match)
                return true;
        }
        return false;
    }

    private bool HasExitPath(List<string> cycle, Dictionary<string, HashSet<string>> transitions)
    {
        var cycleSet = new HashSet<string>(cycle);

        foreach (var state in cycle)
        {
            if (transitions.ContainsKey(state))
            {
                // Check if any transition leads outside the cycle
                if (transitions[state].Any(target => !cycleSet.Contains(target)))
                {
                    return true;
                }
            }
        }

        return false;
    }
}
