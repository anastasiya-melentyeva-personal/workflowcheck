namespace WorkflowCheck.Sample;

using System;

[AttributeUsage(AttributeTargets.Method, Inherited = false, AllowMultiple = false)]
public sealed class WorkflowRunAttribute : Attribute
{
}