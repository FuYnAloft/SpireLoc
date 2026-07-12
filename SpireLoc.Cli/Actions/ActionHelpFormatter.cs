namespace SpireLoc.Cli.Actions;

internal static class ActionHelpFormatter
{
    public static void WriteList(TextWriter output, IReadOnlyList<BuiltinActionInfo> actions)
    {
        output.WriteLine("Built-in actions:");
        if (actions.Count == 0)
        {
            output.WriteLine("  (none)");
            return;
        }

        var nameWidth = actions.Max(static action => action.Name.Length);
        foreach (var action in actions.OrderBy(static action => action.Name, StringComparer.Ordinal))
        {
            output.Write($"  {action.Name}");
            if (!string.IsNullOrWhiteSpace(action.Description))
                output.Write($"{new string(' ', nameWidth - action.Name.Length + 2)}{action.Description}");
            output.WriteLine();
        }
    }

    public static void WriteAction(TextWriter output, string reference, ActionDocument action)
    {
        output.Write($"Usage: spireloc action run {reference}");
        foreach (var parameter in action.Parameters.OrderBy(static parameter => parameter.Position < 0 ? int.MaxValue : parameter.Position))
        {
            if (parameter.Position >= 0)
                output.Write(parameter.HasDefaultValue ? $" [{parameter.Name}]" : $" <{parameter.Name}>");
        }
        if (action.Parameters.Any(static parameter => parameter.Position < 0))
            output.Write(" [options]");
        output.WriteLine();

        if (!string.IsNullOrWhiteSpace(action.Description))
        {
            output.WriteLine();
            output.WriteLine(action.Description);
        }

        if (action.Parameters.Count == 0)
            return;

        output.WriteLine();
        output.WriteLine("Parameters:");
        var labels = action.Parameters.Select(GetParameterLabel).ToArray();
        var labelWidth = labels.Max(static label => label.Length);
        for (var index = 0; index < action.Parameters.Count; index++)
        {
            var parameter = action.Parameters[index];
            output.Write($"  {labels[index].PadRight(labelWidth)}");
            var detail = GetParameterDetail(parameter);
            if (detail.Length > 0)
                output.Write($"  {detail}");
            output.WriteLine();
        }
    }

    private static string GetParameterLabel(ActionParameterDefinition parameter)
    {
        var named = parameter.IsFlag
            ? $"--{parameter.Name}"
            : $"--{parameter.Name} <{GetTypeName(parameter.Type)}>";
        return parameter.Position >= 0 ? $"<{parameter.Name}>, {named}" : named;
    }

    private static string GetParameterDetail(ActionParameterDefinition parameter)
    {
        var suffix = parameter.HasDefaultValue
            ? $"default: {FormatDefault(parameter.DefaultValue!.Value)}"
            : "required";
        return string.IsNullOrWhiteSpace(parameter.Description)
            ? $"({suffix})"
            : $"{parameter.Description} ({suffix})";
    }

    private static string GetTypeName(ActionParameterType type) => type switch
    {
        ActionParameterType.String => "string",
        ActionParameterType.Boolean => "bool",
        ActionParameterType.Integer => "int",
        _ => throw new ArgumentOutOfRangeException(nameof(type), type, null),
    };

    private static string FormatDefault(Pipeline.InvocationScalar value) => value.Kind switch
    {
        Pipeline.InvocationScalarKind.Boolean => value.FormatInvariant().ToLowerInvariant(),
        _ => value.FormatInvariant(),
    };
}
