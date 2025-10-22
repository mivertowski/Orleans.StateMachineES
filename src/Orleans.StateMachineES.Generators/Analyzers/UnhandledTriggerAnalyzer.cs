using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Diagnostics;

namespace Orleans.StateMachineES.Generators.Analyzers;

/// <summary>
/// Analyzer that detects states with no configured trigger handlers.
/// States should handle at least one trigger or have an OnUnhandledTrigger callback.
/// </summary>
[DiagnosticAnalyzer(LanguageNames.CSharp)]
public class UnhandledTriggerAnalyzer : DiagnosticAnalyzer
{
    public const string DiagnosticId = "OSMES006";

    private static readonly LocalizableString Title =
        "State has no trigger handlers";
    private static readonly LocalizableString MessageFormat =
        "State '{0}' has no configured trigger handlers. Consider adding Permit/Ignore transitions or OnUnhandledTrigger handler.";
    private static readonly LocalizableString Description =
        "States should handle at least one trigger or configure an OnUnhandledTrigger callback. " +
        "States with no trigger handlers may cause runtime exceptions when triggers are fired.";

    private const string Category = "Usage";

    private static readonly DiagnosticDescriptor Rule = new DiagnosticDescriptor(
        DiagnosticId,
        Title,
        MessageFormat,
        Category,
        DiagnosticSeverity.Warning,
        isEnabledByDefault: true,
        description: Description,
        helpLinkUri: "https://github.com/mivertowski/Orleans.StateMachineES/docs/analyzers/OSMES006.md");

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

        // Track states and their trigger configurations
        var stateConfigurations = new Dictionary<string, StateConfiguration>();
        bool hasGlobalUnhandledTriggerHandler = false;

        // Analyze all Configure calls
        foreach (var statement in methodDeclaration.Body!.Statements)
        {
            if (statement is ExpressionStatementSyntax exprStmt)
            {
                AnalyzeStatementForTriggers(exprStmt.Expression, stateConfigurations,
                    ref hasGlobalUnhandledTriggerHandler, semanticModel);
            }
        }

        // Report states with no trigger handlers and no unhandled trigger callback
        foreach (var kvp in stateConfigurations)
        {
            var state = kvp.Key;
            var config = kvp.Value;

            if (!config.HasTriggerHandlers &&
                !config.HasOnUnhandledTrigger &&
                !hasGlobalUnhandledTriggerHandler)
            {
                if (config.Location != null)
                {
                    var diagnostic = Diagnostic.Create(Rule, config.Location, state);
                    context.ReportDiagnostic(diagnostic);
                }
            }
        }
    }

    private void AnalyzeStatementForTriggers(
        ExpressionSyntax expression,
        Dictionary<string, StateConfiguration> stateConfigurations,
        ref bool hasGlobalUnhandledTriggerHandler,
        SemanticModel semanticModel)
    {
        if (expression is InvocationExpressionSyntax invocation)
        {
            var methodName = AnalyzerHelpers.GetMethodName(invocation);

            // Check for Configure call
            if (methodName == "Configure")
            {
                var stateName = AnalyzerHelpers.ExtractStateFromConfigureCall(invocation);
                if (!string.IsNullOrEmpty(stateName))
                {
                    if (!stateConfigurations.ContainsKey(stateName!))
                    {
                        stateConfigurations[stateName!] = new StateConfiguration
                        {
                            Location = invocation.GetLocation()
                        };
                    }

                    // Analyze chained method calls
                    AnalyzeChainedCalls(invocation, stateName!, stateConfigurations,
                        ref hasGlobalUnhandledTriggerHandler);
                }
            }
            // Check for global OnUnhandledTrigger on state machine
            else if (methodName == "OnUnhandledTrigger")
            {
                hasGlobalUnhandledTriggerHandler = true;
            }
        }
    }

    private void AnalyzeChainedCalls(
        InvocationExpressionSyntax invocation,
        string stateName,
        Dictionary<string, StateConfiguration> stateConfigurations,
        ref bool hasGlobalUnhandledTriggerHandler)
    {
        var current = invocation.Parent;
        while (current is MemberAccessExpressionSyntax memberAccess)
        {
            if (memberAccess.Parent is InvocationExpressionSyntax parentInvocation)
            {
                var methodName = memberAccess.Name.ToString();

                // Check for trigger handling methods
                if (AnalyzerHelpers.IsTransitionConfigurationMethod(methodName))
                {
                    stateConfigurations[stateName].HasTriggerHandlers = true;
                }
                // Check for OnUnhandledTrigger on this state
                else if (methodName == "OnUnhandledTrigger")
                {
                    stateConfigurations[stateName].HasOnUnhandledTrigger = true;
                }

                current = parentInvocation.Parent;
            }
            else
            {
                break;
            }
        }
    }

    private class StateConfiguration
    {
        public bool HasTriggerHandlers { get; set; }
        public bool HasOnUnhandledTrigger { get; set; }
        public Location? Location { get; set; }
    }
}
