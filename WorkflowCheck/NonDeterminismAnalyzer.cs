namespace WorkflowCheck;

using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Diagnostics;
using Helpers;

/// <summary>
/// Analyzer that reports non-determinism found in Temporal Workflow definition.
/// Traverses through the Syntax Tree and checks for pre-determined non-deterministic functions.
/// 1. Using Random Numbers 
/// 1.1 Random Class (a pseudo-random number generator)
/// 1.2 RandomNumberGenerator Class (provides a way to generate cryptographically secure random numbers)
/// 1.3 Guid Struct (provides a way to generate a unique identifier)
/// 2. Relying on current System Time 
/// 2.1 Accessing Now, UtcNow and Today properties of the DateTime Struct
/// </summary>
[DiagnosticAnalyzer(LanguageNames.CSharp)]
public class NonDeterminismAnalyzer : DiagnosticAnalyzer
{
    private readonly HashSet<SyntaxNode> _visitedNodes = new();

    private const string WorkflowDefinitionMarker = "WorkflowRun";
    
    private readonly Dictionary<string, bool> _nonDeterminismMap = new()
    {
        { "System.Guid.NewGuid", true },
        { "System.Guid.CreateVersion7", true },
        
        { "System.Security.Cryptography.RandomNumberGenerator", true },
    };
    
    // Preferred format of DiagnosticId is Your Prefix + Number, e.g. CA1234.
    private const string DiagnosticId = "WF0001";
    private static readonly LocalizableString Title = new LocalizableResourceString(nameof(Resources.WF0001Title),
        Resources.ResourceManager, typeof(Resources));

    // The message that will be displayed to the user.
    private static readonly LocalizableString MessageFormat =
        new LocalizableResourceString(nameof(Resources.WF0001MessageFormat), Resources.ResourceManager,
            typeof(Resources));

    private static readonly LocalizableString Description =
        new LocalizableResourceString(nameof(Resources.WF0001Description), Resources.ResourceManager,
            typeof(Resources));

    // The category of the diagnostic (Design, Naming etc.).
    private const string Category = "Usage";

    private static readonly DiagnosticDescriptor Rule = new(DiagnosticId, Title, MessageFormat, Category,
        DiagnosticSeverity.Error, isEnabledByDefault: true, Description);

    // Keep in mind: you have to list your rules here.
    public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics { get; } =
        ImmutableArray.Create(Rule);

    public override void Initialize(AnalysisContext context)
    {
        // You must call this method to avoid analyzing generated code.
        context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);

        // You must call this method to enable the Concurrent Execution.
        context.EnableConcurrentExecution();

