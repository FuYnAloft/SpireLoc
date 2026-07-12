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

            if (!string.Equals(args[0], "pipe", StringComparison.Ordinal))
                throw new CliException($"Unknown command '{args[0]}'. Expected 'pipe'.");

            var registry = OperationRegistry.Scan(typeof(ILocOperation).Assembly);
            if (args.Length == 2 && args[1] is "--help" or "-h")
            {
                PrintPipeHelp(registry);
                return 0;
            }

            return new PipeCommand(registry, Console.Out, Console.Error).Run(args[1..]);
        }
        catch (CliException exception)
        {
            Console.Error.WriteLine($"error: {exception.Message}");
            return 2;
        }
    }

    private static void PrintRootHelp()
    {
        Console.WriteLine("Usage: spireloc pipe <steps>");
        Console.WriteLine();
        Console.WriteLine("Commands:");
        Console.WriteLine("  pipe    Build and execute a localization operation pipeline.");
    }

    private static void PrintPipeHelp(OperationRegistry registry)
    {
        Console.WriteLine("Usage: spireloc pipe <steps>");
        Console.WriteLine();
        Console.WriteLine("Registered steps:");
        foreach (var descriptor in registry.Descriptors)
            Console.WriteLine($"  {descriptor.GetUsage()}");
    }
}
