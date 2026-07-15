using System.Globalization;
using SpireLoc.Cli.Pipeline;
using YamlDotNet.Core;
using YamlDotNet.RepresentationModel;

namespace SpireLoc.Cli.Actions;

internal sealed class ActionYamlLoader
{
    private static readonly HashSet<string> RootFields =
        ["schema-version", "version", "description", "parameters", "steps"];
    private static readonly HashSet<string> ParameterFields = ["type", "description", "position", "flag", "default"];
    private static readonly HashSet<string> ReservedParameterNames = ["ActionPath", "ActionDir"];

    private readonly Dictionary<string, ActionDocument> _fileCache = new(PathComparer);
    private readonly Dictionary<string, ActionDocument> _builtinCache = new(StringComparer.Ordinal);

    public ActionDocument Load(string path)
    {
        var fullPath = Path.GetFullPath(path);
        if (_fileCache.TryGetValue(fullPath, out var cached))
            return cached;

        ActionDocument document;
        try
        {
            using var reader = File.OpenText(fullPath);
            var stream = new YamlStream();
            stream.Load(reader);
            if (stream.Documents.Count != 1)
                throw new CliException($"{fullPath}: Action definition must contain exactly one document.");
            document = ParseDocument(fullPath, false, stream.Documents[0].RootNode);
        }
        catch (CliException)
        {
            throw;
        }
        catch (YamlException exception)
        {
            throw new CliException(
                $"{fullPath}:{exception.Start.Line + 1}:{exception.Start.Column + 1}: {exception.Message}",
                exception);
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            throw new CliException($"Could not load action '{fullPath}': {exception.Message}", exception);
        }

        _fileCache.Add(fullPath, document);
        return document;
    }

    public ActionDocument LoadBuiltin(string name, TextReader reader)
    {
        var identity = $"builtin:{name}";
        if (_builtinCache.TryGetValue(name, out var cached))
            return cached;

        try
        {
            var stream = new YamlStream();
            stream.Load(reader);
            if (stream.Documents.Count != 1)
                throw new CliException($"{identity}: Action definition must contain exactly one document.");
            var document = ParseDocument(identity, true, stream.Documents[0].RootNode);
            _builtinCache.Add(name, document);
            return document;
        }
        catch (CliException)
        {
            throw;
        }
        catch (YamlException exception)
        {
            throw new CliException(
                $"{identity}:{exception.Start.Line + 1}:{exception.Start.Column + 1}: {exception.Message}",
                exception);
        }
    }

    private static ActionDocument ParseDocument(string filePath, bool isBuiltin, YamlNode rootNode)
    {
        var root = RequireMapping(filePath, rootNode, "Action root must be a mapping.");
        var fields = ReadFields(filePath, root);
        RejectUnknownFields(filePath, root, fields, RootFields, "action");

        var hasSchemaVersion = fields.TryGetValue("schema-version", out var schemaVersionNode);
        var hasLegacyVersion = fields.TryGetValue("version", out var legacyVersionNode);
        if (hasSchemaVersion && hasLegacyVersion)
        {
            throw Error(
                filePath,
                fields["version"],
                "Action fields 'schema-version' and legacy 'version' cannot be used together.");
        }

        var versionNode = hasSchemaVersion
            ? schemaVersionNode
            : hasLegacyVersion
                ? legacyVersionNode
                : null;
        var schemaVersion = versionNode is not null
            ? ReadInteger(filePath, versionNode, "Action schema version")
            : 1;
        if (schemaVersion != 1)
            throw Error(filePath, versionNode ?? root, $"Unsupported action schema version '{schemaVersion}'.");

        var description = fields.TryGetValue("description", out var descriptionNode)
            ? ReadString(filePath, descriptionNode, "Action description")
            : null;

        var parameters = fields.TryGetValue("parameters", out var parametersNode)
            ? ParseParameters(filePath, parametersNode)
            : [];
        if (!fields.TryGetValue("steps", out var stepsNode))
            throw Error(filePath, root, "Action field 'steps' is required.");
        var steps = ParseSteps(filePath, stepsNode);

        return new ActionDocument(filePath, isBuiltin, schemaVersion, description, parameters, steps);
    }

