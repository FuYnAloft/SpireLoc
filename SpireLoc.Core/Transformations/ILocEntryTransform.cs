using SpireLoc.Core.Models;

namespace SpireLoc.Core.Transformations;

/// <summary>Transforms one structured localization entry between source and game representations.</summary>
public interface ILocEntryTransform
{
    LocEntry ToGame(LocEntry entry, LocEntryTransformContext context);
    LocEntry ToSource(LocEntry entry, LocEntryTransformContext context);
}
