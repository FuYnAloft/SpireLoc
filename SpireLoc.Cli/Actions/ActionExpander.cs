using SpireLoc.Cli.Pipeline;
using SpireLoc.Cli.Registration;

namespace SpireLoc.Cli.Actions;

internal sealed class ActionExpander
{
    public const int MaximumDepth = 64;

    private static readonly StringComparer PathComparer =
        OperatingSystem.IsWindows() ? StringComparer.OrdinalIgnoreCase : StringComparer.Ordinal;

    private readonly ActionYamlLoader _loader;
    private readonly BuiltinActionCatalog _builtins;
    private readonly OperationRegistry _registry;

    public ActionExpander(ActionYamlLoader loader, OperationRegistry registry)
        : this(loader, new BuiltinActionCatalog(loader, typeof(ActionExpander).Assembly), registry)
    {
    }

    public ActionExpander(
        ActionYamlLoader loader,
        BuiltinActionCatalog builtins,
        OperationRegistry registry)
    {
        _loader = loader;
        _builtins = builtins;
        _registry = registry;
    }

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
                        action.ActionPath,
                        action.ArgumentTokens,
                        workingDirectory,
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
            actionPath,
            argumentTokens,
            workingDirectory,
            new InvocationSource($"action run -> {actionPath}"));

    private IReadOnlyList<OperationInvocationSpec> ExpandCliAction(
        string actionReference,
        IReadOnlyList<string> argumentTokens,
        string workingDirectory,
        InvocationSource invocationSource)
    {
        var document = LoadReference(actionReference, workingDirectory, invocationSource);
        ValidateParameterHeads(document);
        var chain = new List<string> { document.FilePath };
        var source = Source(chain, null, "parameters");
        var parameters = ActionParameterBinder.BindCli(document.Parameters, argumentTokens, source);
        var resolutionDirectory = document.IsBuiltin
            ? Path.GetFullPath(workingDirectory)
            : Path.GetDirectoryName(document.FilePath)!;
        return ExpandDocument(document, parameters, chain, resolutionDirectory);
    }

    private IReadOnlyList<OperationInvocationSpec> ExpandDocument(
        ActionDocument document,
        IReadOnlyDictionary<string, InvocationScalar> parameters,
        IReadOnlyList<string> chain,
        string resolutionDirectory)
    {
        var scope = new Dictionary<string, InvocationScalar>(parameters, StringComparer.Ordinal)
        {
            ["ActionPath"] = InvocationScalar.String(document.IsBuiltin
                ? "builtin-missing-action-path"
                : document.FilePath),
            ["ActionDir"] = InvocationScalar.String(document.IsBuiltin
                ? "builtin-missing-action-dir"
                : Path.GetDirectoryName(document.FilePath)!),
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
                    operations.AddRange(ExpandUses(uses, scope, chain, resolutionDirectory));
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
        ActionUsesStep step,
        IReadOnlyDictionary<string, InvocationScalar> callerScope,
        IReadOnlyList<string> chain,
        string resolutionDirectory)
    {
        var source = Source(chain, step.Source, "uses");
        var expandedPath = ActionTemplateExpander.Expand(step.ActionPath, callerScope, source);
        if (expandedPath.Kind != InvocationScalarKind.String || expandedPath.FormatInvariant().Length == 0)
            throw source.Error("Expanded uses path must be a non-empty string.");

        if (chain.Count >= MaximumDepth)
            throw source.Error($"Action call depth exceeds the maximum of {MaximumDepth}.");

        var child = LoadReference((string)expandedPath.Value, resolutionDirectory, source);
        if (chain.Any(identity => IdentityEquals(identity, child.FilePath)))
        {
            var cycle = chain.Append(child.FilePath).Select(DisplayIdentity);
            throw source.Error($"Action cycle detected: {string.Join(" -> ", cycle)}.");
        }

        var childChain = chain.Append(child.FilePath).ToArray();

        ValidateParameterHeads(child);
        var expandedWith = step.With.ToDictionary(
            static argument => argument.Key,
            argument => ActionTemplateExpander.Expand(argument.Value, callerScope, source),
            StringComparer.Ordinal);
        var childParameters = ActionParameterBinder.BindNamed(
            child.Parameters,
            expandedWith,
            Source(childChain, step.Source, "with"));
        var childResolutionDirectory = child.IsBuiltin
            ? resolutionDirectory
            : Path.GetDirectoryName(child.FilePath)!;
        return ExpandDocument(child, childParameters, childChain, childResolutionDirectory);
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
        var conflict = document.Parameters.FirstOrDefault(parameter => _registry.IsStepHeadName(parameter.Name));
        if (conflict is not null)
        {
            throw new CliException(
                $"{conflict.Source.Display}: Action parameter '--{conflict.Name}' conflicts with a pipeline item head.");
        }
    }

    private ActionDocument LoadReference(
        string actionReference,
        string baseDirectory,
        InvocationSource source)
    {
        if (_builtins.TryLoad(actionReference, out var builtin))
            return builtin;

        var fullPath = ResolvePath(actionReference, baseDirectory);
        try
        {
            return _loader.Load(fullPath);
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
        string.Join(" -> ", chain.Select(DisplayIdentity));

    private static string DisplayIdentity(string identity) =>
        identity.StartsWith("builtin:", StringComparison.Ordinal)
            ? identity["builtin:".Length..]
            : Path.GetFileName(identity);

    private static bool IdentityEquals(string left, string right)
    {
        var leftBuiltin = left.StartsWith("builtin:", StringComparison.Ordinal);
        var rightBuiltin = right.StartsWith("builtin:", StringComparison.Ordinal);
        return leftBuiltin || rightBuiltin
            ? string.Equals(left, right, StringComparison.Ordinal)
            : PathComparer.Equals(left, right);
    }

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
