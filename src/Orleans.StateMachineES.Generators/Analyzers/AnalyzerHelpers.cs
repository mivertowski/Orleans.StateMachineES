using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace Orleans.StateMachineES.Generators.Analyzers;

/// <summary>
/// Shared utility methods for state machine analyzers.
/// Provides common functionality for analyzing state machine code patterns.
/// </summary>
internal static class AnalyzerHelpers
{
    /// <summary>
    /// Determines if a method name is a state callback method.
    /// </summary>
    /// <param name="methodName">The method name to check.</param>
    /// <returns>True if the method name matches a callback pattern, false otherwise.</returns>
    public static bool IsCallbackMethodName(string methodName)
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
            "InternalTransition" => true,
            _ => false
        };
    }

    /// <summary>
    /// Extracts the state name from an expression.
    /// </summary>
    /// <param name="expression">The expression to extract from.</param>
    /// <returns>The state name, or null if extraction fails.</returns>
    public static string? ExtractStateName(ExpressionSyntax expression)
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

    /// <summary>
    /// Gets the method name from an invocation expression.
    /// </summary>
    /// <param name="invocation">The invocation expression.</param>
    /// <returns>The method name, or empty string if extraction fails.</returns>
    public static string GetMethodName(InvocationExpressionSyntax invocation)
    {
        return invocation.Expression switch
        {
            MemberAccessExpressionSyntax memberAccess => memberAccess.Name.Identifier.Text,
            IdentifierNameSyntax identifier => identifier.Identifier.Text,
            _ => string.Empty
        };
    }

    /// <summary>
    /// Determines if a method declaration is the BuildStateMachine method.
    /// </summary>
    /// <param name="method">The method declaration to check.</param>
    /// <returns>True if this is a BuildStateMachine method, false otherwise.</returns>
    public static bool IsBuildStateMachineMethod(MethodDeclarationSyntax method)
    {
        return method.Identifier.Text == "BuildStateMachine" && method.Body != null;
    }

    /// <summary>
    /// Determines if an invocation is a Configure method call on a state machine.
    /// </summary>
    /// <param name="invocation">The invocation to check.</param>
    /// <param name="context">The semantic model context.</param>
    /// <returns>True if this is a Configure call, false otherwise.</returns>
    public static bool IsConfigureCall(InvocationExpressionSyntax invocation, SemanticModel context)
    {
        var methodName = GetMethodName(invocation);
        if (methodName != "Configure")
        {
            return false;
        }

        var symbolInfo = context.GetSymbolInfo(invocation);
        if (symbolInfo.Symbol is not IMethodSymbol methodSymbol)
        {
            return false;
        }

        var containingType = methodSymbol.ContainingType;
        return containingType?.Name.Contains("StateMachine") == true;
    }

    /// <summary>
    /// Determines if an invocation is on a StateConfiguration object from Stateless library.
    /// </summary>
    /// <param name="invocation">The invocation to check.</param>
    /// <param name="context">The semantic model context.</param>
    /// <returns>True if this is a StateConfiguration method, false otherwise.</returns>
    public static bool IsStateConfigurationMethod(InvocationExpressionSyntax invocation, SemanticModel context)
    {
        var symbolInfo = context.GetSymbolInfo(invocation);
        if (symbolInfo.Symbol is not IMethodSymbol methodSymbol)
        {
            return false;
        }

        var containingType = methodSymbol.ContainingType;
        if (containingType == null)
        {
            return false;
        }

        // Check if this is a Stateless StateConfiguration type
        return containingType.Name.Contains("StateConfiguration") ||
               containingType.ToDisplayString().Contains("Stateless");
    }

    /// <summary>
    /// Extracts the state argument from a Configure method call.
    /// </summary>
    /// <param name="invocation">The Configure invocation.</param>
    /// <returns>The state name, or null if extraction fails.</returns>
    public static string? ExtractStateFromConfigureCall(InvocationExpressionSyntax invocation)
    {
        if (invocation.ArgumentList?.Arguments.Count > 0)
        {
            var arg = invocation.ArgumentList.Arguments[0].Expression;
            return ExtractStateName(arg);
        }
        return null;
    }

    /// <summary>
    /// Extracts the target state from a Permit method call.
    /// </summary>
    /// <param name="invocation">The Permit invocation.</param>
    /// <returns>The target state name, or null if extraction fails.</returns>
    public static string? ExtractTargetStateFromPermit(InvocationExpressionSyntax invocation)
    {
        // Permit methods typically have trigger as first arg, target state as second
        if (invocation.ArgumentList?.Arguments.Count >= 2)
        {
            var targetStateArg = invocation.ArgumentList.Arguments[1].Expression;
            return ExtractStateName(targetStateArg);
        }
        return null;
    }

    /// <summary>
    /// Extracts the first argument from an invocation as a state name.
    /// </summary>
    /// <param name="invocation">The invocation.</param>
    /// <returns>The state name, or null if extraction fails.</returns>
    public static string? ExtractStateFromFirstArgument(InvocationExpressionSyntax invocation)
    {
        if (invocation.ArgumentList?.Arguments.Count > 0)
        {
            var arg = invocation.ArgumentList.Arguments[0].Expression;
            return ExtractStateName(arg);
        }
        return null;
    }

    /// <summary>
    /// Finds the initial state from a state machine initialization.
    /// </summary>
    /// <param name="body">The method body to search.</param>
    /// <param name="semanticModel">The semantic model for symbol resolution.</param>
    /// <returns>The initial state name, or null if not found.</returns>
    public static string? FindInitialState(BlockSyntax body, SemanticModel semanticModel)
    {
        // Look for constructor initialization
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
                else if (statement is ExpressionStatementSyntax exprStmt &&
                         exprStmt.Expression is AssignmentExpressionSyntax assignment &&
                         assignment.Right is ObjectCreationExpressionSyntax objectCreation)
                {
                    if (objectCreation.ArgumentList?.Arguments.Count > 0)
                    {
                        return ExtractStateName(objectCreation.ArgumentList.Arguments[0].Expression);
                    }
                }
            }
            else if (statement is ReturnStatementSyntax returnStmt &&
                     returnStmt.Expression is ObjectCreationExpressionSyntax returnCreation)
            {
                if (returnCreation.ArgumentList?.Arguments.Count > 0)
                {
                    return ExtractStateName(returnCreation.ArgumentList.Arguments[0].Expression);
                }
            }
        }
        return null;
    }

    /// <summary>
    /// Determines if a method name indicates a Permit-style transition configuration.
    /// </summary>
    /// <param name="methodName">The method name to check.</param>
    /// <returns>True if the method configures a transition, false otherwise.</returns>
    public static bool IsTransitionConfigurationMethod(string methodName)
    {
        return methodName.StartsWith("Permit") ||
               methodName == "InternalTransition" ||
               methodName == "Ignore" ||
               methodName == "IgnoreIf";
    }

    /// <summary>
    /// Checks if an expression is an async lambda.
    /// </summary>
    /// <param name="expression">The expression to check.</param>
    /// <param name="context">The semantic model context.</param>
    /// <returns>True if the expression is an async lambda, false otherwise.</returns>
    public static bool IsAsyncLambda(ExpressionSyntax expression, SemanticModel context)
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
            var symbol = context.GetSymbolInfo(identifier).Symbol;
            if (symbol is IMethodSymbol method)
            {
                return method.IsAsync;
            }
        }

        return false;
    }

    /// <summary>
    /// Finds the Configure invocation location for a specific state.
    /// </summary>
    /// <param name="state">The state name to find.</param>
    /// <param name="body">The method body to search.</param>
    /// <returns>The location of the Configure call, or null if not found.</returns>
    public static Location? FindConfigureLocation(string state, BlockSyntax body)
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
