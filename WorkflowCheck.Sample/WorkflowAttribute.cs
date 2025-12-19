namespace WorkflowCheck.Sample;

using System;

[AttributeUsage(AttributeTargets.Class, Inherited = false, AllowMultiple = false)]
public sealed class WorkflowAttribute : Attribute
{
}