    private static IReadOnlyList<ActionParameterDefinition> ParseParameters(string filePath, YamlNode node)
    {
        var mapping = RequireMapping(filePath, node, "Action 'parameters' must be a mapping.");
        var definitions = new List<ActionParameterDefinition>();
        foreach (var (keyNode, valueNode) in mapping.Children)
        {
            var name = ReadKey(filePath, keyNode);
            if (ReservedParameterNames.Contains(name))
                throw Error(filePath, keyNode, $"Action parameter name '{name}' is reserved.");
            if (name.Length == 0 || name.Any(char.IsWhiteSpace) || name.StartsWith('-'))
                throw Error(filePath, keyNode, $"Action parameter name '{name}' is invalid.");

            var body = RequireMapping(filePath, valueNode, $"Action parameter '{name}' must be a mapping.");
            var fields = ReadFields(filePath, body);
            RejectUnknownFields(filePath, body, fields, ParameterFields, $"parameter '{name}'");
            if (!fields.TryGetValue("type", out var typeNode))
                throw Error(filePath, body, $"Action parameter '{name}' requires field 'type'.");

            var type = ParseParameterType(filePath, typeNode);
            var description = fields.TryGetValue("description", out var descriptionNode)
                ? ReadString(filePath, descriptionNode, $"Description for action parameter '{name}'")
                : null;
            var position = fields.TryGetValue("position", out var positionNode)
                ? ReadInteger(filePath, positionNode, $"Position for action parameter '{name}'")
                : -1;
            if (position < -1)
                throw Error(filePath, positionNode ?? body,
                    $"Action parameter '{name}' has invalid position {position}.");

            var isFlag = fields.TryGetValue("flag", out var flagNode) &&
                         ReadBoolean(filePath, flagNode, $"Flag for action parameter '{name}'");
            if (isFlag && type != ActionParameterType.Boolean)
                throw Error(filePath, flagNode!, $"Flag action parameter '{name}' must have type bool.");
            if (isFlag && position >= 0)
                throw Error(filePath, flagNode!, $"Flag action parameter '{name}' cannot be positional.");

            var hasDefault = fields.TryGetValue("default", out var defaultNode);
            var source = Location(filePath, keyNode);
            InvocationScalar? defaultValue = hasDefault
                ? ActionParameterBinder.ConvertValue(
                    ReadScalar(filePath, defaultNode!),
                    type,
                    new InvocationSource(source.Display),
                    name)
                : null;
            definitions.Add(new ActionParameterDefinition(
                name, description, type, position, isFlag, hasDefault, defaultValue, source));
        }

        var duplicate = definitions.GroupBy(static definition => definition.Name, StringComparer.Ordinal)
            .FirstOrDefault(static group => group.Count() > 1);
        if (duplicate is not null)
            throw new CliException($"{filePath}: Action parameter '{duplicate.Key}' is declared more than once.");

        var positional = definitions.Where(static definition => definition.Position >= 0)
            .OrderBy(static definition => definition.Position)
            .ToArray();
        for (var index = 0; index < positional.Length; index++)
        {
            if (positional[index].Position != index)
                throw new CliException($"{filePath}: Action parameter position {index} is missing.");
            if (positional[index].HasDefaultValue &&
                positional.Skip(index + 1).Any(static definition => !definition.HasDefaultValue))
                throw new CliException($"{filePath}: A required positional parameter cannot follow an optional one.");
        }

        return definitions;
    }

    private static IReadOnlyList<ActionStep> ParseSteps(string filePath, YamlNode node)
    {
        if (node is not YamlSequenceNode sequence)
            throw Error(filePath, node, "Action 'steps' must be a sequence.");

        return sequence.Children.Select(child => ParseStep(filePath, child)).ToArray();
    }

    private static ActionStep ParseStep(string filePath, YamlNode node)
    {
        var mapping = RequireMapping(filePath, node, "Each action step must be a mapping.");
        var fields = ReadFields(filePath, mapping);
        var condition = fields.TryGetValue("if", out var conditionNode)
            ? ParseCondition(filePath, conditionNode)
            : null;

        if (fields.TryGetValue("uses", out var usesNode))
        {
            var unknown = fields.Keys.FirstOrDefault(static key => key is not ("if" or "uses" or "with"));
            if (unknown is not null)
                throw Error(filePath, mapping, $"Uses step contains unknown field '{unknown}'.");
            var actionPath = ReadScalar(filePath, usesNode);
            if (actionPath.Kind != InvocationScalarKind.String || actionPath.FormatInvariant().Length == 0)
                throw Error(filePath, usesNode, "Uses path must be a non-empty string scalar.");
            var with = fields.TryGetValue("with", out var withNode)
                ? ParseScalarMapping(filePath, withNode, "Uses 'with' must be a mapping.")
                : new Dictionary<string, InvocationScalar>(StringComparer.Ordinal);
            return new ActionUsesStep(actionPath, with, condition, Location(filePath, node));
        }

        if (fields.ContainsKey("with"))
            throw Error(filePath, fields["with"], "Field 'with' is only valid on a uses step.");
        var operationFields = fields.Keys.Where(static key => key != "if").ToArray();
        if (operationFields.Length != 1)
            throw Error(filePath, mapping, "A normal step must contain exactly one operation head.");

        var head = operationFields[0];
        if (head.Length == 0 || head.Any(char.IsWhiteSpace) || head.StartsWith('-'))
            throw Error(filePath, mapping, $"Operation head '{head}' is invalid.");
        var body = RequireMapping(filePath, fields[head], $"Operation step '{head}' must be a mapping.");
        var bodyFields = ReadFields(filePath, body);
        var kind = bodyFields.TryGetValue("kind", out var kindNode)
            ? ParseKind(filePath, kindNode)
            : [];
        bodyFields.Remove("kind");
        var arguments = bodyFields.ToDictionary(
            static field => field.Key,
            field => ReadOperationArgument(filePath, field.Value),
            StringComparer.Ordinal);
        return new ActionOperationStep(head, kind, arguments, condition, Location(filePath, node));
    }

