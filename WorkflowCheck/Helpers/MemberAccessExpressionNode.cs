namespace WorkflowCheck.Helpers;

using System.Collections.Generic;
using System.Linq;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Diagnostics;

public static class MemberAccessExpressionNode
{
    private static readonly Dictionary<string, bool> MemberAccessExpressionNodes = new()
    {
        { "System.DateTime.Now", true },
        { "System.DateTime.UtcNow", true },
        { "System.DateTime.Today", true }
    };
    
    public static void Process(CSharpSyntaxNode syntaxNode, SyntaxNodeAnalysisContext context,
        DiagnosticDescriptor rule, Compilation compilation)
    {
        if (syntaxNode is not MemberAccessExpressionSyntax memberAccessExpressionNode)
        {
            return;
        }
        
        var semanticModel = AnalyzerHelpers.GetSemanticModel(context, syntaxNode, compilation);
        var symbolInfo = semanticModel.GetSymbolInfo(memberAccessExpressionNode);
        if (symbolInfo.Symbol is null)
        {
            return;
        }

        var containsMethod = MemberAccessExpressionNodes.Keys.Any(key =>
            symbolInfo.Symbol.ToString().Equals(key));
        if (!containsMethod)
        {
            return;
        }
        
        var diagnostic = Diagnostic.Create(rule, memberAccessExpressionNode.GetLocation(), symbolInfo.Symbol);
        context.ReportDiagnostic(diagnostic);
    }
}