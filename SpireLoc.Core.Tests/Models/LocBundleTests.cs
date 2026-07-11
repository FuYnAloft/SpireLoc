using SpireLoc.Core.Models;
using Xunit;

namespace SpireLoc.Core.Tests.Models;

public sealed class LocBundleTests
{
    [Fact]
    public void StoresLanguageAndTableAsOrthogonalPathDimensions()
    {
        var zhsCards = new LocTablePath("zhs", "cards");
        var zhsUi = new LocTablePath("zhs", "ui");
        var enCards = new LocTablePath("en", "cards");
        var bundle = new LocBundle(new Dictionary<LocTablePath, LocTable>
        {
            [zhsCards] = TableWith("zhs card"),
            [zhsUi] = TableWith("zhs ui"),
            [enCards] = TableWith("en card")
        });

        Assert.Equal(3, bundle.Count);
        Assert.Equal("zhs card", bundle[zhsCards].Single().Value);
        Assert.Equal("zhs ui", bundle[zhsUi].Single().Value);
        Assert.Equal("en card", bundle[enCards].Single().Value);
    }

    [Theory]
    [InlineData("", "cards")]
    [InlineData("zhs", "")]
    [InlineData(" ", "cards")]
    [InlineData("zhs", " ")]
    public void BlankPathSegmentsAreRejected(string language, string tableName)
    {
        Assert.Throws<ArgumentException>(() => new LocTablePath(language, tableName));
    }

    private static LocTable TableWith(string value) =>
        new([new LocEntry(["entry"], value)]);
}
