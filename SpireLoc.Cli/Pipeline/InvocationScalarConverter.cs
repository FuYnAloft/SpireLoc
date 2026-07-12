using System.Globalization;

namespace SpireLoc.Cli.Pipeline;

internal static class InvocationScalarConverter
{
    public static object ConvertTo(InvocationScalar scalar, Type valueType)
    {
        var targetType = Nullable.GetUnderlyingType(valueType) ?? valueType;
        if (targetType == typeof(string))
            return scalar.FormatInvariant();
        if (targetType == typeof(bool))
            return ToBoolean(scalar);
        if (targetType.IsEnum)
            return Enum.Parse(targetType, RequireString(scalar), ignoreCase: true);
        if (targetType == typeof(FileInfo))
            return new FileInfo(RequireString(scalar));
        if (targetType == typeof(DirectoryInfo))
            return new DirectoryInfo(RequireString(scalar));
        if (targetType == typeof(Guid))
            return Guid.Parse(RequireString(scalar));
        if (targetType == typeof(TimeSpan))
            return TimeSpan.Parse(RequireString(scalar), CultureInfo.InvariantCulture);

        var value = scalar.Kind == InvocationScalarKind.Integer
            ? scalar.Value
            : scalar.FormatInvariant();
        return Convert.ChangeType(value, targetType, CultureInfo.InvariantCulture);
    }

    public static bool ToBoolean(InvocationScalar scalar) => scalar.Kind switch
    {
        InvocationScalarKind.Boolean => (bool)scalar.Value,
        InvocationScalarKind.String when bool.TryParse((string)scalar.Value, out var value) => value,
        _ => throw new FormatException($"'{scalar.FormatInvariant()}' is not a boolean value."),
    };

    public static long ToInteger(InvocationScalar scalar) => scalar.Kind switch
    {
        InvocationScalarKind.Integer => (long)scalar.Value,
        InvocationScalarKind.String when long.TryParse(
            (string)scalar.Value,
            NumberStyles.Integer,
            CultureInfo.InvariantCulture,
            out var value) => value,
        _ => throw new FormatException($"'{scalar.FormatInvariant()}' is not an integer value."),
    };

    private static string RequireString(InvocationScalar scalar)
    {
        if (scalar.Kind != InvocationScalarKind.String)
            throw new FormatException($"'{scalar.FormatInvariant()}' must be a string value.");
        return (string)scalar.Value;
    }
}
