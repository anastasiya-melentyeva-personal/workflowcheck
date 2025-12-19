# workflowcheck
Temporal Workflow Check is a Roslyn Analyzer that statically analyzes [Temporal Workflows Definitions](https://docs.temporal.io/workflows) written in .NET (i.e. functions with `[WorkflowRun]` Attribute) to report presence of the intrinsic nondeterminism causes either directly in the function or in a function called by the Workflow.

| Identifier                        | Name                 | Description                                               |
|-----------------------------------|----------------------|-----------------------------------------------------------|
| [WF0001](documentation/WF0001.md) | NonDeterministicCode | Temporal Workflow definition uses non-deterministic code. |

**You must build this project to see the results (warnings) in the IDE.**

### TemporalWorkflowCheckAnalyzer.Sample
A project containing a sample workflow that references the analyzers. Note the parameter of `ProjectReference` in [TemporalWorkflowCheckAnalyzer.Sample.csproj](../TemporalWorkflowCheckAnalyzer.Sample/TemporalWorkflowCheckAnalyzer.Sample.csproj), they make sure that the project is referenced by the analyzer.

### TemporalWorkflowCheckAnalyzer.Tests
Unit tests for the TemporalWorkflowCheckAnalyzer.

## How To?
### How to debug?
- Use the [launchSettings.json](Properties/launchSettings.json) profile.
- Debug tests.

### How to build the NuGet package?

To build the analyzer as a NuGet package:

1. **Prerequisites:**
   - [.NET SDK 6.0 or later](https://dotnet.microsoft.com/download) installed.

2. **Build the package:**
   - Open a terminal in the repository root.
   - Run:
     ```sh
     dotnet build -c Release WorkflowCheck/WorkflowCheck.csproj
     ```
     or to generate the NuGet package directly:
     ```sh
     dotnet pack -c Release WorkflowCheck/WorkflowCheck.csproj
     ```

3. **Find the package:**
   - The generated `.nupkg` file will be in `WorkflowCheck/bin/Release/`.

4. **Use the package:**
   - You can now publish the `.nupkg` to a NuGet feed or reference it locally in other projects.

### Learn more about wiring analyzers
The complete set of information is available at [roslyn github repo wiki](https://github.com/dotnet/roslyn/blob/main/docs/wiki/README.md).