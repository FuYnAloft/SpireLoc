using System.Reflection;
using SpireLoc.Core.Execution;
using SpireLoc.Core.Steps.Support;

namespace SpireLoc.Cli.Registration;

internal sealed class OperationFactoryDescriptor(
    IReadOnlyList<string> path,
    IReadOnlyList<OperationParameterDescriptor> parameters,
    int invocationParameterCount,
    bool producesUnaryProcessor,
    Func<object?[], object> invoke)
{
    public IReadOnlyList<string> Path { get; } = path;
    public IReadOnlyList<OperationParameterDescriptor> Parameters { get; } = parameters;

    public string DisplayPath => $"--{Path[0]} {string.Join(' ', Path.Skip(1))}".TrimEnd();

    public ILocOperation Create(IReadOnlyDictionary<string, object?> values)
    {
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
            throw new CliException($"Could not create step '{DisplayPath}': {cause.Message}", cause);
        }
        catch (Exception exception)
        {
            throw new CliException($"Could not create step '{DisplayPath}': {exception.Message}", exception);
        }

        if (!producesUnaryProcessor)
            return (ILocOperation)created;

        return new UnaryLocBundleProcessorStep(
            (UnaryLocBundleProcessor)created,
            (string)values["from"]!,
            (string)values["to"]!);
    }

    public string GetUsage()
    {
        var parts = new List<string> { DisplayPath };
        foreach (var parameter in Parameters.Where(static parameter => parameter.Position >= 0)
                     .OrderBy(static parameter => parameter.Position))
        {
            parts.Add(parameter.HasDefaultValue
                ? $"[{parameter.Name}]"
                : $"<{parameter.Name}>");
        }

        foreach (var parameter in Parameters.Where(static parameter => parameter.Position < 0))
        {
            var option = parameter.IsFlag
                ? $"--{parameter.Name}"
                : $"--{parameter.Name} <value>";
            parts.Add(parameter.HasDefaultValue ? $"[{option}]" : option);
        }

        return string.Join(' ', parts);
    }
}
