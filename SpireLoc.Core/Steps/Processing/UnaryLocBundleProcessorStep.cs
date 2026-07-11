using SpireLoc.Core.Diagnostics;
using SpireLoc.Core.Execution;
using SpireLoc.Core.Models;
using SpireLoc.Core.Transformations;

namespace SpireLoc.Core.Steps.Processing;

/// <summary>Applies a pure bundle processor to a source workspace slot and writes its result to another slot.</summary>
public sealed class UnaryLocBundleProcessorStep : ILocOperation
{
    public UnaryLocBundleProcessorStep(
        UnaryLocBundleProcessor processor,
        string fromSlot,
        string toSlot)
    {
        ArgumentNullException.ThrowIfNull(processor);
        LocWorkspace.ValidateSlotName(fromSlot);
        LocWorkspace.ValidateSlotName(toSlot);

        Processor = processor;
        FromSlot = fromSlot;
        ToSlot = toSlot;
    }

    public UnaryLocBundleProcessor Processor { get; }
    public string FromSlot { get; }
    public string ToSlot { get; }

    public LocOperationResult Execute(LocWorkspace workspace, LocExecutionContext context)
    {
        ArgumentNullException.ThrowIfNull(workspace);
        ArgumentNullException.ThrowIfNull(context);

        LocBundle input;
        try
        {
            input = workspace.Require<LocBundle>(FromSlot);
        }
        catch (LocWorkspaceException exception)
        {
            return Failure(workspace, "UnaryLocBundleProcessorStep.Input", exception.Message);
        }

        try
        {
            var output = Processor.Process(input)
                ?? throw new InvalidOperationException("The bundle processor returned null.");
            return new LocOperationResult(workspace.Set(ToSlot, output));
        }
        catch (Exception exception)
        {
            return Failure(workspace, "UnaryLocBundleProcessorStep.Process", exception.Message);
        }
    }

    private static LocOperationResult Failure(LocWorkspace workspace, string code, string message) =>
        new(workspace, [Diagnostic.Error(code, message)], LocOperationStatus.Failed);
}
