using SpireLoc.Core.Models;

namespace SpireLoc.Core.Transformations;

public static class LocTableMapper
{
    public static LocBundle Map(
        LocBundle bundle,
        Func<LocTablePath, LocTable, LocTable> mapper)
    {
        ArgumentNullException.ThrowIfNull(bundle);
        ArgumentNullException.ThrowIfNull(mapper);

        return new LocBundle(bundle.Select(table =>
            KeyValuePair.Create(table.Key, mapper(table.Key, table.Value))));
    }
}
