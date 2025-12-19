namespace WorkflowCheck.Sample;

using System;
using System.Security.Cryptography;
using System.Threading.Tasks;
using static System.DateTime;

[Workflow]
public class WorkflowSample
{
    [WorkflowRun]
    public async Task<int> RunAsync()
    {
        var randomNumber = RandomNumberGenerator.Create();
        Console.Write(@"Random value: {0}", randomNumber);
        
        var anotherRandomNumber = new Random();
        var randomValue = anotherRandomNumber.NextInt64(1, 5);
        Console.Write(@"Another Random value: {0}", randomValue);
        
        Console.Write(@"Today's Date is relative: {0}", DateTime.Today);
        
        // Not expected to produce a diagnostic
        Console.Write(@"But {0} is not a relative date", new DateTime(2025, 1, 10).Date);

        WorkflowSampleHelpers.GetToday();

        return await WorkflowDefinition();
    }

    private Task<int> WorkflowDefinition()
    {
        DateTime y = Today;

        PrintRandomGuid();
        return Task.FromResult(0);
    }
    
    private static void PrintRandomGuid()
    {
        // Not expected to produce a diagnostic
        Guid emptyGuid = new Guid();
        Console.Write(@"Empty Guid: {0}", emptyGuid);

        Console.Write(@"Another random Guid: {0}", Guid.NewGuid());
    }
}