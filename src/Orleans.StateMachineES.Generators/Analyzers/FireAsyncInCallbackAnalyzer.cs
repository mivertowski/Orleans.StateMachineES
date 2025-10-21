using System;
using System.Collections.Immutable;
using System.Linq;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Diagnostics;
using Microsoft.CodeAnalysis.Operations;

namespace Orleans.StateMachineES.Generators.Analyzers
{
    /// <summary>
    /// Roslyn analyzer that detects FireAsync calls within OnEntry/OnExit callbacks
    /// which can cause deadlocks and reentrancy issues.
    /// </summary>
    [DiagnosticAnalyzer(LanguageNames.CSharp)]
    public class FireAsyncInCallbackAnalyzer : DiagnosticAnalyzer
    {
        public const string DiagnosticId = "OSMES002";
        
        private static readonly LocalizableString Title = 
            "FireAsync called within state machine callback";
        
        private static readonly LocalizableString MessageFormat = 
            "Calling FireAsync within {0} callback can cause deadlocks or reentrancy issues";
        
        private static readonly LocalizableString Description = 
            "Calling FireAsync or Fire methods within OnEntry, OnExit, or other state callbacks can lead to deadlocks, " +
            "reentrancy issues, and unpredictable behavior. State transitions should be triggered from grain methods " +
            "after the current transition completes.";
        
        private const string Category = "Usage";

        private static readonly DiagnosticDescriptor Rule = new DiagnosticDescriptor(
            DiagnosticId,
            Title,
            MessageFormat,
            Category,
            DiagnosticSeverity.Error,
            isEnabledByDefault: true,
            description: Description,
            helpLinkUri: "https://github.com/mivertowski/Orleans.StateMachineES/docs/analyzers/OSMES002.md");

        public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics => 
            ImmutableArray.Create(Rule);

        public override void Initialize(AnalysisContext context)
        {
            context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);
            context.EnableConcurrentExecution();
            context.RegisterOperationAction(AnalyzeOperation, OperationKind.Invocation);
        }

        private static void AnalyzeOperation(OperationAnalysisContext context)
        {
            var invocation = (IInvocationOperation)context.Operation;
            
            // Check if this is a FireAsync or Fire call
            if (!IsFireMethod(invocation))
                return;

            // Check if we're inside a state callback
            var callbackContext = GetContainingCallbackContext(invocation);
            if (callbackContext != null)
            {
                var diagnostic = Diagnostic.Create(
                    Rule,
                    invocation.Syntax.GetLocation(),
                    callbackContext);
                
                context.ReportDiagnostic(diagnostic);
            }
        }

        private static bool IsFireMethod(IInvocationOperation invocation)
        {
            var methodName = invocation.TargetMethod.Name;
            return methodName == "FireAsync" || 
                   methodName == "Fire" ||
                   methodName == "FireOneOfAsync";
        }

        private static string? GetContainingCallbackContext(IInvocationOperation invocation)
        {
            // Walk up the syntax tree to find if we're inside a callback lambda
            var current = invocation.Syntax.Parent;
            
            while (current != null)
            {
                // Check if we're inside a lambda that's an argument to a callback method
                if (current is LambdaExpressionSyntax lambda)
                {
                    var parent = lambda.Parent;
                    while (parent != null && !(parent is ArgumentSyntax))
                    {
                        parent = parent.Parent;
                    }
                    
                    if (parent?.Parent?.Parent is InvocationExpressionSyntax callbackInvocation)
                    {
                        var methodName = GetMethodName(callbackInvocation);
                        if (IsCallbackMethodName(methodName))
                        {
                            return methodName;
                        }
                    }
                }
                
                // Check if we're inside an anonymous method
                if (current is AnonymousMethodExpressionSyntax anonymousMethod)
                {
                    var parent = anonymousMethod.Parent;
                    if (parent is ArgumentSyntax arg && 
                        arg.Parent?.Parent is InvocationExpressionSyntax callbackInvocation)
                    {
                        var methodName = GetMethodName(callbackInvocation);
                        if (IsCallbackMethodName(methodName))
                        {
                            return methodName;
                        }
                    }
                }
                
                // Stop if we've reached a method declaration (we've gone too far)
                if (current is MethodDeclarationSyntax)
                    break;
                
                current = current.Parent;
            }
            
            return null;
        }

        private static string GetMethodName(InvocationExpressionSyntax invocation)
        {
            return AnalyzerHelpers.GetMethodName(invocation);
        }

        private static bool IsCallbackMethodName(string methodName)
        {
            return AnalyzerHelpers.IsCallbackMethodName(methodName);
        }
    }
}