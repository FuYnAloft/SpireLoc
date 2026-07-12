using System.Globalization;
using SpireLoc.Cli.Registration;
using SpireLoc.Core.Execution;

namespace SpireLoc.Cli.Pipe;

internal sealed class PipeParser(OperationRegistry registry)
{
    public IReadOnlyList<ILocOperation> Parse(IReadOnlyList<string> tokens)
    {
        if (tokens.Count == 0)
            throw new CliException("The pipe command requires at least one operation step.");

        var operations = new List<ILocOperation>();
        var index = 0;
        while (index < tokens.Count)
        {
            var descriptor = registry.Resolve(tokens, index, out var pathTokenCount);
            index += pathTokenCount;

            var values = BindStep(descriptor, tokens, ref index);
            operations.Add(descriptor.Create(values));
        }

        return operations;
    }

    private IReadOnlyDictionary<string, object?> BindStep(
        OperationFactoryDescriptor descriptor,
        IReadOnlyList<string> tokens,
        ref int index)
    {
        var parametersByName = descriptor.Parameters.ToDictionary(
            static parameter => parameter.Name,
            StringComparer.Ordinal);
        var positional = descriptor.Parameters.Where(static parameter => parameter.Position >= 0)
            .OrderBy(static parameter => parameter.Position)
            .ToArray();
        var values = new Dictionary<string, object?>(StringComparer.Ordinal);

        while (index < tokens.Count && !registry.IsStepHead(tokens[index]))
        {
            var token = tokens[index];
            if (token.StartsWith("--", StringComparison.Ordinal))
            {
                var optionName = token[2..];
                if (!parametersByName.TryGetValue(optionName, out var parameter))
                    throw new CliException($"Unknown option '--{optionName}' for step '{descriptor.DisplayPath}'.");
                if (values.ContainsKey(parameter.Name))
                    throw new CliException($"Parameter '{parameter.Name}' was supplied more than once for '{descriptor.DisplayPath}'.");

                index++;
                if (parameter.IsFlag)
                {
                    values.Add(parameter.Name, true);
                    continue;
                }

                if (index >= tokens.Count || registry.IsStepHead(tokens[index]) ||
                    tokens[index].StartsWith("--", StringComparison.Ordinal))
                    throw new CliException($"Option '--{parameter.Name}' for '{descriptor.DisplayPath}' requires a value.");

                values.Add(parameter.Name, ConvertValue(tokens[index], parameter, descriptor));
                index++;
                continue;
            }

            var positionalParameter = positional.FirstOrDefault(parameter => !values.ContainsKey(parameter.Name));
            if (positionalParameter is null)
                throw new CliException($"Unexpected positional value '{token}' for step '{descriptor.DisplayPath}'.");

            values.Add(
                positionalParameter.Name,
                ConvertValue(token, positionalParameter, descriptor));
            index++;
        }

        foreach (var parameter in descriptor.Parameters)
        {
            if (values.ContainsKey(parameter.Name))
                continue;
            if (!parameter.HasDefaultValue)
            {
                throw new CliException(
                    $"Missing required parameter '{parameter.Name}' for step '{descriptor.DisplayPath}'.");
            }

            values.Add(parameter.Name, parameter.DefaultValue);
        }

        return values;
    }

    private static object ConvertValue(
        string value,
        OperationParameterDescriptor parameter,
        OperationFactoryDescriptor descriptor)
    {
        var targetType = Nullable.GetUnderlyingType(parameter.ValueType) ?? parameter.ValueType;
        try
        {
            if (targetType == typeof(string))
                return value;
            if (targetType == typeof(bool))
                return bool.Parse(value);
            if (targetType.IsEnum)
                return Enum.Parse(targetType, value, ignoreCase: true);
            if (targetType == typeof(FileInfo))
                return new FileInfo(value);
            if (targetType == typeof(DirectoryInfo))
                return new DirectoryInfo(value);
            if (targetType == typeof(Guid))
                return Guid.Parse(value);
            if (targetType == typeof(TimeSpan))
                return TimeSpan.Parse(value, CultureInfo.InvariantCulture);

            return Convert.ChangeType(value, targetType, CultureInfo.InvariantCulture);
        }
        catch (Exception exception) when (exception is FormatException or InvalidCastException or OverflowException or ArgumentException)
        {
            throw new CliException(
                $"Value '{value}' is not valid for parameter '{parameter.Name}' on '{descriptor.DisplayPath}'.");
        }
    }
}
