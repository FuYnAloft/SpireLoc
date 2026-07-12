namespace SpireLoc.Cli.Pipeline;

internal sealed record InvocationArgument(IReadOnlyList<InvocationScalar> Values, bool IsList)
{
    public static InvocationArgument Scalar(InvocationScalar value) => new([value], false);

    public static InvocationArgument List(IReadOnlyList<InvocationScalar> values) => new(values, true);
}
