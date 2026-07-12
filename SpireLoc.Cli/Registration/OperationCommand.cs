namespace SpireLoc.Cli.Registration;

internal sealed class OperationCommand(OperationRegistry registry, TextWriter output)
{
    public void List() => OperationHelpFormatter.WriteList(output, registry.Descriptors);

    public void PrintHelp(IReadOnlyList<string> path)
    {
        if (path.Count == 0)
        {
            List();
            return;
        }

        var matching = registry.Descriptors
            .Where(descriptor => StartsWith(descriptor.Path, path))
            .ToArray();
        if (matching.Length == 0)
            throw new CliException($"Unknown operation path '{string.Join(' ', path)}'.");

        var exact = matching.FirstOrDefault(descriptor => descriptor.Path.Count == path.Count);
        OperationHelpFormatter.WriteHelp(output, path, exact, matching);
    }

    private static bool StartsWith(IReadOnlyList<string> path, IReadOnlyList<string> prefix)
    {
        if (path.Count < prefix.Count)
            return false;

        for (var index = 0; index < prefix.Count; index++)
        {
            if (!string.Equals(path[index], prefix[index], StringComparison.Ordinal))
                return false;
        }

        return true;
    }
}
