using System.Reflection;
using System.Text;
using SpireLoc.Cli.Pipeline;
using SpireLoc.Core.Execution;
using SpireLoc.Core.Registration;
using SpireLoc.Core.Steps.Support;

namespace SpireLoc.Cli.Registration;

internal sealed class OperationRegistry
{
    public const string ActionHead = "action";
    public const string KindParameterName = "kind";

    private readonly Dictionary<string, OperationFactoryDescriptor[]> _byHead;
    private readonly Dictionary<string, OperationFactoryDescriptor> _byPath;
    private readonly HashSet<string> _heads;

    private OperationRegistry(IReadOnlyList<OperationFactoryDescriptor> descriptors)
    {
        Descriptors = descriptors
            .OrderBy(static descriptor => descriptor.Path[0], StringComparer.Ordinal)
            .ThenBy(static descriptor => string.Join('\0', descriptor.Path), StringComparer.Ordinal)
            .ToArray();

        var duplicate = Descriptors
            .GroupBy(static descriptor => string.Join('\0', descriptor.Path), StringComparer.Ordinal)
            .FirstOrDefault(static group => group.Count() > 1);
        if (duplicate is not null)
            throw new CliException($"Operation path '{duplicate.First().DisplayPath}' is registered more than once.");

        if (Descriptors.Any(static descriptor => descriptor.Path[0] == ActionHead))
            throw new CliException($"Operation head '--{ActionHead}' is reserved for action expansion.");

        _heads = Descriptors.Select(static descriptor => descriptor.Path[0])
            .ToHashSet(StringComparer.Ordinal);
        _heads.Add(ActionHead);
        foreach (var descriptor in Descriptors)
        {
            var kindConflict = descriptor.Parameters.FirstOrDefault(static parameter =>
                parameter.Name == KindParameterName);
            if (kindConflict is not null)
            {
                throw new CliException(
                    $"Parameter '--{KindParameterName}' on '{descriptor.DisplayPath}' is reserved for operation subcommands.");
            }

            var conflict = descriptor.Parameters.FirstOrDefault(parameter => _heads.Contains(parameter.Name));
            if (conflict is not null)
            {
                throw new CliException(
                    $"Parameter '--{conflict.Name}' on '{descriptor.DisplayPath}' conflicts with an operation head.");
            }
        }

        _byHead = Descriptors
            .GroupBy(static descriptor => descriptor.Path[0], StringComparer.Ordinal)
            .ToDictionary(
                static group => group.Key,
                static group => group.OrderByDescending(static descriptor => descriptor.Path.Count).ToArray(),
                StringComparer.Ordinal);
        _byPath = Descriptors.ToDictionary(
            static descriptor => PathKey(descriptor.Path),
            StringComparer.Ordinal);
    }

    public IReadOnlyList<OperationFactoryDescriptor> Descriptors { get; }

    public static OperationRegistry Scan(params Assembly[] assemblies)
    {
        var descriptors = new List<OperationFactoryDescriptor>();
        foreach (var assembly in assemblies.Distinct())
        {
            foreach (var type in assembly.GetTypes())
            {
                foreach (var method in type.GetMethods(
                             BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static |
                             BindingFlags.Instance | BindingFlags.DeclaredOnly))
                {
                    var attribute = method.GetCustomAttribute<OperationFactoryAttribute>();
                    if (attribute is not null)
                        descriptors.Add(CreateDescriptor(method, attribute));
                }

                foreach (var constructor in type.GetConstructors(
                             BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance))
                {
                    var attribute = constructor.GetCustomAttribute<OperationFactoryAttribute>();
                    if (attribute is not null)
                        descriptors.Add(CreateDescriptor(constructor, attribute));
                }
            }
        }

        return new OperationRegistry(descriptors);
    }

    public bool IsStepHead(string token) =>
        token.StartsWith("--", StringComparison.Ordinal) &&
        token.Length > 2 &&
        _heads.Contains(token[2..]);

    public bool IsStepHeadName(string name) => _heads.Contains(name);

