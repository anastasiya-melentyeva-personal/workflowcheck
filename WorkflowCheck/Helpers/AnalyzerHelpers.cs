namespace WorkflowCheck.Helpers;

using System.Collections.Concurrent;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Diagnostics;

public static class AnalyzerHelpers
{
    private static readonly ConcurrentDictionary<SyntaxNode, SemanticModel> SemanticModelCache = new();

    // Get the semantic model for the given syntax node
    // NOTE:
    // If a Workflow invokes code whose definition is in a different file
    // then in order to analyze it, we need to get its syntax tree and semantic model from the compilation
    public static SemanticModel GetSemanticModel(SyntaxNodeAnalysisContext context, SyntaxNode node, Compilation compilation)
    {
        return SemanticModelCache.GetOrAdd(node, n =>
            context.SemanticModel.SyntaxTree == n.SyntaxTree ? context.SemanticModel : compilation.GetSemanticModel(n.SyntaxTree));
    }
}