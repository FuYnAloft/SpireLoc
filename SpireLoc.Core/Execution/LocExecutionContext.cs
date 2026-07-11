namespace SpireLoc.Core.Execution;

/// <summary>Runtime services and options for operations. It is deliberately separate from workspace state.</summary>
public sealed class LocExecutionContext
{
    public static LocExecutionContext Default { get; } = new();
}
