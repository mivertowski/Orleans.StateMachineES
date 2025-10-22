using System.Collections.Generic;
using System.Collections.Immutable;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Diagnostics;

namespace Orleans.StateMachineES.Generators.Analyzers
{
    /// <summary>
    /// Analyzer that detects potentially unreachable states in state machine configurations.
    /// </summary>
    [DiagnosticAnalyzer(LanguageNames.CSharp)]
    public class UnreachableStateAnalyzer : DiagnosticAnalyzer
    {
        public const string DiagnosticId = "OSMES004";
        
        private static readonly LocalizableString Title = 
            "Potentially unreachable state detected";
        private static readonly LocalizableString MessageFormat = 
            "State '{0}' is configured but has no incoming transitions and is not the initial state";
        private static readonly LocalizableString Description = 
            "States should be reachable through transitions from other states or be the initial state.";
        
        private const string Category = "Design";

        private static readonly DiagnosticDescriptor Rule = new(
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
            
            var semanticModel = context.SemanticModel;
            
            // Track configured states and their relationships
            var configuredStates = new HashSet<string>();
            var statesWithIncomingTransitions = new HashSet<string>();
            var initialState = FindInitialState(methodDeclaration.Body, semanticModel);
            
            // Analyze Configure calls and transitions
            foreach (var statement in methodDeclaration.Body.Statements)
            {
                if (statement is ExpressionStatementSyntax exprStmt)
                {
                    AnalyzeExpression(exprStmt.Expression, configuredStates, 
                                    statesWithIncomingTransitions, semanticModel);
                }
            }
            
            // Find unreachable states
            foreach (var state in configuredStates)
            {
                if (!statesWithIncomingTransitions.Contains(state) && 
                    state != initialState &&
                    !IsSubstateConfiguration(state, methodDeclaration.Body))
                {
                    // Find the location of the Configure call for this state
                    var configureLocation = FindConfigureLocation(state, methodDeclaration.Body);
                    if (configureLocation != null)
                    {
                        var diagnostic = Diagnostic.Create(Rule, configureLocation, state);
                        context.ReportDiagnostic(diagnostic);
                    }
                }
            }
        }
        
        private void AnalyzeExpression(ExpressionSyntax expression, 
                                      HashSet<string> configuredStates,
                                      HashSet<string> statesWithIncomingTransitions,
                                      SemanticModel semanticModel)
        {
            // Look for Configure calls
            if (expression is InvocationExpressionSyntax invocation)
            {
                var invocationText = invocation.ToString();
                
                // Check for Configure(State.X)
                if (invocationText.Contains("Configure"))
                {
                    var stateArg = ExtractStateFromConfigureCall(invocation);
                    if (!string.IsNullOrEmpty(stateArg))
                    {
                        configuredStates.Add(stateArg!);
                    }

                    // Analyze chained method calls for Permit
                    AnalyzeChainedCalls(invocation, statesWithIncomingTransitions);
                }
            }
            else if (expression is MemberAccessExpressionSyntax memberAccess)
            {
                // Handle chained calls
                if (memberAccess.Expression is InvocationExpressionSyntax chainedInvocation)
                {
                    AnalyzeExpression(chainedInvocation, configuredStates, 
                                    statesWithIncomingTransitions, semanticModel);
                }
            }
        }
        
        private void AnalyzeChainedCalls(InvocationExpressionSyntax invocation,
                                        HashSet<string> statesWithIncomingTransitions)
        {
            var current = invocation.Parent;
            while (current is MemberAccessExpressionSyntax memberAccess)
            {
                if (memberAccess.Parent is InvocationExpressionSyntax parentInvocation)
                {
                    var methodName = memberAccess.Name.ToString();
                    
                    // Check for Permit, PermitIf, PermitReentry, etc.
                    if (methodName.StartsWith("Permit") || methodName == "InternalTransition")
                    {
                        // Extract target state from arguments
                        var targetState = ExtractTargetStateFromPermit(parentInvocation);
                        if (!string.IsNullOrEmpty(targetState))
                        {
                            statesWithIncomingTransitions.Add(targetState!);
                        }
                    }
                    // Check for SubstateOf
                    else if (methodName == "SubstateOf")
                    {
                        var parentState = ExtractStateFromArguments(parentInvocation);
                        if (!string.IsNullOrEmpty(parentState))
                        {
                            statesWithIncomingTransitions.Add(parentState!);
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
        
        private string? ExtractStateFromConfigureCall(InvocationExpressionSyntax invocation)
        {
            if (invocation.ArgumentList?.Arguments.Count > 0)
            {
                var arg = invocation.ArgumentList.Arguments[0].Expression;
                return ExtractStateName(arg);
            }
            return null;
        }
        
        private string? ExtractTargetStateFromPermit(InvocationExpressionSyntax invocation)
        {
            // Permit methods typically have trigger as first arg, target state as second
            if (invocation.ArgumentList?.Arguments.Count >= 2)
            {
                var targetStateArg = invocation.ArgumentList.Arguments[1].Expression;
                return ExtractStateName(targetStateArg);
            }
            return null;
        }
        
        private string? ExtractStateFromArguments(InvocationExpressionSyntax invocation)
        {
            if (invocation.ArgumentList?.Arguments.Count > 0)
            {
                var arg = invocation.ArgumentList.Arguments[0].Expression;
                return ExtractStateName(arg);
            }
            return null;
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
            return expression?.ToString();
        }
        
        private string? FindInitialState(BlockSyntax body, SemanticModel semanticModel)
        {
            // Look for constructor initialization or first configured state
            foreach (var statement in body.Statements)
            {
                var statementText = statement.ToString();
                if (statementText.Contains("= new StateMachine"))
                {
                    // Try to extract initial state from constructor
                    if (statement is LocalDeclarationStatementSyntax localDecl)
                    {
                        var initializer = localDecl.Declaration.Variables.FirstOrDefault()?.Initializer;
                        if (initializer?.Value is ObjectCreationExpressionSyntax creation)
                        {
                            if (creation.ArgumentList?.Arguments.Count > 0)
                            {
                                return ExtractStateName(creation.ArgumentList.Arguments[0].Expression);
                            }
                        }
                    }
                }
            }
            return null;
        }
        
        private bool IsSubstateConfiguration(string state, BlockSyntax body)
        {
            // Check if this state is configured as a substate
            var bodyText = body.ToString();
            return bodyText.Contains($".SubstateOf") && bodyText.Contains(state);
        }
        
        private Location? FindConfigureLocation(string state, BlockSyntax body)
        {
            foreach (var statement in body.Statements)
            {
                if (statement is ExpressionStatementSyntax exprStmt &&
                    exprStmt.Expression is InvocationExpressionSyntax invocation)
                {
                    if (invocation.ToString().Contains($"Configure") &&
                        invocation.ToString().Contains(state))
                    {
                        return invocation.GetLocation();
                    }
                }
            }
            return null;
        }
    }
}