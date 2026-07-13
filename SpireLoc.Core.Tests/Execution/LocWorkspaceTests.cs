using SpireLoc.Core.Execution;
using Xunit;

namespace SpireLoc.Core.Tests.Execution;

public sealed class LocWorkspaceTests
{
    [Fact]
    public void SetReturnsNewWorkspaceWithoutChangingOldWorkspace()
    {
        var first = new TestArtifact("first");
        var second = new TestArtifact("second");
        var original = LocWorkspace.Empty;
        var created = original.Set("bundle", first);
        var overwritten = created.Set("bundle", second);

        Assert.Empty(original);
        Assert.Same(first, created.Require<TestArtifact>("bundle"));
        Assert.Same(second, overwritten.Require<TestArtifact>("bundle"));
    }

    [Fact]
    public void SlotNamespaceAcceptsDifferentArtifactTypes()
    {
        var workspace = LocWorkspace.Empty
            .Set("first", new TestArtifact("artifact"))
            .Set("second", new OtherTestArtifact(2));

        Assert.IsType<TestArtifact>(workspace["first"]);
        Assert.IsType<OtherTestArtifact>(workspace["second"]);
    }

    [Fact]
    public void SetOverwritesAndRemoveMissingSlotHasSpecificError()
    {
        var workspace = LocWorkspace.Empty.Set("slot", new TestArtifact("value"));
        var overwritten = workspace.Set("slot", new TestArtifact("other"));

        var missing = Assert.Throws<MissingSlotException>(() => workspace.Remove("missing"));

        Assert.Equal("other", overwritten.Require<TestArtifact>("slot").Value);
        Assert.Equal("missing", missing.SlotName);
    }

    [Fact]
    public void RequireReportsSlotAndBothTypesOnMismatch()
    {
        var workspace = LocWorkspace.Empty.Set("slot", new TestArtifact("value"));

        var exception = Assert.Throws<SlotTypeMismatchException>(() => workspace.Require<OtherTestArtifact>("slot"));

        Assert.Equal("slot", exception.SlotName);
        Assert.Equal(typeof(OtherTestArtifact), exception.ExpectedType);
        Assert.Equal(typeof(TestArtifact), exception.ActualType);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public void BlankSlotNamesAreRejected(string slotName)
    {
        Assert.Throws<ArgumentException>(() => LocWorkspace.Empty.Set(slotName, new TestArtifact("value")));
    }

    private sealed record TestArtifact(string Value) : ILocArtifact;

    private sealed record OtherTestArtifact(int Value) : ILocArtifact;
}
