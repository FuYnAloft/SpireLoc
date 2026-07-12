using System.Text;
using SpireLoc.Cli.Pipeline;

namespace SpireLoc.Cli.Actions;

internal static class ActionTemplateExpander
{
    public static InvocationScalar Expand(
        InvocationScalar scalar,
        IReadOnlyDictionary<string, InvocationScalar> scope,
        InvocationSource source)
    {
        if (scalar.Kind != InvocationScalarKind.String)
            return scalar;

        var value = (string)scalar.Value;
        if (TryReadExactVariable(value, out var exactName))
            return Resolve(exactName, scope, source);

        var builder = new StringBuilder(value.Length);
        for (var index = 0; index < value.Length;)
        {
            if (index + 2 < value.Length && value[index] == '$' && value[index + 1] == '$' && value[index + 2] == '(')
            {
                var end = value.IndexOf(')', index + 3);
                if (end < 0)
                    throw source.Error("Unterminated escaped template expression.");
                builder.Append("$(");
                builder.Append(value, index + 3, end - index - 3);
                builder.Append(')');
                index = end + 1;
                continue;
            }

            if (index + 1 < value.Length && value[index] == '$' && value[index + 1] == '(')
            {
                var end = value.IndexOf(')', index + 2);
                if (end < 0)
                    throw source.Error("Unterminated template expression.");
                var name = value[(index + 2)..end];
                builder.Append(Resolve(name, scope, source).FormatInvariant());
                index = end + 1;
                continue;
            }

            builder.Append(value[index++]);
        }

        return InvocationScalar.String(builder.ToString());
    }

    private static bool TryReadExactVariable(string value, out string name)
    {
        if (value.StartsWith("$(", StringComparison.Ordinal) && value.EndsWith(')') &&
            value.IndexOf(')', 2) == value.Length - 1)
        {
            name = value[2..^1];
            return true;
        }

        name = string.Empty;
        return false;
    }

    private static InvocationScalar Resolve(
        string name,
        IReadOnlyDictionary<string, InvocationScalar> scope,
        InvocationSource source)
    {
        if (name.Length == 0)
            throw source.Error("Template variable name cannot be empty.");
        if (!scope.TryGetValue(name, out var value))
            throw source.Error($"Unknown template variable '{name}'.");
        return value;
    }
}
