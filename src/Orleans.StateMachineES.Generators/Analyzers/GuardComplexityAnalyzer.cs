using System.Collections.Immutable;
using System.Linq;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Diagnostics;

namespace Orleans.StateMachineES.Generators.Analyzers;

/// <summary>
/// Analyzer that detects overly complex guard conditions.
/// Complex guards are harder to test, maintain, and understand.
/// </summary>
[DiagnosticAnalyzer(LanguageNames.CSharp)]
public class GuardComplexityAnalyzer : DiagnosticAnalyzer
{
    public const string DiagnosticId = "OSMES008";

    private const int MaxComplexityThreshold = 5;
    private const int MaxNestingDepth = 3;

    private static readonly LocalizableString Title =
        "Guard condition is too complex";
    private static readonly LocalizableString MessageFormat =
        "Guard condition has cyclomatic complexity of {0} (threshold: {1}). Consider extracting to a named method";
    private static readonly LocalizableString Description =
        "Guard conditions with high cyclomatic complexity are difficult to test and maintain. " +
        "Extract complex logic into well-named methods with unit tests.";

    private const string Category = "Maintainability";

    private static readonly DiagnosticDescriptor Rule = new DiagnosticDescriptor(
        DiagnosticId,
        Title,
        MessageFormat,
        Category,
        DiagnosticSeverity.Warning,
        isEnabledByDefault: true,
        description: Description,
        helpLinkUri: "https://github.com/mivertowski/Orleans.StateMachineES/docs/analyzers/OSMES008.md");

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

        // Analyze all PermitIf and other guard-related calls
        var invocations = methodDeclaration.Body!.DescendantNodes()
            .OfType<InvocationExpressionSyntax>();

        foreach (var invocation in invocations)
        {
            var methodName = AnalyzerHelpers.GetMethodName(invocation);

            if (IsGuardMethod(methodName))
            {
                AnalyzeGuardArguments(invocation, context);
            }
        }
    }

    private bool IsGuardMethod(string methodName)
    {
        return methodName switch
        {
            "PermitIf" => true,
            "PermitReentryIf" => true,
            "IgnoreIf" => true,
            "PermitDynamicIf" => true,
            _ => false
        };
    }

    private void AnalyzeGuardArguments(InvocationExpressionSyntax invocation, SyntaxNodeAnalysisContext context)
    {
        if (invocation.ArgumentList == null)
            return;

        foreach (var argument in invocation.ArgumentList.Arguments)
        {
            // Check lambda expressions
            if (argument.Expression is ParenthesizedLambdaExpressionSyntax parenLambda)
            {
                AnalyzeLambdaComplexity(parenLambda, parenLambda.Body, context);
            }
            else if (argument.Expression is SimpleLambdaExpressionSyntax simpleLambda)
            {
                AnalyzeLambdaComplexity(simpleLambda, simpleLambda.Body, context);
            }
        }
    }

    private void AnalyzeLambdaComplexity(
        SyntaxNode lambdaNode,
        CSharpSyntaxNode body,
        SyntaxNodeAnalysisContext context)
    {
        int complexity = CalculateCyclomaticComplexity(body);
        int nestingDepth = CalculateMaxNestingDepth(body);

        if (complexity > MaxComplexityThreshold)
        {
            var diagnostic = Diagnostic.Create(
                Rule,
                lambdaNode.GetLocation(),
                complexity,
                MaxComplexityThreshold);
            context.ReportDiagnostic(diagnostic);
        }
        else if (nestingDepth > MaxNestingDepth)
        {
            // Create a modified message for nesting depth
            var nestingRule = new DiagnosticDescriptor(
                DiagnosticId,
                "Guard condition has deep nesting",
                "Guard condition has nesting depth of {0} (threshold: {1}). Consider extracting to a named method",
                Category,
                DiagnosticSeverity.Warning,
                isEnabledByDefault: true,
                description: Description);

            var diagnostic = Diagnostic.Create(
                nestingRule,
                lambdaNode.GetLocation(),
                nestingDepth,
                MaxNestingDepth);
            context.ReportDiagnostic(diagnostic);
        }
    }

    private int CalculateCyclomaticComplexity(CSharpSyntaxNode node)
    {
        // Start with 1 (base complexity)
        int complexity = 1;

        // Count decision points
        var descendants = node.DescendantNodes();

        // Count if statements
        complexity += descendants.OfType<IfStatementSyntax>().Count();

        // Count while/for/foreach loops
        complexity += descendants.OfType<WhileStatementSyntax>().Count();
        complexity += descendants.OfType<ForStatementSyntax>().Count();
        complexity += descendants.OfType<ForEachStatementSyntax>().Count();

        // Count case labels in switch statements
        complexity += descendants.OfType<SwitchSectionSyntax>().Count();

        // Count conditional expressions (ternary operators)
        complexity += descendants.OfType<ConditionalExpressionSyntax>().Count();

        // Count logical && and || operators
        var binaryExpressions = descendants.OfType<BinaryExpressionSyntax>();
        complexity += binaryExpressions.Count(be =>
            be.IsKind(SyntaxKind.LogicalAndExpression) ||
            be.IsKind(SyntaxKind.LogicalOrExpression));

        // Count null-coalescing operators
        complexity += binaryExpressions.Count(be =>
            be.IsKind(SyntaxKind.CoalesceExpression));

        // Count catch clauses
        complexity += descendants.OfType<CatchClauseSyntax>().Count();

        return complexity;
    }

    private int CalculateMaxNestingDepth(CSharpSyntaxNode node)
    {
        return CalculateNestingDepthRecursive(node, 0);
    }

    private int CalculateNestingDepthRecursive(SyntaxNode node, int currentDepth)
    {
        int maxDepth = currentDepth;

        foreach (var child in node.ChildNodes())
        {
            int childDepth = currentDepth;

            // Increment depth for nesting constructs
            if (child is IfStatementSyntax ||
                child is WhileStatementSyntax ||
                child is ForStatementSyntax ||
                child is ForEachStatementSyntax ||
                child is SwitchStatementSyntax ||
                child is TryStatementSyntax ||
                child is LockStatementSyntax)
            {
                childDepth++;
            }

            int descendantDepth = CalculateNestingDepthRecursive(child, childDepth);
            maxDepth = System.Math.Max(maxDepth, descendantDepth);
        }

        return maxDepth;
    }
}
