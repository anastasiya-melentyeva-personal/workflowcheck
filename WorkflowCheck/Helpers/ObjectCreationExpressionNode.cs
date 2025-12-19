namespace WorkflowCheck.Helpers;

using System.Collections.Generic;
using System.Linq;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Diagnostics;

public static class ObjectCreationExpressionNode
{
    private static readonly Dictionary<string, bool> ObjectCreationExpressionNodes = new()
    {
        { "System.Random", true },
    };

    public static void Process(CSharpSyntaxNode syntaxNode, SyntaxNodeAnalysisContext context,
        DiagnosticDescriptor rule, Compilation compilation)
    {
        if (syntaxNode is not ObjectCreationExpressionSyntax objectCreationExpressionNode)
        {
            return;
        }

        var semanticModel = AnalyzerHelpers.GetSemanticModel(context, syntaxNode, compilation);
        if (semanticModel.GetSymbolInfo(objectCreationExpressionNode.Type).Symbol is not ITypeSymbol typeSymbol)
        {
            return;
        }

        var containsMethod = ObjectCreationExpressionNodes.Keys.Any(key => typeSymbol.ToString().Equals(key));
        if (!containsMethod)
        {
            return;
        }

        var diagnostic = Diagnostic.Create(rule, objectCreationExpressionNode.GetLocation(), objectCreationExpressionNode.ToString());
        context.ReportDiagnostic(diagnostic);
    }
}