using HollowWake.Client.UI.Logic;
using Xunit;

namespace HollowWake.Client.Tests;

public class ResumeCharacterTests
{
    [Fact]
    public void ResumeNeverGuessesBetweenCharactersOrCreatesOne()
    {
        Assert.Null(ResumeCharacter.Select([], null));
        Assert.Equal("only", ResumeCharacter.Select(["only"], null));
        Assert.Equal("last", ResumeCharacter.Select(["first", "last"], "last"));
        Assert.Null(ResumeCharacter.Select(["first", "last"], "deleted"));
        Assert.Null(ResumeCharacter.Select(["first", "last"], null));
    }
}
