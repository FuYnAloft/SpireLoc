using SpireLoc.Cli.Actions;
using SpireLoc.Cli.Pipe;
using SpireLoc.Cli.Registration;
using SpireLoc.Core.Execution;

namespace SpireLoc.Cli;

internal static class Program
{
    public static int Main(string[] args)
    {
        try
        {
            if (args.Length == 0 || args is ["--help" or "-h"])
            {
                PrintRootHelp();
                return 0;
            }

            var registry = OperationRegistry.Scan(typeof(ILocOperation).Assembly);
            if (string.Equals(args[0], "pipe", StringComparison.Ordinal))
            {
                if (args is [_, "--help" or "-h"])
                {
                    PrintPipeHelp(registry);
                    return 0;
                }

                return new PipeCommand(registry, Console.Out, Console.Error).Run(args[1..]);
            }

            if (string.Equals(args[0], "action", StringComparison.Ordinal))
                return RunAction(args, registry);

            throw new CliException($"Unknown command '{args[0]}'. Expected 'pipe' or 'action'.");
        }
        catch (CliException exception)
        {
            Console.Error.WriteLine($"error: {exception.Message}");
            return 2;
        }
    }

    private static void PrintRootHelp()
    {
        Console.WriteLine("Usage: spireloc <command>");
        Console.WriteLine();
        Console.WriteLine("Commands:");
        Console.WriteLine("  pipe    Build and execute a localization operation pipeline.");
        Console.WriteLine("  action  Run a reusable YAML action.");
    }

    private static void PrintPipeHelp(OperationRegistry registry)
    {
        Console.WriteLine("Usage: spireloc pipe <steps>");
        Console.WriteLine();
        Console.WriteLine("Registered steps:");
        foreach (var descriptor in registry.Descriptors)
            Console.WriteLine($"  {descriptor.GetUsage()}");
        Console.WriteLine("  --action <file> [action arguments]");
    }

    private static int RunAction(IReadOnlyList<string> args, OperationRegistry registry)
    {
        if (args.Count == 1 || args.Count == 2 && args[1] is "--help" or "-h")
        {
            Console.WriteLine("Usage: spireloc action run <file> [action arguments]");
            return 0;
        }
        if (!string.Equals(args[1], "run", StringComparison.Ordinal))
            throw new CliException($"Unknown action command '{args[1]}'. Expected 'run'.");
        if (args is [_, _, "--help" or "-h"])
        {
            Console.WriteLine("Usage: spireloc action run <file> [action arguments]");
            return 0;
        }
        if (args.Count < 3)
            throw new CliException("The 'action run' command requires an action file path.");

        return new ActionCommand(registry, Console.Out, Console.Error).Run(args[2], args.Skip(3).ToArray());
    }
}
