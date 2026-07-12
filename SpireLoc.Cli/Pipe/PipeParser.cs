using SpireLoc.Cli.Pipeline;
using SpireLoc.Cli.Registration;

namespace SpireLoc.Cli.Pipe;

internal sealed class PipeParser(OperationRegistry registry)
{
    public IReadOnlyList<PipelineItem> Parse(IReadOnlyList<string> tokens)
    {
        if (tokens.Count == 0)
            throw new CliException("The pipe command requires at least one pipeline item.");

        var items = new List<PipelineItem>();
        var index = 0;
        while (index < tokens.Count)
        {
            if (string.Equals(tokens[index], $"--{OperationRegistry.ActionHead}", StringComparison.Ordinal))
                items.Add(ParseAction(tokens, ref index));
            else
                items.Add(ParseOperation(tokens, ref index));
        }

        return items;
    }

    private OperationInvocationSpec ParseOperation(IReadOnlyList<string> tokens, ref int index)
    {
        var descriptor = registry.Resolve(tokens, index, out var pathTokenCount);
        index += pathTokenCount;

        var arguments = BindOperationTokens(descriptor, tokens, ref index);
        return new OperationInvocationSpec(
            descriptor.Path,
            arguments,
            new InvocationSource($"pipe -> {descriptor.DisplayPath}"));
    }

    private ActionInvocationSpec ParseAction(IReadOnlyList<string> tokens, ref int index)
    {
        index++;
        if (index >= tokens.Count || registry.IsStepHead(tokens[index]) ||
            tokens[index].StartsWith("--", StringComparison.Ordinal))
            throw new CliException("The '--action' item requires an action name or file path.");

        var actionPath = tokens[index++];
        var arguments = new List<string>();
        while (index < tokens.Count && !registry.IsStepHead(tokens[index]))
            arguments.Add(tokens[index++]);

        return new ActionInvocationSpec(
            actionPath,
            arguments,
            new InvocationSource($"pipe -> --action {actionPath}"));
    }

    private IReadOnlyDictionary<string, InvocationScalar> BindOperationTokens(
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
        var values = new Dictionary<string, InvocationScalar>(StringComparer.Ordinal);

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
                    values.Add(parameter.Name, InvocationScalar.Boolean(true));
                    continue;
                }

                if (index >= tokens.Count || registry.IsStepHead(tokens[index]) ||
                    tokens[index].StartsWith("--", StringComparison.Ordinal))
                    throw new CliException($"Option '--{parameter.Name}' for '{descriptor.DisplayPath}' requires a value.");

                values.Add(parameter.Name, InvocationScalar.String(tokens[index++]));
                continue;
            }

            var positionalParameter = positional.FirstOrDefault(parameter => !values.ContainsKey(parameter.Name));
            if (positionalParameter is null)
                throw new CliException($"Unexpected positional value '{token}' for step '{descriptor.DisplayPath}'.");

            values.Add(positionalParameter.Name, InvocationScalar.String(token));
            index++;
        }

        return values;
    }
}