        context.RegisterCompilationStartAction(compilationStartContext =>
        {
            var compilation = compilationStartContext.Compilation;

            // Subscribe to the Syntax Node with the appropriate 'SyntaxKind' (e.g. MethodDeclaration) action.
            compilationStartContext.RegisterSyntaxNodeAction(
                ctx => AnalyzeSyntax(ctx, compilation), 
                SyntaxKind.MethodDeclaration
            );
        });
    }

    /// <summary>
    /// Executed for each Syntax Node with 'SyntaxKind' is 'MethodDeclaration'.
    /// </summary>
    /// <param name="context">Operation context.</param>
    /// <param name="compilation">Representation of a single invocation of the compiler. Provides access to SemanticModel for nodes outside of the operation context.</param>
    private void AnalyzeSyntax(SyntaxNodeAnalysisContext context, Compilation compilation)
    {
        if (!IsWorkflowDefinition(context))
        {
            return;
        }
        
        TraverseNode(context, compilation);
    }
    
    private void TraverseNode(SyntaxNodeAnalysisContext context, Compilation compilation)
    {
        if (!_visitedNodes.Add(context.Node))
        {
            return;
        }
        
        ProcessNode(context, compilation);
    }

    
    private void ProcessNode(SyntaxNodeAnalysisContext context, Compilation compilation)
    {
        if (context.Node is not MethodDeclarationSyntax methodDeclarationNode)
        {
            return;
        }
        
        // Process all child ObjectCreationExpressionSyntax nodes (e.g. new Random())
        ProcessShallowNodes<ObjectCreationExpressionSyntax>(context, Rule, compilation, ObjectCreationExpressionNode.Process);
        
        // Process all child MemberAccessExpressionSyntax nodes
        // that are not part of InvocationExpressionSyntax nodes (e.g. DateTime.Now)
        ProcessShallowNodes<MemberAccessExpressionSyntax>(context, Rule, compilation, (node, ctx, rule, compilationArg) =>
        {
            if (node.Parent is not InvocationExpressionSyntax)
            {
                MemberAccessExpressionNode.Process(node, ctx, rule, compilationArg);
            }
        });
        
        // Process all child IdentifierNameSyntax nodes
        // that are not part of MemberAccessExpressionSyntax nodes (e.g. using static System.DateTime; DateTime today = Today;)
        ProcessShallowNodes<IdentifierNameSyntax>(context, Rule, compilation, (node, ctx, rule, compilationArg) =>
        {
            if (node.Parent is not MemberAccessExpressionSyntax)
            {
                IdentifierNameNode.Process(node, ctx, rule, compilationArg);
            }
        });
        
        // Traverse all child InvocationExpressionSyntax nodes
        var invocationExpressions = methodDeclarationNode.DescendantNodes().OfType<InvocationExpressionSyntax>();
        foreach (var invocation in invocationExpressions)
        {
            var symbolInfo = context.SemanticModel.GetSymbolInfo(invocation);
            if (symbolInfo.Symbol is not IMethodSymbol methodSymbol)
            {
                continue;
            }

            var invokedMethod = invocation.Expression.TryGetInferredMemberName();
            if (invokedMethod is null)
            {
                continue;
            }
            
            var containingType = methodSymbol.ContainingType.ToString();
            var fullInvocation = $"{containingType}.{invokedMethod}";

            // We use StartsWith since InvocationExpressionSyntax includes the method being called and the arguments passed to it.
            // While, on the other hand, MemberAccessExpressionSyntax includes the expression on which the member is accessed and the member itself but not the arguments.
            var containsMethod = _nonDeterminismMap.Keys.Any(key => fullInvocation.StartsWith(key));
            if (!containsMethod)
            {
                var declaringSyntaxReferences = methodSymbol.DeclaringSyntaxReferences.FirstOrDefault();
                if (declaringSyntaxReferences is null)
                {
                    continue;
                }
                var correspondingMethodDeclarationNode = declaringSyntaxReferences.GetSyntax();
                var semanticModel = AnalyzerHelpers.GetSemanticModel(context, correspondingMethodDeclarationNode, compilation);
                
                var childContext = new SyntaxNodeAnalysisContext(
                    correspondingMethodDeclarationNode,
                    semanticModel,
                    context.Options,
                    context.ReportDiagnostic,
                    (Diagnostic _) => true,
                    context.CancellationToken
                );
                
                // Recursively traverse child nodes of the invocationExpression being processed
                TraverseNode(childContext, compilation);
                continue;
            }
            
            var diagnostic = Diagnostic.Create(Rule, invocation.GetLocation(), fullInvocation);
            context.ReportDiagnostic(diagnostic);
        }
    }

    private static bool IsWorkflowDefinition(SyntaxNodeAnalysisContext context)
    {
        if (context.Node is not MethodDeclarationSyntax methodDeclarationNode)
        {
            return false;
        }

        var attributes = methodDeclarationNode.AttributeLists;
        var isWorkflowDefinition = false;

        if (attributes.Count == 0)
        {
            return isWorkflowDefinition;
        }
        
        foreach (var attributeList in attributes)
        {
            foreach (var attribute in attributeList.Attributes)
            {
                var attributeName = attribute.Name;

                if (!attributeName.ToString().Equals(WorkflowDefinitionMarker))
                {
                    continue;
                }
                
                isWorkflowDefinition = true;
                break;
            }
            
            if (isWorkflowDefinition)
            {
                break;
            }
        }

        return isWorkflowDefinition;
    }
    
    private static void ProcessShallowNodes<TSyntax>(
        SyntaxNodeAnalysisContext context, 
        DiagnosticDescriptor rule,
        Compilation compilation,
        Action<TSyntax, SyntaxNodeAnalysisContext, DiagnosticDescriptor, Compilation> processAction)
        where TSyntax : CSharpSyntaxNode
    {
        var nodes = context.Node.DescendantNodes().OfType<TSyntax>();
        foreach (var node in nodes)
        {
            processAction(node, context, rule, compilation);
        }
    }
}