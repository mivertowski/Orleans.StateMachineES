using System;
using System.Collections.Immutable;
using System.Linq;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Diagnostics;

namespace Orleans.StateMachineES.Generators.Analyzers
{
    /// <summary>
    /// Roslyn analyzer that detects async lambdas in OnEntry/OnExit callbacks
    /// and provides compile-time warnings to prevent runtime issues.
    /// </summary>
    [DiagnosticAnalyzer(LanguageNames.CSharp)]
    public class AsyncLambdaInCallbackAnalyzer : DiagnosticAnalyzer
    {
        public const string DiagnosticId = "OSMES001";
        
        private static readonly LocalizableString Title = 
            "Async lambda detected in state machine callback";
        
        private static readonly LocalizableString MessageFormat = 
            "Async lambda in {0} callback will not be awaited and may cause issues";
        
        private static readonly LocalizableString Description = 
            "OnEntry, OnExit, OnEntryFrom, and OnExitTo callbacks in Stateless do not support async operations. " +
            "Async lambdas will run as fire-and-forget tasks which can lead to race conditions and unhandled exceptions. " +
            "Consider moving async logic to grain methods that are called after state transitions.";
        
        private const string Category = "Usage";

        private static readonly DiagnosticDescriptor Rule = new DiagnosticDescriptor(
            DiagnosticId,
            Title,
            MessageFormat,
            Category,
            DiagnosticSeverity.Warning,
            isEnabledByDefault: true,
            description: Description,
            helpLinkUri: "https://github.com/mivertowski/Orleans.StateMachineES/docs/analyzers/OSMES001.md");

        public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics => 
            ImmutableArray.Create(Rule);

        public override void Initialize(AnalysisContext context)
        {
            context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);
            context.EnableConcurrentExecution();
            context.RegisterSyntaxNodeAction(AnalyzeInvocation, SyntaxKind.InvocationExpression);
        }

        private static void AnalyzeInvocation(SyntaxNodeAnalysisContext context)
        {
            var invocation = (InvocationExpressionSyntax)context.Node;
            
            // Check if this is a method we care about
            if (!IsStateCallbackMethod(invocation, context))
                return;

            // Look for async lambdas in the arguments
            foreach (var argument in invocation.ArgumentList.Arguments)
            {
                if (IsAsyncLambda(argument.Expression, context))
                {
                    var methodName = GetMethodName(invocation);
                    var diagnostic = Diagnostic.Create(
                        Rule,
                        argument.GetLocation(),
                        methodName);
                    
                    context.ReportDiagnostic(diagnostic);
                }
            }
        }

        private static bool IsStateCallbackMethod(InvocationExpressionSyntax invocation, SyntaxNodeAnalysisContext context)
        {
            var methodName = GetMethodName(invocation);
            
            // Check for the callback method names
            if (!IsCallbackMethodName(methodName))
                return false;

            // Verify this is on a StateConfiguration object
            var symbolInfo = context.SemanticModel.GetSymbolInfo(invocation);
            if (symbolInfo.Symbol is not IMethodSymbol methodSymbol)
                return false;

            var containingType = methodSymbol.ContainingType;
            if (containingType == null)
                return false;

            // Check if this is a Stateless StateConfiguration type
            return containingType.Name.Contains("StateConfiguration") ||
                   containingType.ToDisplayString().Contains("Stateless");
        }

        private static bool IsCallbackMethodName(string methodName)
        {
            return methodName switch
            {
                "OnEntry" => true,
                "OnExit" => true,
                "OnEntryFrom" => true,
                "OnExitTo" => true,
                "OnActivate" => true,
                "OnDeactivate" => true,
                "OnUnhandledTrigger" => true,
                _ => false
            };
        }

        private static string GetMethodName(InvocationExpressionSyntax invocation)
        {
            return invocation.Expression switch
            {
                MemberAccessExpressionSyntax memberAccess => memberAccess.Name.Identifier.Text,
                IdentifierNameSyntax identifier => identifier.Identifier.Text,
                _ => string.Empty
            };
        }

        private static bool IsAsyncLambda(ExpressionSyntax expression, SyntaxNodeAnalysisContext context)
        {
            // Check for async lambda expressions
            if (expression is ParenthesizedLambdaExpressionSyntax parenLambda)
            {
                return parenLambda.AsyncKeyword.IsKind(SyntaxKind.AsyncKeyword);
            }
            
            if (expression is SimpleLambdaExpressionSyntax simpleLambda)
            {
                return simpleLambda.AsyncKeyword.IsKind(SyntaxKind.AsyncKeyword);
            }
            
            // Check for async anonymous methods
            if (expression is AnonymousMethodExpressionSyntax anonymousMethod)
            {
                return anonymousMethod.AsyncKeyword.IsKind(SyntaxKind.AsyncKeyword);
            }

            // Check if it's a method group that points to an async method
            if (expression is IdentifierNameSyntax identifier)
            {
                var symbol = context.SemanticModel.GetSymbolInfo(identifier).Symbol;
                if (symbol is IMethodSymbol method)
                {
                    return method.IsAsync;
                }
            }

            return false;
        }
    }
}