    private static InvocationArgument ReadOperationArgument(string filePath, YamlNode node)
    {
        if (node is YamlScalarNode)
            return InvocationArgument.Scalar(ReadScalar(filePath, node));
        if (node is YamlSequenceNode sequence)
            return InvocationArgument.List(sequence.Children
                .Select(child => ReadOperationArgumentElement(filePath, child))
                .ToArray());
        throw Error(filePath, node, "Operation argument must be a scalar or scalar sequence.");
    }

    private static InvocationScalar ReadOperationArgumentElement(string filePath, YamlNode node)
    {
        var value = ReadScalar(filePath, node);
        if (value.Kind == InvocationScalarKind.Boolean)
            throw Error(filePath, node, "Operation argument sequences only support string and integer values.");
        return value;
    }

    private static IReadOnlyList<InvocationScalar> ParseKind(string filePath, YamlNode node)
    {
        if (node is YamlScalarNode)
        {
            var value = ReadScalar(filePath, node);
            RequireNonEmptyString(filePath, node, value, "Operation kind");
            return [value];
        }

        if (node is not YamlSequenceNode sequence || sequence.Children.Count == 0)
            throw Error(filePath, node, "Operation kind must be a non-empty string or string sequence.");
        return sequence.Children.Select(child =>
        {
            var value = ReadScalar(filePath, child);
            RequireNonEmptyString(filePath, child, value, "Operation kind segment");
            return value;
        }).ToArray();
    }

    private static ActionCondition ParseCondition(string filePath, YamlNode node)
    {
        var source = Location(filePath, node);
        if (node is YamlScalarNode)
            return new ScalarActionCondition(ReadScalar(filePath, node), source);

        var mapping = RequireMapping(filePath, node, "Condition must be a scalar or mapping.");
        var fields = ReadFields(filePath, mapping);
        if (fields.Count != 1)
            throw Error(filePath, node, "Condition mapping must contain exactly one operator.");
        var (operation, valueNode) = fields.Single();
        return operation switch
        {
            "equals" => ParseEqualsCondition(filePath, valueNode, source),
            "not" => new NotActionCondition(ParseCondition(filePath, valueNode), source),
            "all" => new AllActionCondition(ParseConditionSequence(filePath, valueNode, "all"), source),
            "any" => new AnyActionCondition(ParseConditionSequence(filePath, valueNode, "any"), source),
            _ => throw Error(filePath, node, $"Unknown condition operator '{operation}'."),
        };
    }

    private static EqualsActionCondition ParseEqualsCondition(
        string filePath,
        YamlNode node,
        ActionSourceLocation source)
    {
        if (node is not YamlSequenceNode sequence || sequence.Children.Count != 2)
            throw Error(filePath, node, "Condition 'equals' requires exactly two scalar values.");
        return new EqualsActionCondition(
            ReadScalar(filePath, sequence.Children[0]),
            ReadScalar(filePath, sequence.Children[1]),
            source);
    }

    private static IReadOnlyList<ActionCondition> ParseConditionSequence(
        string filePath,
        YamlNode node,
        string operation)
    {
        if (node is not YamlSequenceNode sequence || sequence.Children.Count == 0)
            throw Error(filePath, node, $"Condition '{operation}' requires a non-empty sequence.");
        return sequence.Children.Select(child => ParseCondition(filePath, child)).ToArray();
    }

    private static IReadOnlyDictionary<string, InvocationScalar> ParseScalarMapping(
        string filePath,
        YamlNode node,
        string errorMessage)
    {
        var mapping = RequireMapping(filePath, node, errorMessage);
        return ReadFields(filePath, mapping).ToDictionary(
            static field => field.Key,
            field => ReadScalar(filePath, field.Value),
            StringComparer.Ordinal);
    }

