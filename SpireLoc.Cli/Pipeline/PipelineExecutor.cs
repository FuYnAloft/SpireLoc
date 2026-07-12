using SpireLoc.Cli.Registration;
using SpireLoc.Core.Diagnostics;
using SpireLoc.Core.Execution;

namespace SpireLoc.Cli.Pipeline;

internal sealed class PipelineExecutor(OperationRegistry registry, TextWriter output, TextWriter error)
{
    public int Execute(IReadOnlyList<OperationInvocationSpec> invocations)
    {
        var operations = new OperationCompiler(registry).Compile(invocations);
        var result = new LocOperationRunner().Run(LocWorkspace.Empty, operations);

        foreach (var diagnostic in result.Diagnostics)
        {
            var writer = diagnostic.Severity == DiagnosticSeverity.Info ? output : error;
            writer.WriteLine($"{diagnostic.Severity.ToString().ToLowerInvariant()}: [{diagnostic.Code}] {diagnostic.Message}");
        }

        return result.Diagnostics.HasErrors ? 1 : 0;
    }
}
