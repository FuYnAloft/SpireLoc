using SpireLoc.Cli.Registration;
using SpireLoc.Core.Diagnostics;
using SpireLoc.Core.Execution;

namespace SpireLoc.Cli.Pipe;

internal sealed class PipeCommand(OperationRegistry registry, TextWriter output, TextWriter error)
{
    public int Run(IReadOnlyList<string> args)
    {
        var operations = new PipeParser(registry).Parse(args);
        var result = new LocOperationRunner().Run(LocWorkspace.Empty, operations);

        foreach (var diagnostic in result.Diagnostics)
        {
            var writer = diagnostic.Severity == DiagnosticSeverity.Info ? output : error;
            writer.WriteLine($"{diagnostic.Severity.ToString().ToLowerInvariant()}: [{diagnostic.Code}] {diagnostic.Message}");
        }

        return result.Diagnostics.HasErrors ? 1 : 0;
    }
}