    private static ActionParameterType ParseParameterType(string filePath, YamlNode node)
    {
        var value = ReadScalar(filePath, node);
        if (value.Kind != InvocationScalarKind.String)
            throw Error(filePath, node, "Action parameter type must be a string.");
        return (string)value.Value switch
        {
            "string" => ActionParameterType.String,
            "bool" => ActionParameterType.Boolean,
            "int" or "integer" => ActionParameterType.Integer,
            var type => throw Error(filePath, node, $"Unknown action parameter type '{type}'."),
        };
    }

    private static InvocationScalar ReadScalar(string filePath, YamlNode node)
    {
        if (node is not YamlScalarNode scalar)
            throw Error(filePath, node, "Expected a scalar value.");
        var value = scalar.Value ?? throw Error(filePath, node, "Null scalar values are not supported.");

        if (scalar.Style is ScalarStyle.SingleQuoted or ScalarStyle.DoubleQuoted or
            ScalarStyle.Literal or ScalarStyle.Folded)
            return InvocationScalar.String(value);
        if (value is "null" or "Null" or "NULL" or "~")
            throw Error(filePath, node, "Null scalar values are not supported.");
        if (bool.TryParse(value, out var boolean))
            return InvocationScalar.Boolean(boolean);
        if (long.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out var integer))
            return InvocationScalar.Integer(integer);
        return InvocationScalar.String(value);
    }

    private static string ReadString(string filePath, YamlNode node, string context)
    {
        var value = ReadScalar(filePath, node);
        if (value.Kind != InvocationScalarKind.String)
            throw Error(filePath, node, $"{context} must be a string.");
        return (string)value.Value;
    }

    private static int ReadInteger(string filePath, YamlNode node, string context)
    {
        try
        {
            return checked((int)InvocationScalarConverter.ToInteger(ReadScalar(filePath, node)));
        }
        catch (Exception exception) when (exception is FormatException or OverflowException)
        {
            throw Error(filePath, node, $"{context} must be an integer.");
        }
    }

    private static bool ReadBoolean(string filePath, YamlNode node, string context)
    {
        try
        {
            return InvocationScalarConverter.ToBoolean(ReadScalar(filePath, node));
        }
        catch (FormatException)
        {
            throw Error(filePath, node, $"{context} must be boolean.");
        }
    }

    private static Dictionary<string, YamlNode> ReadFields(string filePath, YamlMappingNode mapping)
    {
        var result = new Dictionary<string, YamlNode>(StringComparer.Ordinal);
        foreach (var (keyNode, valueNode) in mapping.Children)
        {
            var key = ReadKey(filePath, keyNode);
            if (!result.TryAdd(key, valueNode))
                throw Error(filePath, keyNode, $"Field '{key}' occurs more than once.");
        }

        return result;
    }

    private static string ReadKey(string filePath, YamlNode node)
    {
        if (node is not YamlScalarNode scalar || scalar.Value is null)
            throw Error(filePath, node, "Mapping keys must be string scalars.");
        return scalar.Value;
    }

    private static YamlMappingNode RequireMapping(string filePath, YamlNode node, string message)
    {
        if (node is not YamlMappingNode mapping)
            throw Error(filePath, node, message);
        return mapping;
    }

    private static void RejectUnknownFields(
        string filePath,
        YamlNode node,
        IReadOnlyDictionary<string, YamlNode> fields,
        IReadOnlySet<string> allowed,
        string context)
    {
        var unknown = fields.Keys.FirstOrDefault(key => !allowed.Contains(key));
        if (unknown is not null)
            throw Error(filePath, node, $"Unknown {context} field '{unknown}'.");
    }

    private static void RequireNonEmptyString(
        string filePath,
        YamlNode node,
        InvocationScalar value,
        string context)
    {
        if (value.Kind != InvocationScalarKind.String || value.FormatInvariant().Length == 0)
            throw Error(filePath, node, $"{context} must be a non-empty string.");
    }

    private static ActionSourceLocation Location(string filePath, YamlNode node) =>
        new(filePath, checked((int)node.Start.Line + 1), checked((int)node.Start.Column + 1));

    private static CliException Error(string filePath, YamlNode node, string message) =>
        new($"{filePath}:{node.Start.Line + 1}:{node.Start.Column + 1}: {message}");

    private static StringComparer PathComparer =>
        OperatingSystem.IsWindows() ? StringComparer.OrdinalIgnoreCase : StringComparer.Ordinal;
}
