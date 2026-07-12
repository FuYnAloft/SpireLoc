using System.Reflection;
using SpireLoc.Cli.Pipeline;
using SpireLoc.Core.Execution;
using SpireLoc.Core.Steps.Support;

namespace SpireLoc.Cli.Registration;

internal sealed class OperationFactoryDescriptor(
    IReadOnlyList<string> path,
    string? description,
    IReadOnlyList<OperationParameterDescriptor> parameters,
    int invocationParameterCount,
    bool producesUnaryProcessor,
    Func<object?[], object> invoke)
{
    public IReadOnlyList<string> Path { get; } = path;
    public string? Description { get; } = description;
    public IReadOnlyList<OperationParameterDescriptor> Parameters { get; } = parameters;

    public string DisplayPath => $"--{Path[0]} {string.Join(' ', Path.Skip(1))}".TrimEnd();

    public ILocOperation Create(OperationInvocationSpec invocation)
    {
        var parametersByName = Parameters.ToDictionary(static parameter => parameter.Name, StringComparer.Ordinal);
        var unknown = invocation.Arguments.Keys.FirstOrDefault(name => !parametersByName.ContainsKey(name));
        if (unknown is not null)
            throw invocation.Source.Error($"Unknown parameter '{unknown}' for step '{DisplayPath}'.");

        var values = new Dictionary<string, object?>(StringComparer.Ordinal);
        foreach (var parameter in Parameters)
        {
            if (invocation.Arguments.TryGetValue(parameter.Name, out var argument))
            {
                if (parameter.IsList != argument.IsList)
                {
                    var expectedShape = parameter.IsList ? "a list" : "a scalar";
                    throw invocation.Source.Error(
                        $"Parameter '{parameter.Name}' on '{DisplayPath}' requires {expectedShape} value.");
                }

                try
                {
                    values.Add(parameter.Name, ConvertArgument(argument, parameter));
                }
                catch (Exception exception) when (exception is FormatException or InvalidCastException or
                                                   OverflowException or ArgumentException)
                {
                    throw invocation.Source.Error(
                        $"Value {FormatArgument(argument)} is not valid for parameter '{parameter.Name}' on '{DisplayPath}'.");
                }

                continue;
            }

            if (!parameter.HasDefaultValue)
                throw invocation.Source.Error($"Missing required parameter '{parameter.Name}' for step '{DisplayPath}'.");
            values.Add(parameter.Name, parameter.DefaultValue);
        }

        var arguments = new object?[invocationParameterCount];
        foreach (var parameter in Parameters.Where(static parameter => parameter.InvocationIndex >= 0))
            arguments[parameter.InvocationIndex] = values[parameter.Name];

        object created;
        try
        {
            created = invoke(arguments);
        }
        catch (TargetInvocationException exception)
        {
            var cause = exception.InnerException ?? exception;
            throw new CliException($"{invocation.Source.Description}: Could not create step '{DisplayPath}': {cause.Message}", cause);
        }
        catch (Exception exception)
        {
            throw new CliException($"{invocation.Source.Description}: Could not create step '{DisplayPath}': {exception.Message}", exception);
        }

        if (!producesUnaryProcessor)
            return (ILocOperation)created;

        return new UnaryLocBundleProcessorStep(
            (UnaryLocBundleProcessor)created,
            (string)values["from"]!,
            (string)values["to"]!);
    }

    private static object ConvertArgument(
        InvocationArgument argument,
        OperationParameterDescriptor parameter)
    {
        if (!parameter.IsList)
        {
            if (argument.Values.Count != 1)
            {
                throw new FormatException(
                    $"Scalar parameter '{parameter.Name}' requires exactly one value.");
            }

            return InvocationScalarConverter.ConvertTo(argument.Values[0], parameter.ValueType);
        }

        var elementType = parameter.ListElementType!;
        var values = Array.CreateInstance(elementType, argument.Values.Count);
        for (var index = 0; index < argument.Values.Count; index++)
        {
            values.SetValue(
                InvocationScalarConverter.ConvertTo(argument.Values[index], elementType),
                index);
        }

        return values;
    }

    private static string FormatArgument(InvocationArgument argument) =>
        argument.Values.Count == 1
            ? $"'{argument.Values[0].FormatInvariant()}'"
            : $"[{string.Join(", ", argument.Values.Select(static value => $"'{value.FormatInvariant()}'"))}]";

    public string GetUsage()
    {
        var parts = new List<string> { DisplayPath };
        foreach (var parameter in Parameters.Where(static parameter => parameter.Position >= 0)
                     .OrderBy(static parameter => parameter.Position))
        {
            var positional = $"<{parameter.Name}>{(parameter.IsList ? "..." : string.Empty)}";
            parts.Add(parameter.HasDefaultValue ? $"[{positional}]" : positional);
        }

        foreach (var parameter in Parameters.Where(static parameter => parameter.Position < 0))
        {
            var option = parameter.IsFlag
                ? $"--{parameter.Name}"
                : parameter.IsList
                    ? $"--{parameter.Name} <value> [--{parameter.Name} <value> ...]"
                    : $"--{parameter.Name} <value>";
            parts.Add(parameter.HasDefaultValue ? $"[{option}]" : option);
        }

        return string.Join(' ', parts);
    }
}
