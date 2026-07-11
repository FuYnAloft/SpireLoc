using SpireLoc.Core.Models;

namespace SpireLoc.Core.Transformations;

/// <summary>Base class for transforms that should round-trip between source and game representations.</summary>
public abstract class ReversibleLocEntryTransform : ILocEntryTransform
{
    public LocEntry ToGame(LocEntry entry, LocEntryTransformContext context)
    {
        ArgumentNullException.ThrowIfNull(entry);
        ArgumentNullException.ThrowIfNull(context);

        var transformed = TransformToGame(entry, context);
        VerifyRoundTrip(entry, TransformToSource(transformed, context), "ToGame", context);
        return transformed;
    }

    public LocEntry ToSource(LocEntry entry, LocEntryTransformContext context)
    {
        ArgumentNullException.ThrowIfNull(entry);
        ArgumentNullException.ThrowIfNull(context);

        var transformed = TransformToSource(entry, context);
        VerifyRoundTrip(entry, TransformToGame(transformed, context), "ToSource", context);
        return transformed;
    }

    protected abstract LocEntry TransformToGame(LocEntry entry, LocEntryTransformContext context);
    protected abstract LocEntry TransformToSource(LocEntry entry, LocEntryTransformContext context);

    private static void VerifyRoundTrip(
        LocEntry original,
        LocEntry reverted,
        string direction,
        LocEntryTransformContext context)
    {
        if (original != reverted)
        {
            context.ReportWarning(
                "EntryTransform.NonReversible",
                $"{direction} conversion is not reversible: {original} -> {reverted}.");
        }
    }
}
