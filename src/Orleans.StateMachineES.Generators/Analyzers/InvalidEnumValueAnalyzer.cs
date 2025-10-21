using System.Collections.Immutable;
using System.Linq;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Diagnostics;

namespace Orleans.StateMachineES.Generators.Analyzers;

/// <summary>
/// Analyzer that detects invalid enum values used for states or triggers.
/// Warns about casting integers to enums or using undefined enum values.
/// </summary>
[DiagnosticAnalyzer(LanguageNames.CSharp)]
public class InvalidEnumValueAnalyzer : DiagnosticAnalyzer
{
    public const string DiagnosticId = "OSMES010";

    private static readonly LocalizableString Title =
        "Potentially invalid enum value used for state or trigger";
    private static readonly LocalizableString MessageFormat =
        "Value '{0}' may not be a valid enum member. Use named enum values instead of numeric casts";
    private static readonly LocalizableString Description =
        "Using numeric values cast to enums can lead to runtime errors if the value is not defined in the enum. " +
        "Always use named enum members for type safety.";

    private const string Category = "Reliability";

    private static readonly DiagnosticDescriptor Rule = new DiagnosticDescriptor(
        DiagnosticId,
        Title,
        MessageFormat,
        Category,
        DiagnosticSeverity.Warning,
        isEnabledByDefault: true,
        description: Description,
        helpLinkUri: "https://github.com/mivertowski/Orleans.StateMachineES/docs/analyzers/OSMES010.md");

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

        // Find all cast expressions in the method
        var castExpressions = methodDeclaration.Body!.DescendantNodes()
            .OfType<CastExpressionSyntax>();

        foreach (var cast in castExpressions)
        {
            AnalyzeCastExpression(cast, context, semanticModel);
        }

        // Also check for Convert.ToXxx and Enum.Parse patterns
        var invocations = methodDeclaration.Body.DescendantNodes()
            .OfType<InvocationExpressionSyntax>();

        foreach (var invocation in invocations)
        {
            AnalyzeEnumConversion(invocation, context, semanticModel);
        }
    }

    private void AnalyzeCastExpression(
        CastExpressionSyntax cast,
        SyntaxNodeAnalysisContext context,
        SemanticModel semanticModel)
    {
        // Check if casting to an enum type
        var typeInfo = semanticModel.GetTypeInfo(cast.Type);
        if (typeInfo.Type == null || typeInfo.Type.TypeKind != TypeKind.Enum)
            return;

        // Check if the expression being cast is a numeric literal or numeric expression
        if (IsNumericExpression(cast.Expression, semanticModel))
        {
            var diagnostic = Diagnostic.Create(
                Rule,
                cast.GetLocation(),
                cast.ToString());
            context.ReportDiagnostic(diagnostic);
        }
    }

    private void AnalyzeEnumConversion(
        InvocationExpressionSyntax invocation,
        SyntaxNodeAnalysisContext context,
        SemanticModel semanticModel)
    {
        var memberAccess = invocation.Expression as MemberAccessExpressionSyntax;
        if (memberAccess == null)
            return;

        var methodName = memberAccess.Name.Identifier.Text;

        // Check for Enum.Parse, Enum.TryParse, Convert.ToXxx patterns
        bool isEnumConversion = false;

        if (memberAccess.Expression is IdentifierNameSyntax identifier)
        {
            if (identifier.Identifier.Text == "Enum" &&
                (methodName.StartsWith("Parse") || methodName.StartsWith("ToObject")))
            {
                isEnumConversion = true;
            }
            else if (identifier.Identifier.Text == "Convert" &&
                     methodName.StartsWith("To"))
            {
                // Check if result type is an enum
                var typeInfo = semanticModel.GetTypeInfo(invocation);
                if (typeInfo.Type?.TypeKind == TypeKind.Enum)
                {
                    isEnumConversion = true;
                }
            }
        }

        if (isEnumConversion)
        {
            // Check if the argument is a numeric value
            if (invocation.ArgumentList?.Arguments.Count > 0)
            {
                var firstArg = invocation.ArgumentList.Arguments[0].Expression;
                if (IsNumericExpression(firstArg, semanticModel))
                {
                    var diagnostic = Diagnostic.Create(
                        Rule,
                        invocation.GetLocation(),
                        invocation.ToString());
                    context.ReportDiagnostic(diagnostic);
                }
            }
        }
    }

    private bool IsNumericExpression(ExpressionSyntax expression, SemanticModel semanticModel)
    {
        // Check for numeric literals
        if (expression is LiteralExpressionSyntax literal)
        {
            return literal.IsKind(SyntaxKind.NumericLiteralExpression);
        }

        // Check for numeric type
        var typeInfo = semanticModel.GetTypeInfo(expression);
        if (typeInfo.Type != null)
        {
            var specialType = typeInfo.Type.SpecialType;
            return specialType >= SpecialType.System_SByte &&
                   specialType <= SpecialType.System_Double;
        }

        // Check for binary operations with numeric operands
        if (expression is BinaryExpressionSyntax binary)
        {
            return IsNumericExpression(binary.Left, semanticModel) ||
                   IsNumericExpression(binary.Right, semanticModel);
        }

        return false;
    }
}
