namespace SpireLoc.Cli.Registration;

internal static class OperationHelpFormatter
{
    public static void WriteList(TextWriter output, OperationRegistry registry) =>
        WriteList(output, registry.Descriptors);

    public static void WriteHelp(
        TextWriter output,
        OperationRegistry registry,
        IReadOnlyList<string> path) =>
        new OperationCommand(registry, output).PrintHelp(path);

    public static void WriteList(TextWriter output, IReadOnlyList<OperationFactoryDescriptor> descriptors)
    {
        output.WriteLine("Operations:");
        WriteDescriptorList(output, descriptors);
    }

    public static void WriteHelp(
        TextWriter output,
        IReadOnlyList<string> path,
        OperationFactoryDescriptor? descriptor,
        IReadOnlyList<OperationFactoryDescriptor> descendants)
    {
        if (descriptor is not null)
        {
            output.WriteLine($"Usage: spireloc pipe {descriptor.GetUsage()}");
            if (!string.IsNullOrWhiteSpace(descriptor.Description))
            {
                output.WriteLine();
                output.WriteLine(descriptor.Description);
            }

            WriteParameters(output, descriptor.Parameters);
        }

        var children = descendants
            .Where(candidate => candidate.Path.Count > path.Count)
            .GroupBy(candidate => candidate.Path[path.Count], StringComparer.Ordinal)
            .Select(group => new ChildOperation(
                group.Key,
                group.FirstOrDefault(candidate => candidate.Path.Count == path.Count + 1)?.Description))
            .OrderBy(static child => child.Name, StringComparer.Ordinal)
            .ToArray();
        if (children.Length == 0)
            return;

        if (descriptor is not null)
            output.WriteLine();
        output.WriteLine($"Operation: {string.Join(' ', path)}");
        output.WriteLine();
        output.WriteLine("Subcommands:");
        WriteRows(output, children.Select(static child => (child.Name, child.Description)));
    }

    private static void WriteDescriptorList(
        TextWriter output,
        IReadOnlyList<OperationFactoryDescriptor> descriptors) =>
        WriteRows(output, descriptors.Select(static descriptor =>
            (string.Join(' ', descriptor.Path), descriptor.Description)));

    private static void WriteParameters(
        TextWriter output,
        IReadOnlyList<OperationParameterDescriptor> parameters)
    {
        if (parameters.Count == 0)
            return;

        output.WriteLine();
        output.WriteLine("Parameters:");
        var rows = parameters.Select(parameter =>
            (Label: GetParameterLabel(parameter), Description: (string?)GetParameterDetail(parameter)));
        WriteRows(output, rows);
    }

    private static string GetParameterLabel(OperationParameterDescriptor parameter)
    {
        var type = parameter.IsList
            ? $"{GetTypeName(parameter.ListElementType!)}..."
            : GetTypeName(parameter.ValueType);
        var named = parameter.IsFlag ? $"--{parameter.Name}" : $"--{parameter.Name} <{type}>";
        return parameter.Position >= 0 ? $"<{parameter.Name}>, {named}" : named;
    }

    private static string GetParameterDetail(OperationParameterDescriptor parameter)
    {
        var requirement = parameter.HasDefaultValue
            ? $"default: {FormatDefault(parameter.DefaultValue)}"
            : "required";
        return string.IsNullOrWhiteSpace(parameter.Description)
            ? $"({requirement})"
            : $"{parameter.Description} ({requirement})";
    }

    private static string GetTypeName(Type type)
    {
        if (type == typeof(string))
            return "string";
        if (type == typeof(int))
            return "int";
        if (type == typeof(bool))
            return "bool";
        if (type.IsEnum)
            return string.Join('|', Enum.GetNames(type).Select(static name => name.ToLowerInvariant()));
        return type.Name;
    }

    private static string FormatDefault(object? value) => value switch
    {
        null => "null",
        bool boolean => boolean ? "true" : "false",
        _ => value.ToString()!,
    };

    private static void WriteRows(TextWriter output, IEnumerable<(string Label, string? Description)> rows)
    {
        var materialized = rows.ToArray();
        if (materialized.Length == 0)
        {
            output.WriteLine("  (none)");
            return;
        }

        var width = materialized.Max(static row => row.Label.Length);
        foreach (var (label, description) in materialized)
        {
            output.Write($"  {label}");
            if (!string.IsNullOrWhiteSpace(description))
                output.Write($"{new string(' ', width - label.Length + 2)}{description}");
            output.WriteLine();
        }
    }

    private sealed record ChildOperation(string Name, string? Description);
}
