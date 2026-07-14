using Xunit;

namespace SpireLoc.Cli.Tests;

public sealed class ProgramTests
{
    [Fact]
    public void DisplayVersionUsesPackageVersionWithoutSourceRevision()
    {
        var version = Program.GetVersion();

        Assert.Matches(@"^\d+\.\d+\.\d+(?:-[0-9A-Za-z.-]+)?$", version);
        Assert.DoesNotContain('+', version);
    }
}
