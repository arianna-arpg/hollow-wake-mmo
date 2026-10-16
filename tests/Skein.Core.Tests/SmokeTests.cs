using Xunit;

namespace Skein.Core.Tests;

public class SmokeTests
{
    [Fact]
    public void CoreLoads() => Assert.Equal("Skein", CoreInfo.Name);
}
