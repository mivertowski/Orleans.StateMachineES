using System.Collections.Immutable;
using System.Linq;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Diagnostics;

namespace Orleans.StateMachineES.Generators.Analyzers
{
    /// <summary>
    /// Analyzer that detects StateMachineGrain-derived classes that don't properly implement BuildStateMachine.
    /// </summary>
    [DiagnosticAnalyzer(LanguageNames.CSharp)]
    public class MissingBuildStateMachineAnalyzer : DiagnosticAnalyzer
    {
        /// <summary>
        /// The diagnostic identifier for this analyzer.
        /// </summary>
        public const string DiagnosticId = "OSMES003";
        
        private static readonly LocalizableString Title = 
            "Missing or empty BuildStateMachine implementation";
        private static readonly LocalizableString MessageFormat = 
            "The class '{0}' derives from StateMachineGrain but has an empty or missing BuildStateMachine implementation";
        private static readonly LocalizableString Description = 
            "Classes deriving from StateMachineGrain must implement BuildStateMachine to configure states and transitions.";
        
        private const string Category = "Implementation";

        private static readonly DiagnosticDescriptor Rule = new(
            DiagnosticId,
            Title,
            MessageFormat,
            Category,
            DiagnosticSeverity.Error,
            isEnabledByDefault: true,
            description: Description);

        /// <summary>
        /// Gets the set of descriptors for the diagnostics that this analyzer is capable of producing.
        /// </summary>
        public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics =>
            ImmutableArray.Create(Rule);

        /// <summary>
        /// Registers actions in an analysis context to detect missing BuildStateMachine implementations.
        /// </summary>
        /// <param name="context">The context to register analysis actions.</param>
        public override void Initialize(AnalysisContext context)
        {
            context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);
            context.EnableConcurrentExecution();
            context.RegisterSyntaxNodeAction(AnalyzeClass, SyntaxKind.ClassDeclaration);
        }

        private void AnalyzeClass(SyntaxNodeAnalysisContext context)
        {
            var classDeclaration = (ClassDeclarationSyntax)context.Node;
            var semanticModel = context.SemanticModel;
            
            // Get the class symbol
            var classSymbol = semanticModel.GetDeclaredSymbol(classDeclaration);
            if (classSymbol == null) return;
            
            // Check if it derives from StateMachineGrain
            if (!DerivesFromStateMachineGrain(classSymbol)) return;
            
            // Skip abstract classes
            if (classSymbol.IsAbstract) return;
            
            // Find BuildStateMachine method
            var buildStateMachineMethod = classSymbol.GetMembers("BuildStateMachine")
                .OfType<IMethodSymbol>()
                .FirstOrDefault(m => m.Parameters.Length == 0 && 
                                   m.DeclaredAccessibility == Accessibility.Protected);
            
            if (buildStateMachineMethod == null)
            {
                // No BuildStateMachine method found
                var diagnostic = Diagnostic.Create(Rule, classDeclaration.Identifier.GetLocation(), classSymbol.Name);
                context.ReportDiagnostic(diagnostic);
                return;
            }
            
            // Check if the method has an implementation and is not empty
            var methodSyntax = buildStateMachineMethod.DeclaringSyntaxReferences
                .Select(sr => sr.GetSyntax())
                .OfType<MethodDeclarationSyntax>()
                .FirstOrDefault();
            
            if (methodSyntax?.Body != null)
            {
                // Check if body is effectively empty (only contains base call or return)
                var statements = methodSyntax.Body.Statements;
                var hasConfiguration = statements.Any(stmt =>
                {
                    if (stmt is ExpressionStatementSyntax exprStmt)
                    {
                        var expression = exprStmt.Expression.ToString();
                        return expression.Contains("Configure") || 
                               expression.Contains("SetTriggerParameters") ||
                               expression.Contains("Permit") ||
                               expression.Contains("OnEntry") ||
                               expression.Contains("OnExit");
                    }
                    return false;
                });
                
                if (!hasConfiguration)
                {
                    var diagnostic = Diagnostic.Create(Rule, methodSyntax.Identifier.GetLocation(), classSymbol.Name);
                    context.ReportDiagnostic(diagnostic);
                }
            }
        }
        
        private bool DerivesFromStateMachineGrain(INamedTypeSymbol classSymbol)
        {
            var baseType = classSymbol.BaseType;
            while (baseType != null)
            {
                if (baseType.Name == "StateMachineGrain" && 
                    baseType.ContainingNamespace?.ToString()?.Contains("Orleans.StateMachineES") == true)
                {
                    return true;
                }
                baseType = baseType.BaseType;
            }
            return false;
        }
    }
}