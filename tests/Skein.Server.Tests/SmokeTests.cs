using Xunit;

namespace Skein.Server.Tests;

public class SmokeTests
{
    [Fact]
    public void ServerLoads() => Assert.Equal("Skein.Server", ServerInfo.Name);
}
