using Xunit;

namespace HollowWake.Core.Tests;

public class SmokeTests
{
    [Fact]
    public void CoreLoads() => Assert.Equal("Hollow Wake", CoreInfo.Name);
}
