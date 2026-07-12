using SpireLoc.Cli.Pipeline;
using SpireLoc.Cli.Registration;

namespace SpireLoc.Cli.Actions;

internal sealed class ActionExpander(ActionYamlLoader loader, OperationRegistry registry)
{
    public const int MaximumDepth = 64;

    private static readonly StringComparer PathComparer =
        OperatingSystem.IsWindows() ? StringComparer.OrdinalIgnoreCase : StringComparer.Ordinal;

    public IReadOnlyList<OperationInvocationSpec> ExpandPipeline(
        IReadOnlyList<PipelineItem> items,
        string workingDirectory)
    {
        var operations = new List<OperationInvocationSpec>();
        foreach (var item in items)
        {
            switch (item)
            {
                case OperationInvocationSpec operation:
                    operations.Add(operation);
                    break;
                case ActionInvocationSpec action:
                    operations.AddRange(ExpandCliAction(
                        ResolvePath(action.ActionPath, workingDirectory),
                        action.ArgumentTokens,
                        action.Source));
                    break;
                default:
                    throw new ArgumentOutOfRangeException(nameof(item), item, null);
            }
        }

        return operations;
    }

    public IReadOnlyList<OperationInvocationSpec> ExpandAction(
        string actionPath,
        IReadOnlyList<string> argumentTokens,
        string workingDirectory) =>
        ExpandCliAction(
            ResolvePath(actionPath, workingDirectory),
            argumentTokens,
            new InvocationSource($"action run -> {actionPath}"));

    private IReadOnlyList<OperationInvocationSpec> ExpandCliAction(
        string fullPath,
        IReadOnlyList<string> argumentTokens,
        InvocationSource invocationSource)
    {
        var document = LoadRoot(fullPath, invocationSource);
        ValidateParameterHeads(document);
        var chain = new List<string> { document.FilePath };
        var source = Source(chain, null, "parameters");
        var parameters = ActionParameterBinder.BindCli(document.Parameters, argumentTokens, source);
        return ExpandDocument(document, parameters, chain);
    }

    private IReadOnlyList<OperationInvocationSpec> ExpandDocument(
        ActionDocument document,
        IReadOnlyDictionary<string, InvocationScalar> parameters,
        IReadOnlyList<string> chain)
    {
        var scope = new Dictionary<string, InvocationScalar>(parameters, StringComparer.Ordinal)
        {
            ["ActionPath"] = InvocationScalar.String(document.FilePath),
            ["ActionDir"] = InvocationScalar.String(Path.GetDirectoryName(document.FilePath)!),
        };
        var operations = new List<OperationInvocationSpec>();

        foreach (var step in document.Steps)
        {
            if (step.Condition is not null && !EvaluateCondition(step.Condition, scope, chain))
                continue;

            switch (step)
            {
                case ActionOperationStep operation:
                    operations.Add(ExpandOperation(operation, scope, chain));
                    break;
                case ActionUsesStep uses:
                    operations.AddRange(ExpandUses(document, uses, scope, chain));
                    break;
                default:
                    throw new ArgumentOutOfRangeException(nameof(step), step, null);
            }
        }

        return operations;
    }

    private OperationInvocationSpec ExpandOperation(
        ActionOperationStep step,
        IReadOnlyDictionary<string, InvocationScalar> scope,
        IReadOnlyList<string> chain)
    {
        var source = Source(chain, step.Source, $"--{step.Head}");
        var path = new List<string> { step.Head };
        foreach (var kind in step.Kind)
        {
            var expanded = ActionTemplateExpander.Expand(kind, scope, source);
            if (expanded.Kind != InvocationScalarKind.String || expanded.FormatInvariant().Length == 0)
                throw source.Error("Expanded operation kind must be a non-empty string segment.");
            path.Add((string)expanded.Value);
        }

        var arguments = step.Arguments.ToDictionary(
            static argument => argument.Key,
            argument => ActionTemplateExpander.Expand(argument.Value, scope, source),
            StringComparer.Ordinal);
        var display = $"--{path[0]} {string.Join(' ', path.Skip(1))}".TrimEnd();
        return new OperationInvocationSpec(
            path,
            arguments,
            Source(chain, step.Source, display));
    }

