using SpireLoc.Cli.Pipeline;

namespace SpireLoc.Cli.Actions;

internal static class ActionParameterBinder
{
    public static IReadOnlyDictionary<string, InvocationScalar> BindCli(
        IReadOnlyList<ActionParameterDefinition> definitions,
        IReadOnlyList<string> tokens,
        InvocationSource source)
    {
        var definitionsByName = definitions.ToDictionary(static definition => definition.Name, StringComparer.Ordinal);
        var positional = definitions.Where(static definition => definition.Position >= 0)
            .OrderBy(static definition => definition.Position)
            .ToArray();
        var supplied = new Dictionary<string, InvocationScalar>(StringComparer.Ordinal);

        var index = 0;
        while (index < tokens.Count)
        {
            var token = tokens[index];
            if (token.StartsWith("--", StringComparison.Ordinal))
            {
                var name = token[2..];
                if (!definitionsByName.TryGetValue(name, out var definition))
                    throw source.Error($"Unknown action parameter '--{name}'.");
                if (supplied.ContainsKey(name))
                    throw source.Error($"Action parameter '{name}' was supplied more than once.");

                index++;
                if (definition.IsFlag)
                {
                    supplied.Add(name, InvocationScalar.Boolean(true));
                    continue;
                }

                if (index >= tokens.Count || tokens[index].StartsWith("--", StringComparison.Ordinal))
                    throw source.Error($"Action parameter '--{name}' requires a value.");
                supplied.Add(name, InvocationScalar.String(tokens[index++]));
                continue;
            }

            var definitionForPosition = positional.FirstOrDefault(definition => !supplied.ContainsKey(definition.Name));
            if (definitionForPosition is null)
                throw source.Error($"Unexpected action positional value '{token}'.");
            supplied.Add(definitionForPosition.Name, InvocationScalar.String(token));
            index++;
        }

        return Bind(definitions, supplied, source);
    }

    public static IReadOnlyDictionary<string, InvocationScalar> BindNamed(
        IReadOnlyList<ActionParameterDefinition> definitions,
        IReadOnlyDictionary<string, InvocationScalar> supplied,
        InvocationSource source) =>
        Bind(definitions, supplied, source);

    public static InvocationScalar ConvertValue(
        InvocationScalar value,
        ActionParameterType type,
        InvocationSource source,
        string parameterName)
    {
        try
        {
            return type switch
            {
                ActionParameterType.String => InvocationScalar.String(value.FormatInvariant()),
                ActionParameterType.Boolean => InvocationScalar.Boolean(InvocationScalarConverter.ToBoolean(value)),
                ActionParameterType.Integer => InvocationScalar.Integer(InvocationScalarConverter.ToInteger(value)),
                _ => throw new ArgumentOutOfRangeException(nameof(type), type, null),
            };
        }
        catch (FormatException)
        {
            throw source.Error(
                $"Value '{value.FormatInvariant()}' is not valid for action parameter '{parameterName}' of type '{FormatType(type)}'.");
        }
    }

    private static IReadOnlyDictionary<string, InvocationScalar> Bind(
        IReadOnlyList<ActionParameterDefinition> definitions,
        IReadOnlyDictionary<string, InvocationScalar> supplied,
        InvocationSource source)
    {
        var definitionsByName = definitions.ToDictionary(static definition => definition.Name, StringComparer.Ordinal);
        var unknown = supplied.Keys.FirstOrDefault(name => !definitionsByName.ContainsKey(name));
        if (unknown is not null)
            throw source.Error($"Unknown action parameter '{unknown}'.");

        var values = new Dictionary<string, InvocationScalar>(StringComparer.Ordinal);
        foreach (var definition in definitions)
        {
            if (supplied.TryGetValue(definition.Name, out var value))
            {
                values.Add(definition.Name, ConvertValue(value, definition.Type, source, definition.Name));
                continue;
            }

            if (!definition.HasDefaultValue)
                throw source.Error($"Missing required action parameter '{definition.Name}'.");
            values.Add(definition.Name, definition.DefaultValue!.Value);
        }

        return values;
    }

    private static string FormatType(ActionParameterType type) => type switch
    {
        ActionParameterType.String => "string",
        ActionParameterType.Boolean => "bool",
        ActionParameterType.Integer => "int",
        _ => throw new ArgumentOutOfRangeException(nameof(type), type, null),
    };
}
