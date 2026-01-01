namespace WorkflowCheck;

using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Diagnostics;
using Processors;

/// <summary>
/// Analyzer that reports non-determinism found in Temporal Workflow definition.
/// Traverses through the Syntax Tree and checks for pre-determined non-deterministic nodes of types
/// - InvocationExpressionSyntax
/// - IdentifierNameSyntax
/// - MemberAccessExpressionSyntax
/// - ObjectCreationExpressionSyntax
/// </summary>
[DiagnosticAnalyzer(LanguageNames.CSharp)]
public class NonDeterminismAnalyzer : DiagnosticAnalyzer
{
    private readonly HashSet<SyntaxNode> _visitedNodes = new();

    private const string WorkflowDefinitionMarker = "WorkflowRun";

    private readonly Dictionary<string, bool> _nonDeterministicInvocationExpressions = new()
    {
        // Randomization and Cryptography
        { "System.Guid.NewGuid", true },
        { "System.Guid.CreateVersion7", true },
        { "System.Security.Cryptography.RandomNumberGenerator", true },
        
        // Networking (System.Net)
        { "System.Net.Http.HttpClient.GetAsync", true },
        { "System.Net.Http.HttpClient.PostAsync", true },
        { "System.Net.Http.HttpClient.PutAsync", true },
        { "System.Net.Http.HttpClient.DeleteAsync", true },
        { "System.Net.Http.HttpClient.SendAsync", true },
        { "System.Net.Http.HttpClient.GetStringAsync", true },
        { "System.Net.Http.HttpClient.GetByteArrayAsync", true },
        { "System.Net.Http.HttpClient.GetStreamAsync", true },
        { "System.Net.Dns.GetHostAddresses", true },
        { "System.Net.Dns.GetHostEntry", true },
        { "System.Net.Dns.GetHostByName", true },
        { "System.Net.WebClient.DownloadString", true },
        { "System.Net.WebClient.DownloadData", true },
        { "System.Net.WebClient.UploadString", true },
        { "System.Net.WebClient.UploadData", true },
        { "System.Net.Sockets.Socket.Connect", true },
        { "System.Net.Sockets.Socket.Accept", true },
        { "System.Net.Sockets.Socket.Receive", true },
        { "System.Net.Sockets.Socket.Send", true },
        { "System.Net.NetworkInformation.NetworkInterface.GetAllNetworkInterfaces", true },
        { "System.Net.NetworkInformation.IPGlobalProperties.GetIPGlobalProperties", true },
        
        // File I/O (System.IO)
        { "System.IO.File.ReadAllText", true },
        { "System.IO.File.ReadAllBytes", true },
        { "System.IO.File.WriteAllText", true },
        { "System.IO.File.WriteAllBytes", true },
        { "System.IO.File.Exists", true },
        { "System.IO.File.GetCreationTime", true },
        { "System.IO.File.GetLastWriteTime", true },
        { "System.IO.File.GetAttributes", true },
        { "System.IO.Directory.GetFiles", true },
        { "System.IO.Directory.GetDirectories", true },
        { "System.IO.Directory.Exists", true },
        { "System.IO.Directory.GetCreationTime", true },
        { "System.IO.Directory.GetLastWriteTime", true },
        { "System.IO.FileStream.Read", true },
        { "System.IO.FileStream.Write", true },
        { "System.IO.Stream.ReadByte", true },
        { "System.IO.Stream.WriteByte", true },
        { "System.IO.StreamReader.ReadLine", true },
        { "System.IO.StreamReader.ReadToEnd", true },
        
        // Threading and Timing (System.Threading)
        { "System.Threading.Thread.Sleep", true },
        { "System.Threading.Task.Delay", true },
        { "System.Threading.Task.Run", true },
        { "System.Threading.PeriodicTimer.WaitForNextTickAsync", true },
        { "System.Threading.Monitor.Wait", true },
        { "System.Threading.Monitor.Pulse", true },
        { "System.Threading.Semaphore.WaitOne", true },
        { "System.Threading.ManualResetEvent.WaitOne", true },
        { "System.Threading.ThreadPool.QueueUserWorkItem", true },
        
        // Environment Information (System.Environment)
        { "System.Environment.GetEnvironmentVariable", true },
        { "System.Environment.GetEnvironmentVariables", true },
        { "System.Environment.GetFolderPath", true },
        { "System.Environment.GetLogicalDrives", true },
        
        // Process and Diagnostics (System.Diagnostics)
        { "System.Diagnostics.Process.Start", true },
        { "System.Diagnostics.Process.GetProcesses", true },
        { "System.Diagnostics.Process.GetCurrentProcess", true },
        { "System.Diagnostics.Process.Kill", true },
        { "System.Diagnostics.PerformanceCounter.NextValue", true },
        { "System.Diagnostics.PerformanceCounterCategory.GetCounters", true },
        { "System.Diagnostics.EventLog.WriteEntry", true },
        { "System.Diagnostics.EventLog.GetEventLogs", true },
        { "System.Diagnostics.Stopwatch.StartNew", true },
        { "System.Diagnostics.Stopwatch.Start", true },
        { "System.Diagnostics.Stopwatch.Stop", true },
        
        // Registry Access (Windows)
        { "Microsoft.Win32.Registry.GetValue", true },
        { "Microsoft.Win32.RegistryKey.GetValueNames", true }
    };

    // The Preferred format of DiagnosticId is Your Prefix + Number, e.g., CA1234.
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

            // Subscribe to the Syntax Node with the appropriate 'SyntaxKind' (e.g., MethodDeclaration) action.
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
    /// <param name="compilation">Representation of a single invocation of the compiler. Provides access to SemanticModel for nodes outside the operation context.</param>
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
        ProcessShallowNodes<ObjectCreationExpressionSyntax>(context, Rule, compilation, ObjectCreationExpressionNodeProcessor.Process);

        // Process all child MemberAccessExpressionSyntax nodes
        // that are not part of InvocationExpressionSyntax nodes (e.g., DateTime.Now)
        ProcessShallowNodes<MemberAccessExpressionSyntax>(context, Rule, compilation, (node, ctx, rule, compilationArg) =>
        {
            if (node.Parent is not InvocationExpressionSyntax)
            {
                MemberAccessExpressionNodeProcessor.Process(node, ctx, rule, compilationArg);
            }
        });

        // Process all child IdentifierNameSyntax nodes
        // that are not part of MemberAccessExpressionSyntax nodes (e.g., using static System.DateTime; DateTime today = Today;)
        ProcessShallowNodes<IdentifierNameSyntax>(context, Rule, compilation, (node, ctx, rule, compilationArg) =>
        {
            if (node.Parent is not MemberAccessExpressionSyntax)
            {
                IdentifierNameNodeProcessor.Process(node, ctx, rule, compilationArg);
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
            var containsMethod = _nonDeterministicInvocationExpressions.Keys.Any(key => fullInvocation.StartsWith(key));
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