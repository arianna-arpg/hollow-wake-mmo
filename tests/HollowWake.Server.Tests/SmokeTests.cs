using Xunit;

namespace HollowWake.Server.Tests;

public class SmokeTests
{
    [Fact]
    public void ServerLoads() => Assert.Equal("HollowWake.Server", ServerInfo.Name);
}
