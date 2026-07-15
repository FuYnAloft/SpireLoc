using System.Reflection;
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

            if (args is ["--version"])
            {
                Console.WriteLine($"spireloc {GetVersion()}");
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

            if (string.Equals(args[0], "operation", StringComparison.Ordinal))
                return RunOperation(args, registry);

            if (args[0] is "baselib" or "ritsulib")
            {
                return RunAction(
                    ["action", "run", args[0], .. args[1..]],
                    registry);
            }

            throw new CliException(
                $"Unknown command '{args[0]}'. Expected 'pipe', 'action', 'operation', 'baselib', or 'ritsulib'.");
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
        Console.WriteLine("  pipe       Build and execute a localization operation pipeline.");
        Console.WriteLine("  action     Run or inspect reusable YAML or JSON actions.");
        Console.WriteLine("  operation  Inspect registered operations.");
        Console.WriteLine("  baselib    Run the built-in BaseLib action.");
        Console.WriteLine("  ritsulib   Run the built-in RitsuLib action.");
        Console.WriteLine();
        Console.WriteLine("Options:");
        Console.WriteLine("  --version  Show version information.");
    }

    internal static string GetVersion() =>
        typeof(Program).Assembly
            .GetCustomAttribute<AssemblyInformationalVersionAttribute>()!
            .InformationalVersion.Split('+', 2)[0];

    private static void PrintPipeHelp(OperationRegistry registry)
    {
        Console.WriteLine("Usage: spireloc pipe <steps>");
        Console.WriteLine();
        Console.WriteLine("Registered steps:");
        foreach (var descriptor in registry.Descriptors)
            Console.WriteLine($"  {descriptor.GetUsage()}");
        Console.WriteLine("  --action <action> [action arguments]");
    }

    private static int RunAction(IReadOnlyList<string> args, OperationRegistry registry)
    {
        var command = new ActionCommand(registry, Console.Out, Console.Error);
        if (args.Count == 1 || (args.Count == 2 && args[1] is "--help" or "-h"))
        {
            PrintActionHelp();
            return 0;
        }

        if (string.Equals(args[1], "list", StringComparison.Ordinal))
        {
            if (args.Count != 2)
                throw new CliException("The 'action list' command does not accept arguments.");
            command.List();
            return 0;
        }

        if (string.Equals(args[1], "help", StringComparison.Ordinal))
        {
            if (args.Count != 3)
                throw new CliException("Usage: spireloc action help <action>");
            command.PrintHelp(args[2]);
            return 0;
        }

        if (!string.Equals(args[1], "run", StringComparison.Ordinal))
            throw new CliException($"Unknown action command '{args[1]}'. Expected 'run', 'list', or 'help'.");
        if (args is [_, _, "--help" or "-h"])
        {
            Console.WriteLine("Usage: spireloc action run <action> [action arguments]");
            return 0;
        }

        if (args.Count < 3)
            throw new CliException("The 'action run' command requires an action name or file path.");
        if (args.Count == 4 && args[3] is "--help" or "-h")
        {
            command.PrintHelp(args[2]);
            return 0;
        }

        return command.Run(args[2], args.Skip(3).ToArray());
    }

    private static int RunOperation(IReadOnlyList<string> args, OperationRegistry registry)
    {
        var command = new OperationCommand(registry, Console.Out);
        if (args.Count == 1 || (args.Count == 2 && args[1] is "--help" or "-h"))
        {
            PrintOperationHelp();
            return 0;
        }

        if (string.Equals(args[1], "list", StringComparison.Ordinal))
        {
            if (args.Count != 2)
                throw new CliException("The 'operation list' command does not accept arguments.");
            command.List();
            return 0;
        }

        if (string.Equals(args[1], "help", StringComparison.Ordinal))
        {
            if (args.Count < 3)
                throw new CliException("The 'operation help' command requires an operation path.");
            command.PrintHelp(args.Skip(2).ToArray());
            return 0;
        }

        throw new CliException($"Unknown operation command '{args[1]}'. Expected 'list' or 'help'.");
    }

    private static void PrintActionHelp()
    {
        Console.WriteLine("Usage: spireloc action <command>");
        Console.WriteLine();
        Console.WriteLine("Commands:");
        Console.WriteLine("  run <action> [action arguments]  Run an action.");
        Console.WriteLine("  list                             List built-in actions.");
        Console.WriteLine("  help <action>                    Show help for an action.");
    }

    private static void PrintOperationHelp()
    {
        Console.WriteLine("Usage: spireloc operation <command>");
        Console.WriteLine();
        Console.WriteLine("Commands:");
        Console.WriteLine("  list             List registered operation heads.");
        Console.WriteLine("  help <path...>   Show help for an operation or its subcommands.");
    }
}
