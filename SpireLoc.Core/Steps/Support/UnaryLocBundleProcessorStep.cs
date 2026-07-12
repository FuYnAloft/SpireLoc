using SpireLoc.Core.Diagnostics;
using SpireLoc.Core.Execution;
using SpireLoc.Core.Models;

namespace SpireLoc.Core.Steps.Support;

/// <summary>Applies a pure bundle processor to a source workspace slot and writes its result to another slot.</summary>
public sealed class UnaryLocBundleProcessorStep : ILocOperation
{
    public const string DefaultSlotName = "main";

    public UnaryLocBundleProcessorStep(
        UnaryLocBundleProcessor processor,
        string fromSlot = DefaultSlotName,
        string toSlot = DefaultSlotName)
    {
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
        LocBundle input;
        var diagnostics = new DiagnosticCollection();
        try
        {
            input = workspace.Require<LocBundle>(FromSlot);
        }
        catch (LocWorkspaceException exception)
        {
            diagnostics.AddException("UnaryLocBundleProcessorStep.Input", exception);
            return new LocOperationResult(workspace, diagnostics, LocOperationStatus.Failed);
        }

        try
        {
            var output = Processor.Process(input, diagnostics);
            return new LocOperationResult(
                workspace.Set(ToSlot, output),
                diagnostics,
                diagnostics.HasErrors ? LocOperationStatus.Failed : LocOperationStatus.Succeeded);
        }
        catch (Exception exception)
        {
            diagnostics.AddException("UnaryLocBundleProcessorStep.Process", exception);
            return new LocOperationResult(workspace, diagnostics, LocOperationStatus.Failed);
        }
    }
}
