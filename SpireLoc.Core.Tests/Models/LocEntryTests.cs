using SpireLoc.Core.Models;
using Xunit;

namespace SpireLoc.Core.Tests.Models;

public sealed class LocEntryTests
{
    [Fact]
    public void EqualityUsesKeyContentsRatherThanListIdentity()
    {
        var left = new LocEntry(["Card", "title"], "Strike");
        var right = new LocEntry(new List<string> { "Card", "title" }, "Strike");

        Assert.Equal(left, right);
        Assert.Equal(left.GetHashCode(), right.GetHashCode());
    }

    [Fact]
    public void ConstructorTakesSnapshotOfKey()
    {
        var key = new List<string> { "Card", "title" };
        var entry = new LocEntry(key, "Strike");

        key[0] = "Changed";

        Assert.Equal(["Card", "title"], entry.Key);
    }

    [Fact]
    public void EmptyKeyIsRejected()
    {
        Assert.Throws<ArgumentException>(() => new LocEntry([], "value"));
    }
}
