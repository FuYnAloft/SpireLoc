using SpireLoc.Core.Models;

namespace SpireLoc.Core.Transformations;

public sealed class DelegateLocEntryConverter(Func<LocEntry, LocEntry> converter) : ILocEntryConverter
{
    public LocEntry Convert(LocEntry entry) => converter(entry);
}
