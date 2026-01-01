namespace WorkflowCheck.Processors;

using System.Collections.Generic;
using System.Linq;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Diagnostics;

public static class MemberAccessExpressionNodeProcessor
{
    private static readonly Dictionary<string, bool> MemberAccessExpressionNodes = new()
    {
        // Date and Time
        { "System.DateTime.Now", true },
        { "System.DateTime.UtcNow", true },
        { "System.DateTime.Today", true },
        
        // Environment Information (System.Environment)
        { "System.Environment.TickCount", true },
        { "System.Environment.TickCount64", true },
        { "System.Environment.MachineName", true },
        { "System.Environment.UserName", true },
        { "System.Environment.UserDomainName", true },
        { "System.Environment.ProcessorCount", true },
        { "System.Environment.WorkingSet", true },
        { "System.Environment.CurrentDirectory", true },
        { "System.Environment.SystemDirectory", true },
        { "System.Environment.CommandLine", true },
        { "System.Environment.ExitCode", true },
        
        // Process and Diagnostics (System.Diagnostics)
        { "System.Diagnostics.PerformanceCounter.RawValue", true },
        { "System.Diagnostics.Stopwatch.ElapsedMilliseconds", true },
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