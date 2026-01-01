namespace WorkflowCheck.Tests;

using System.Threading.Tasks;
using Xunit;
using Verifier = Microsoft.CodeAnalysis.CSharp.Testing.XUnit.AnalyzerVerifier<NonDeterminismAnalyzer>;

public class TemporalWorkflowAnalyzerTests
{
    [Fact]
    public async Task WorkflowCheck_ContainsNonDeterministicInvocationExpression_AlertDiagnostic()
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

        var expected = new[]
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
    public async Task WorkflowCheck_ContainsNonDeterministicChildNode_AlertDiagnostic()
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
    public async Task WorkflowCheck_ContainsMemberAccessExpressionNode_AlertDiagnostic()
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
    public async Task WorkflowCheck_ContainsNonDeterministicNodeTwice_NodeVisitedOnce()
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
    public async Task WorkflowCheck_ContainsNonDeterministicObjectCreationNode_AlertDiagnostic()
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
    public async Task WorkflowCheck_ContainsIdentifierNameNodes_AlertDiagnostic()
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

        var expected = new[]{
            Verifier.Diagnostic()
            .WithSpan(17, 26, 17, 31)
            .WithArguments("System.DateTime.Today"),
            Verifier.Diagnostic()
                .WithSpan(24, 24, 24, 27)
                .WithArguments("System.DateTime.Now")
        };
        await Verifier.VerifyAnalyzerAsync(text, expected).ConfigureAwait(false);
    }

    [Fact]
    public async Task WorkflowCheck_ContainsDateTimeMethods_AlertDiagnostic()
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
        Console.Write(DateTime.UtcNow);
        Console.Write(DateTime.Today);
    }
}
";

        var expected = new[]
        {
            Verifier.Diagnostic()
                .WithSpan(16, 23, 16, 38)
                .WithArguments("System.DateTime.UtcNow"),
            Verifier.Diagnostic()
                .WithSpan(17, 23, 17, 37)
                .WithArguments("System.DateTime.Today")
        };
        await Verifier.VerifyAnalyzerAsync(text, expected).ConfigureAwait(false);
    }

    [Fact]
    public async Task WorkflowCheck_ContainsFileIOOperations_AlertDiagnostic()
    {
        const string text = @"
using System;
using System.IO;
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
        File.ReadAllText(""test.txt"");
        File.Exists(""test.txt"");
        Directory.GetFiles(""."");
    }
}
";

        var expected = new[]
        {
            Verifier.Diagnostic()
                .WithSpan(17, 9, 17, 37)
                .WithArguments("System.IO.File.ReadAllText"),
            Verifier.Diagnostic()
                .WithSpan(18, 9, 18, 32)
                .WithArguments("System.IO.File.Exists"),
            Verifier.Diagnostic()
                .WithSpan(19, 9, 19, 32)
                .WithArguments("System.IO.Directory.GetFiles")
        };
        await Verifier.VerifyAnalyzerAsync(text, expected).ConfigureAwait(false);
    }

    [Fact]
    public async Task WorkflowCheck_ContainsThreadingOperations_AlertDiagnostic()
    {
        const string text = @"
using System;
using System.Threading;
using System.Threading.Tasks;

// mock implementation of the WorkflowRunAttribute attribute
[AttributeUsage(AttributeTargets.Method, Inherited = false, AllowMultiple = false)]
public sealed class WorkflowRunAttribute : Attribute
{
}

public class Workflow
{
    [WorkflowRun]
    public async Task RunAsync(string request)
    {
        Thread.Sleep(1000);
        ThreadPool.QueueUserWorkItem(_ => {});
    }
}
";

        var expected = new[]
        {
            Verifier.Diagnostic()
                .WithSpan(17, 9, 17, 27)
                .WithArguments("System.Threading.Thread.Sleep"),
            Verifier.Diagnostic()
                .WithSpan(18, 9, 18, 46)
                .WithArguments("System.Threading.ThreadPool.QueueUserWorkItem")
        };
        await Verifier.VerifyAnalyzerAsync(text, expected).ConfigureAwait(false);
    }

    [Fact]
    public async Task WorkflowCheck_ContainsEnvironmentAccess_AlertDiagnostic()
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
        Console.Write(Environment.GetEnvironmentVariable(""PATH""));
    }
}
";

        var expected = Verifier.Diagnostic()
            .WithSpan(16, 23, 16, 65)
            .WithArguments("System.Environment.GetEnvironmentVariable");
        await Verifier.VerifyAnalyzerAsync(text, expected).ConfigureAwait(false);
    }

    [Fact]
    public async Task WorkflowCheck_ContainsProcessOperations_AlertDiagnostic()
    {
        const string text = @"
using System;
using System.Diagnostics;
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
        Process.Start(""notepad.exe"");
        Process.GetProcesses();
        var stopwatch = Stopwatch.StartNew();
    }
}
";

        var expected = new[]
        {
            Verifier.Diagnostic()
                .WithSpan(17, 9, 17, 37)
                .WithArguments("System.Diagnostics.Process.Start"),
            Verifier.Diagnostic()
                .WithSpan(18, 9, 18, 31)
                .WithArguments("System.Diagnostics.Process.GetProcesses"),
            Verifier.Diagnostic()
                .WithSpan(19, 25, 19, 45)
                .WithArguments("System.Diagnostics.Stopwatch.StartNew")
        };
        await Verifier.VerifyAnalyzerAsync(text, expected).ConfigureAwait(false);
    }
}