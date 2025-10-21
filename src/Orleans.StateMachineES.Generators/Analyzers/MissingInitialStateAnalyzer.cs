using System.Collections.Immutable;
using System.Linq;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Diagnostics;

namespace Orleans.StateMachineES.Generators.Analyzers;

/// <summary>
/// Analyzer that detects state machines without a clear initial state.
/// All state machines must have an initial state specified in the constructor.
/// </summary>
[DiagnosticAnalyzer(LanguageNames.CSharp)]
public class MissingInitialStateAnalyzer : DiagnosticAnalyzer
{
    public const string DiagnosticId = "OSMES009";

    private static readonly LocalizableString Title =
        "State machine has no initial state";
    private static readonly LocalizableString MessageFormat =
        "BuildStateMachine method does not specify an initial state. Add initial state to StateMachine constructor.";
    private static readonly LocalizableString Description =
        "State machines must have a clearly defined initial state. " +
        "Specify the initial state in the StateMachine<TState, TTrigger> constructor call.";

    private const string Category = "Usage";

    private static readonly DiagnosticDescriptor Rule = new DiagnosticDescriptor(
        DiagnosticId,
        Title,
        MessageFormat,
        Category,
        DiagnosticSeverity.Error,
        isEnabledByDefault: true,
        description: Description,
        helpLinkUri: "https://github.com/mivertowski/Orleans.StateMachineES/docs/analyzers/OSMES009.md");

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
        bool hasInitialState = false;

        // Look for StateMachine constructor calls
        var objectCreations = methodDeclaration.Body!.DescendantNodes()
            .OfType<ObjectCreationExpressionSyntax>();

        foreach (var creation in objectCreations)
        {
            // Check if this is a StateMachine creation
            var typeInfo = semanticModel.GetTypeInfo(creation);
            if (typeInfo.Type?.Name.Contains("StateMachine") == true)
            {
                // Check if it has arguments (initial state)
                if (creation.ArgumentList?.Arguments.Count > 0)
                {
                    hasInitialState = true;
                    break;
                }
            }
        }

        // Check for return statements with StateMachine creation
        if (!hasInitialState)
        {
            var returnStatements = methodDeclaration.Body.DescendantNodes()
                .OfType<ReturnStatementSyntax>();

            foreach (var returnStmt in returnStatements)
            {
                if (returnStmt.Expression is ObjectCreationExpressionSyntax returnCreation)
                {
                    var typeInfo = semanticModel.GetTypeInfo(returnCreation);
                    if (typeInfo.Type?.Name.Contains("StateMachine") == true)
                    {
                        if (returnCreation.ArgumentList?.Arguments.Count > 0)
                        {
                            hasInitialState = true;
                            break;
                        }
                    }
                }
            }
        }

        // Report if no initial state found
        if (!hasInitialState)
        {
            var diagnostic = Diagnostic.Create(
                Rule,
                methodDeclaration.Identifier.GetLocation());
            context.ReportDiagnostic(diagnostic);
        }
    }
}
