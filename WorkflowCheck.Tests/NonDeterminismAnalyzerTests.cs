namespace WorkflowCheck.Tests;

using System.Threading.Tasks;
using Xunit;
using Verifier = Microsoft.CodeAnalysis.CSharp.Testing.XUnit.AnalyzerVerifier<NonDeterminismAnalyzer>;

public class TemporalWorkflowAnalyzerTests
{
    [Fact]
    public async Task TemporalWorkflowCheckAnalyzer_WorkflowDefinitionContainsNonDeterministicInvocationExpression_AlertDiagnostic()
    {
        const string text = @"
using System;
using System.Threading.Tasks;
using System.Security.Cryptography;

// mock implementation of the WorkflowRunAttribute attribute
[AttributeUsage(AttributeTargets.Method, Inherited = false, AllowMultiple = false)]
public sealed class WorkflowRunAttribute : Attribute
{
}

public class Workflow
{
    [WorkflowRun]
    public void RunAsync(string request)
    {
        Console.Write(Guid.NewGuid());
        Console.Write(RandomNumberGenerator.Create());
    }
}
";

        var expected = new []
        {
            Verifier.Diagnostic()
                .WithSpan(17, 23, 17, 37)
                .WithArguments("System.Guid.NewGuid"),
            Verifier.Diagnostic()
                .WithSpan(18, 23, 18, 53)
                .WithArguments("System.Security.Cryptography.RandomNumberGenerator.Create"),
        };
        await Verifier.VerifyAnalyzerAsync(text, expected).ConfigureAwait(false);
    }

    [Fact]
    public async Task TemporalWorkflowCheckAnalyzer_WorkflowDefinitionContainsNonDeterministicChildNode_AlertDiagnostic()
    {
        const string text = @"
using System;
using System.Threading.Tasks;

// mock implementation of the WorkflowRunAttribute attribute
[AttributeUsage(AttributeTargets.Method, Inherited = false, AllowMultiple = false)]
public sealed class WorkflowRunAttribute : Attribute
{
}

public class Workflow
{
    [WorkflowRun]
    public void RunAsync(string request)
    {
        TestFunc();
    }

    private void TestFunc()
    {
        Console.Write(Guid.NewGuid());
    }
}
";

        var expected = Verifier.Diagnostic()
            .WithSpan(21, 23, 21, 37)
            .WithArguments("System.Guid.NewGuid");
        await Verifier.VerifyAnalyzerAsync(text, expected).ConfigureAwait(false);
    }

    [Fact]
    public async Task TemporalWorkflowCheckAnalyzer_WorkflowDefinitionContainsMemberAccessExpressionNode_AlertDiagnostic()
    {
        const string text = @"
using System;
using System.Threading.Tasks;

// mock implementation of the WorkflowRunAttribute attribute
[AttributeUsage(AttributeTargets.Method, Inherited = false, AllowMultiple = false)]
public sealed class WorkflowRunAttribute : Attribute
{
}

public class Workflow
{
    [WorkflowRun]
    public void RunAsync(string request)
    {
        Console.Write(DateTime.Now);
        Console.Write(new DateTime(2008, 6, 1, 7, 47, 0).Date);
    }
}
";

        var expected = Verifier.Diagnostic()
            .WithSpan(16, 23, 16, 35)
            .WithArguments("System.DateTime.Now");
        await Verifier.VerifyAnalyzerAsync(text, expected).ConfigureAwait(false);
    }
    
    [Fact]
    public async Task TemporalWorkflowCheckAnalyzer_WorkflowDefinitionCallsFunctionContainsNonDeterministicNodTwice_NodeVisitedOnce()
    {
        const string text = @"
using System;
using System.Threading.Tasks;

// mock implementation of the WorkflowRunAttribute attribute
[AttributeUsage(AttributeTargets.Method, Inherited = false, AllowMultiple = false)]
public sealed class WorkflowRunAttribute : Attribute
{
}

public class Workflow
{
    [WorkflowRun]
    public void RunAsync(string request)
    {
        TestFunc();
        TestFunc();
    }

    private void TestFunc()
    {
        Console.Write(Guid.NewGuid());
    }
}
";

        var expected = Verifier.Diagnostic()
            .WithSpan(22, 23, 22, 37)
            .WithArguments("System.Guid.NewGuid");
        await Verifier.VerifyAnalyzerAsync(text, expected).ConfigureAwait(false);
    }
    
    [Fact]
    public async Task TemporalWorkflowCheckAnalyzer_WorkflowDefinitionContainsNonDeterministicObjectCreationNode_AlertDiagnostic()
    {
        const string text = @"
using System;
using System.Threading.Tasks;

// mock implementation of the WorkflowRunAttribute attribute
[AttributeUsage(AttributeTargets.Method, Inherited = false, AllowMultiple = false)]
public sealed class WorkflowRunAttribute : Attribute
{
}

public class Workflow
{
    [WorkflowRun]
    public void RunAsync(string request)
    {
        var random = new Random();
        Console.Write(@""Random value: {0}"", random);
        TestFunc();
    }

    private void TestFunc()
    {
        Guid guid = new Guid();
        Console.Write(@""Random Guid: {0}"", guid);;
    }
}
";

        var expected = Verifier.Diagnostic()
                .WithSpan(16, 22, 16, 34)
                .WithArguments("new Random()");
        await Verifier.VerifyAnalyzerAsync(text, expected).ConfigureAwait(false);
    }
    
    [Fact]
    public async Task TemporalWorkflowCheckAnalyzer_WorkflowDefinitionContainsIdentifierNameNodes_AlertDiagnostic()
    {
        const string text = @"
using System;
using System.Threading.Tasks;
using static System.DateTime;

// mock implementation of the WorkflowRunAttribute attribute
[AttributeUsage(AttributeTargets.Method, Inherited = false, AllowMultiple = false)]
public sealed class WorkflowRunAttribute : Attribute
{
}

public class Workflow
{
    [WorkflowRun]
    public void RunAsync(string request)
    {
        DateTime today = Today;
        Console.Write(@""Today is: {0}"", today);
        TestFunc();
    }

    private void TestFunc()
    {
        DateTime now = Now;
        Console.Write(@""Now is: {0}"", now);
    }
}
";

        var expected = new []{
            Verifier.Diagnostic()
            .WithSpan(17, 26, 17, 31)
            .WithArguments("System.DateTime.Today"),
            Verifier.Diagnostic()
                .WithSpan(24, 24, 24, 27)
                .WithArguments("System.DateTime.Now")
        };
        await Verifier.VerifyAnalyzerAsync(text, expected).ConfigureAwait(false);
    }
}