    public OperationFactoryDescriptor Resolve(IReadOnlyList<string> tokens, int startIndex, out int consumed)
    {
        var token = tokens[startIndex];
        if (!token.StartsWith("--", StringComparison.Ordinal) || token.Length == 2)
            throw new CliException($"Expected an operation head at '{token}'.");

        var head = token[2..];
        if (!_byHead.TryGetValue(head, out var candidates))
            throw new CliException($"Unknown operation head '--{head}'.");

        foreach (var candidate in candidates)
        {
            if (candidate.Path.Count > tokens.Count - startIndex)
                continue;

            var matches = true;
            for (var pathIndex = 1; pathIndex < candidate.Path.Count; pathIndex++)
            {
                if (string.Equals(
                        tokens[startIndex + pathIndex], candidate.Path[pathIndex], StringComparison.Ordinal))
                    continue;

                matches = false;
                break;
            }

            if (!matches)
                continue;

            consumed = candidate.Path.Count;
            return candidate;
        }

        var variants = candidates
            .Select(static candidate => string.Join(' ', candidate.Path.Skip(1)))
            .Where(static variant => variant.Length > 0)
            .Distinct(StringComparer.Ordinal);
        throw new CliException(
            $"Unknown subcommand for '--{head}'. Expected one of: {string.Join(", ", variants)}.");
    }

    public OperationFactoryDescriptor Resolve(IReadOnlyList<string> path, InvocationSource source)
    {
        if (_byPath.TryGetValue(PathKey(path), out var descriptor))
            return descriptor;
        throw source.Error($"Unknown operation path '--{path[0]} {string.Join(' ', path.Skip(1))}'.");
    }

    private static OperationFactoryDescriptor CreateDescriptor(MethodBase member, OperationFactoryAttribute attribute)
    {
        ValidatePath(attribute.Path, member);

        Type resultType;
        Func<object?[], object> invoke;
        switch (member)
        {
            case MethodInfo method:
                if (!method.IsStatic)
                    throw RegistrationError(member, "Factory methods must be static.");
                resultType = method.ReturnType;
                invoke = arguments => method.Invoke(null, arguments)!;
                break;
            case ConstructorInfo constructor:
                resultType = constructor.DeclaringType!;
                invoke = arguments => constructor.Invoke(arguments);
                break;
            default:
                throw RegistrationError(member, "Only methods and constructors can be operation factories.");
        }

        var producesOperation = typeof(ILocOperation).IsAssignableFrom(resultType);
        var producesUnaryProcessor = typeof(UnaryLocBundleProcessor).IsAssignableFrom(resultType);
        if (!producesOperation && !producesUnaryProcessor)
        {
            throw RegistrationError(
                member,
                $"Factory result type must implement {nameof(ILocOperation)} or derive from {nameof(UnaryLocBundleProcessor)}.");
        }

        var memberParameters = member.GetParameters();
        var parameters = memberParameters
            .Select((parameter, index) => CreateParameterDescriptor(parameter, index, member))
            .ToList();
        ValidateParameters(parameters, member);

        if (producesUnaryProcessor)
        {
            AddInjectedSlotParameter(parameters, "from", member);
            AddInjectedSlotParameter(parameters, "to", member);
        }

        return new OperationFactoryDescriptor(
            attribute.Path.ToArray(),
            parameters,
            memberParameters.Length,
            producesUnaryProcessor,
            invoke);
    }

    private static OperationParameterDescriptor CreateParameterDescriptor(
        ParameterInfo parameter,
        int invocationIndex,
        MemberInfo member)
    {
        if (parameter.ParameterType.IsByRef || parameter.IsOut ||
            parameter.GetCustomAttribute<ParamArrayAttribute>() is not null)
            throw RegistrationError(member, $"Parameter '{parameter.Name}' uses an unsupported parameter shape.");

        var attribute = parameter.GetCustomAttribute<OperationParameterAttribute>();
        var name = attribute?.Name ?? ToKebabCase(parameter.Name!);
        var position = attribute?.Position ?? -1;
        var isFlag = attribute?.IsFlag ?? false;
        var listElementType = GetSupportedListElementType(parameter.ParameterType);

        if (parameter.ParameterType.IsGenericType &&
            parameter.ParameterType.GetGenericTypeDefinition() == typeof(IReadOnlyList<>) &&
            listElementType is null)
        {
            throw RegistrationError(
                member,
                $"List parameter '{name}' must have element type string or int.");
        }

        ValidateName(name, member);
        if (position < -1)
            throw RegistrationError(member, $"Parameter '{name}' has invalid position {position}.");
        if (isFlag && listElementType is not null)
            throw RegistrationError(member, $"List parameter '{name}' cannot be a flag.");
        if (isFlag && parameter.ParameterType != typeof(bool))
            throw RegistrationError(member, $"Flag parameter '{name}' must have type bool.");
        if (isFlag && position >= 0)
            throw RegistrationError(member, $"Flag parameter '{name}' cannot be positional.");

        return new OperationParameterDescriptor(
            name,
            parameter.ParameterType,
            listElementType,
            position,
            isFlag,
            isFlag || parameter.HasDefaultValue,
            isFlag ? false : parameter.DefaultValue,
            invocationIndex);
    }

