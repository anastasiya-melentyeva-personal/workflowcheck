namespace WorkflowCheck.Helpers;

using System.Collections.Generic;
using System.Linq;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Diagnostics;

public static class IdentifierNameNode
{
    private static readonly Dictionary<string, bool> IdentifierNameNodes = new()
    {
        { "System.DateTime.Now", true },
        { "System.DateTime.UtcNow", true },
        { "System.DateTime.Today", true }
    };

    public static void Process(CSharpSyntaxNode syntaxNode, SyntaxNodeAnalysisContext context,
        DiagnosticDescriptor rule, Compilation compilation)
    {
        if (syntaxNode is not IdentifierNameSyntax identifierNameNode)
        {
            return;
        }

        var semanticModel = AnalyzerHelpers.GetSemanticModel(context, syntaxNode, compilation);
        var symbolInfo = semanticModel.GetSymbolInfo(identifierNameNode);
        if (symbolInfo.Symbol is null)
        {
            return;
        }

        var containsMethod = IdentifierNameNodes.Keys.Any(key => symbolInfo.Symbol.ToString().Equals(key));
        if (!containsMethod)
        {
            return;
        }

        var diagnostic = Diagnostic.Create(rule, identifierNameNode.GetLocation(), symbolInfo.Symbol);
        context.ReportDiagnostic(diagnostic);
    }
}