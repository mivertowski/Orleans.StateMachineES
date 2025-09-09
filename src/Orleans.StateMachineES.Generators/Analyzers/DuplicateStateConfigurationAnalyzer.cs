using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Diagnostics;

namespace Orleans.StateMachineES.Generators.Analyzers
{
    /// <summary>
    /// Analyzer that detects duplicate state configurations in BuildStateMachine method.
    /// </summary>
    [DiagnosticAnalyzer(LanguageNames.CSharp)]
    public class DuplicateStateConfigurationAnalyzer : DiagnosticAnalyzer
    {
        public const string DiagnosticId = "OSMES005";
        
        private static readonly LocalizableString Title = 
            "Duplicate state configuration detected";
        private static readonly LocalizableString MessageFormat = 
            "State '{0}' is configured multiple times";
        private static readonly LocalizableString Description = 
            "Each state should be configured once to avoid confusion and potential conflicts.";
        
        private const string Category = "Design";

        private static readonly DiagnosticDescriptor Rule = new DiagnosticDescriptor(
            DiagnosticId,
            Title,
            MessageFormat,
            Category,
            DiagnosticSeverity.Warning,
            isEnabledByDefault: true,
            description: Description);

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
            if (methodDeclaration.Identifier.Text != "BuildStateMachine") return;
            if (methodDeclaration.Body == null) return;
            
            // Track configured states and their locations
            var stateConfigurations = new Dictionary<string, List<Location>>();
            
            // Analyze all Configure calls
            foreach (var statement in methodDeclaration.Body.Statements)
            {
                if (statement is ExpressionStatementSyntax exprStmt)
                {
                    AnalyzeConfigureCall(exprStmt.Expression, stateConfigurations);
                }
            }
            
            // Report duplicates
            foreach (var kvp in stateConfigurations.Where(x => x.Value.Count > 1))
            {
                foreach (var location in kvp.Value.Skip(1)) // Skip first occurrence
                {
                    var diagnostic = Diagnostic.Create(Rule, location, kvp.Key);
                    context.ReportDiagnostic(diagnostic);
                }
            }
        }
        
        private void AnalyzeConfigureCall(ExpressionSyntax expression, 
                                         Dictionary<string, List<Location>> stateConfigurations)
        {
            // Look for Configure calls
            if (expression is InvocationExpressionSyntax invocation)
            {
                var invocationText = invocation.Expression.ToString();
                
                if (invocationText.Contains("Configure") && !invocationText.Contains(".Configure"))
                {
                    // Extract state from Configure(State.X)
                    if (invocation.ArgumentList?.Arguments.Count > 0)
                    {
                        var stateArg = invocation.ArgumentList.Arguments[0].Expression;
                        var stateName = ExtractStateName(stateArg);
                        
                        if (!string.IsNullOrEmpty(stateName))
                        {
                            if (!stateConfigurations.ContainsKey(stateName))
                            {
                                stateConfigurations[stateName] = new List<Location>();
                            }
                            stateConfigurations[stateName].Add(invocation.GetLocation());
                        }
                    }
                }
                
                // Also check for chained Configure calls
                if (expression is MemberAccessExpressionSyntax memberAccess)
                {
                    if (memberAccess.Expression is InvocationExpressionSyntax chainedInvocation)
                    {
                        AnalyzeConfigureCall(chainedInvocation, stateConfigurations);
                    }
                }
            }
        }
        
        private string? ExtractStateName(ExpressionSyntax expression)
        {
            // Handle State.SomeName or MyStates.SomeName patterns
            if (expression is MemberAccessExpressionSyntax memberAccess)
            {
                return memberAccess.Name.ToString();
            }
            // Handle direct enum values or constants
            else if (expression is IdentifierNameSyntax identifier)
            {
                return identifier.Identifier.Text;
            }
            // Handle cast expressions like (State)1
            else if (expression is CastExpressionSyntax cast)
            {
                return cast.Expression.ToString();
            }
            
            return expression?.ToString();
        }
    }
}