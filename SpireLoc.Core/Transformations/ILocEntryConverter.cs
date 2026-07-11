using SpireLoc.Core.Models;

namespace SpireLoc.Core.Transformations;

public interface ILocEntryConverter
{
    LocEntry Convert(LocEntry entry);
}