    private static void ValidateParameters(
        IReadOnlyList<OperationParameterDescriptor> parameters,
        MemberInfo member)
    {
        var duplicateName = parameters.GroupBy(static parameter => parameter.Name, StringComparer.Ordinal)
            .FirstOrDefault(static group => group.Count() > 1);
        if (duplicateName is not null)
            throw RegistrationError(member, $"Parameter name '{duplicateName.Key}' occurs more than once.");

        var positional = parameters.Where(static parameter => parameter.Position >= 0)
            .OrderBy(static parameter => parameter.Position)
            .ToArray();
        for (var index = 0; index < positional.Length; index++)
        {
            if (positional[index].Position != index)
            {
                throw RegistrationError(
                    member,
                    $"Positional parameters must be contiguous from 0; position {index} is missing.");
            }

            if (positional[index].HasDefaultValue &&
                positional.Skip(index + 1).Any(static parameter => !parameter.HasDefaultValue))
            {
                throw RegistrationError(member, "A required positional parameter cannot follow an optional one.");
            }
        }

        var positionalList = positional.FirstOrDefault(static parameter => parameter.IsList);
        if (positionalList is not null && positionalList.Position != positional.Length - 1)
        {
            throw RegistrationError(
                member,
                $"Positional list parameter '{positionalList.Name}' must be the last positional parameter.");
        }
    }

    private static void AddInjectedSlotParameter(
        ICollection<OperationParameterDescriptor> parameters,
        string name,
        MemberInfo member)
    {
        if (parameters.Any(parameter => string.Equals(parameter.Name, name, StringComparison.Ordinal)))
            throw RegistrationError(member, $"Unary processor factories reserve parameter name '{name}'.");

        parameters.Add(new OperationParameterDescriptor(
            name,
            typeof(string),
            null,
            -1,
            false,
            true,
            UnaryLocBundleProcessorStep.DefaultSlotName,
            -1));
    }

    private static void ValidatePath(IReadOnlyList<string> path, MemberInfo member)
    {
        if (path.Count == 0)
            throw RegistrationError(member, "Operation path cannot be empty.");

        foreach (var segment in path)
        {
            if (segment.Length == 0 || segment.Any(char.IsWhiteSpace) || segment.StartsWith('-'))
                throw RegistrationError(member, $"Operation path segment '{segment}' is invalid.");
        }
    }

    private static void ValidateName(string name, MemberInfo member)
    {
        if (name.Length == 0 || name.Any(char.IsWhiteSpace) || name.StartsWith('-'))
            throw RegistrationError(member, $"Operation parameter name '{name}' is invalid.");
    }

    private static Type? GetSupportedListElementType(Type parameterType)
    {
        if (!parameterType.IsGenericType ||
            parameterType.GetGenericTypeDefinition() != typeof(IReadOnlyList<>))
            return null;

        var elementType = parameterType.GetGenericArguments()[0];
        return elementType == typeof(string) || elementType == typeof(int)
            ? elementType
            : null;
    }

    private static string ToKebabCase(string value)
    {
        var builder = new StringBuilder(value.Length + 4);
        for (var index = 0; index < value.Length; index++)
        {
            var current = value[index];
            if (current is '_' or ' ')
            {
                if (builder.Length > 0 && builder[^1] != '-')
                    builder.Append('-');
                continue;
            }

            var needsSeparator = char.IsUpper(current) && index > 0 && builder[^1] != '-' &&
                                 (char.IsLower(value[index - 1]) || char.IsDigit(value[index - 1]) ||
                                  (char.IsUpper(value[index - 1]) && index + 1 < value.Length &&
                                   char.IsLower(value[index + 1])));
            if (needsSeparator)
                builder.Append('-');
            builder.Append(char.ToLowerInvariant(current));
        }

        return builder.ToString();
    }

    private static CliException RegistrationError(MemberInfo member, string message) =>
        new($"Invalid operation factory '{member.DeclaringType!.FullName}.{member.Name}': {message}");

    private static string PathKey(IEnumerable<string> path) => string.Join('\0', path);
}
