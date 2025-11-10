using System.Collections.Immutable;
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
        /// <summary>
        /// The diagnostic identifier for this analyzer.
        /// </summary>
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

        private static readonly DiagnosticDescriptor Rule = new(
            DiagnosticId,
            Title,
            MessageFormat,
            Category,
            DiagnosticSeverity.Warning,
            isEnabledByDefault: true,
            description: Description,
            helpLinkUri: "https://github.com/mivertowski/Orleans.StateMachineES/docs/analyzers/OSMES001.md");

        /// <summary>
        /// Gets the set of descriptors for the diagnostics that this analyzer is capable of producing.
        /// </summary>
        public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics =>
            ImmutableArray.Create(Rule);

        /// <summary>
        /// Registers actions in an analysis context to analyze async lambdas in state callbacks.
        /// </summary>
        /// <param name="context">The context to register analysis actions.</param>
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
                    var methodName = AnalyzerHelpers.GetMethodName(invocation);
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
            var methodName = AnalyzerHelpers.GetMethodName(invocation);

            // Check for the callback method names
            if (!AnalyzerHelpers.IsCallbackMethodName(methodName))
                return false;

            // Verify this is on a StateConfiguration object
            return AnalyzerHelpers.IsStateConfigurationMethod(invocation, context.SemanticModel);
        }

        private static bool IsAsyncLambda(ExpressionSyntax expression, SyntaxNodeAnalysisContext context)
        {
            return AnalyzerHelpers.IsAsyncLambda(expression, context.SemanticModel);
        }
    }
}