    private IReadOnlyList<OperationInvocationSpec> ExpandUses(
        ActionDocument caller,
        ActionUsesStep step,
        IReadOnlyDictionary<string, InvocationScalar> callerScope,
        IReadOnlyList<string> chain)
    {
        var source = Source(chain, step.Source, "uses");
        var expandedPath = ActionTemplateExpander.Expand(step.ActionPath, callerScope, source);
        if (expandedPath.Kind != InvocationScalarKind.String || expandedPath.FormatInvariant().Length == 0)
            throw source.Error("Expanded uses path must be a non-empty string.");

        var childPath = ResolvePath((string)expandedPath.Value, Path.GetDirectoryName(caller.FilePath)!);
        if (chain.Count >= MaximumDepth)
            throw source.Error($"Action call depth exceeds the maximum of {MaximumDepth}.");
        if (chain.Contains(childPath, PathComparer))
        {
            var cycle = chain.Append(childPath).Select(Path.GetFileName);
            throw source.Error($"Action cycle detected: {string.Join(" -> ", cycle)}.");
        }

        var childChain = chain.Append(childPath).ToArray();
        ActionDocument child;
        try
        {
            child = loader.Load(childPath);
        }
        catch (CliException exception)
        {
            throw new CliException($"{FormatChain(childChain)}: {exception.Message}", exception);
        }

        ValidateParameterHeads(child);
        var expandedWith = step.With.ToDictionary(
            static argument => argument.Key,
            argument => ActionTemplateExpander.Expand(argument.Value, callerScope, source),
            StringComparer.Ordinal);
        var childParameters = ActionParameterBinder.BindNamed(
            child.Parameters,
            expandedWith,
            Source(childChain, step.Source, "with"));
        return ExpandDocument(child, childParameters, childChain);
    }

    private bool EvaluateCondition(
        ActionCondition condition,
        IReadOnlyDictionary<string, InvocationScalar> scope,
        IReadOnlyList<string> chain)
    {
        var source = Source(chain, condition.Source, "if");
        return condition switch
        {
            ScalarActionCondition scalar => EvaluateScalarCondition(scalar.Value, scope, source),
            EqualsActionCondition equals => ActionTemplateExpander.Expand(equals.Left, scope, source)
                .SemanticallyEquals(ActionTemplateExpander.Expand(equals.Right, scope, source)),
            NotActionCondition not => !EvaluateCondition(not.Value, scope, chain),
            AllActionCondition all => all.Values.All(value => EvaluateCondition(value, scope, chain)),
            AnyActionCondition any => any.Values.Any(value => EvaluateCondition(value, scope, chain)),
            _ => throw new ArgumentOutOfRangeException(nameof(condition), condition, null),
        };
    }

    private static bool EvaluateScalarCondition(
        InvocationScalar value,
        IReadOnlyDictionary<string, InvocationScalar> scope,
        InvocationSource source)
    {
        var expanded = ActionTemplateExpander.Expand(value, scope, source);
        try
        {
            return InvocationScalarConverter.ToBoolean(expanded);
        }
        catch (FormatException)
        {
            throw source.Error($"Condition value '{expanded.FormatInvariant()}' is not boolean.");
        }
    }

    private void ValidateParameterHeads(ActionDocument document)
    {
        var conflict = document.Parameters.FirstOrDefault(parameter => registry.IsStepHeadName(parameter.Name));
        if (conflict is not null)
        {
            throw new CliException(
                $"{conflict.Source.Display}: Action parameter '--{conflict.Name}' conflicts with a pipeline item head.");
        }
    }

    private ActionDocument LoadRoot(string fullPath, InvocationSource source)
    {
        try
        {
            return loader.Load(fullPath);
        }
        catch (CliException exception)
        {
            throw new CliException($"{source.Description}: {exception.Message}", exception);
        }
    }

    private static InvocationSource Source(
        IReadOnlyList<string> chain,
        ActionSourceLocation? location,
        string suffix)
    {
        var description = FormatChain(chain);
        if (suffix.Length > 0)
            description += $" -> {suffix}";
        if (location is not null)
            description += $" ({location.FilePath}:{location.Line}:{location.Column})";
        return new InvocationSource(description);
    }

    private static string FormatChain(IEnumerable<string> chain) =>
        string.Join(" -> ", chain.Select(Path.GetFileName));

    private static string ResolvePath(string path, string baseDirectory)
    {
        try
        {
            return Path.IsPathRooted(path)
                ? Path.GetFullPath(path)
                : Path.GetFullPath(path, Path.GetFullPath(baseDirectory));
        }
        catch (Exception exception) when (exception is ArgumentException or NotSupportedException or PathTooLongException)
        {
            throw new CliException($"Action path '{path}' is invalid: {exception.Message}", exception);
        }
    }
}
