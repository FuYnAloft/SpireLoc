using System.Globalization;

namespace SpireLoc.Cli.Pipeline;

internal enum InvocationScalarKind
{
    String,
    Boolean,
    Integer,
}

internal readonly record struct InvocationScalar
{
    private InvocationScalar(object value, InvocationScalarKind kind)
    {
        Value = value;
        Kind = kind;
    }

    public object Value { get; }
    public InvocationScalarKind Kind { get; }

    public static InvocationScalar String(string value) => new(value, InvocationScalarKind.String);
    public static InvocationScalar Boolean(bool value) => new(value, InvocationScalarKind.Boolean);
    public static InvocationScalar Integer(long value) => new(value, InvocationScalarKind.Integer);

    public string FormatInvariant() => Kind switch
    {
        InvocationScalarKind.String => (string)Value,
        InvocationScalarKind.Boolean => (bool)Value ? "true" : "false",
        InvocationScalarKind.Integer => ((long)Value).ToString(CultureInfo.InvariantCulture),
        _ => throw new ArgumentOutOfRangeException(nameof(Kind), Kind, null),
    };

    public bool SemanticallyEquals(InvocationScalar other) =>
        Kind == other.Kind
            ? Equals(Value, other.Value)
            : string.Equals(FormatInvariant(), other.FormatInvariant(), StringComparison.Ordinal);
}
