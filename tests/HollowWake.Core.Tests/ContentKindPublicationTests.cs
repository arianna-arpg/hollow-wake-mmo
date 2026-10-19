using HollowWake.Core.Content;
using HollowWake.Core.Definitions;
using Xunit;

namespace HollowWake.Core.Tests;

public class ContentKindPublicationTests
{
    private sealed record PublishedDef : Definition;
    private sealed record ConcurrentDef : Definition;

    [Fact]
    public void ReplacementAndAddedValidatorsRemainVisibleAfterAnEarlierLookup()
    {
        const string name = "publication_test";
        ContentKinds.Register<PublishedDef>(name, (_, _) => throw new Exception("replaced validator"));
        Assert.True(ContentKinds.TryGet(name, out var old));
        var retained = ContentKinds.All;
        ContentKinds.Register<PublishedDef>(name);
        var seen = 0;
        ContentKinds.AddValidator<PublishedDef>((_, _) => seen++);
        Assert.True(ContentKinds.TryGet(name, out var current));
        Assert.True(ContentKinds.TryGet(typeof(PublishedDef), out var byType));
        Assert.Same(current, byType);
        Assert.NotSame(old, current);
        Assert.Same(old, Assert.Single(retained, k => k.Name == name));
        Assert.Equal(name, ContentKinds.NameOf<PublishedDef>());
        var body = new PublishedDef { Id = "body" };
        var registry = ContentRegistry.Empty().With(body);
        Assert.Same(body, registry.Require<PublishedDef>("body"));
        current!.Validate!(body, new ValidationContext(registry, new ValidationReport(), name, body.Id, null));
        Assert.Equal(1, seen);
    }

    [Fact]
    public void ConcurrentPublicationKeepsReadersOnCompleteIndexes()
    {
        const string name = "publication_concurrent_test";
        ContentKinds.Register<ConcurrentDef>(name);
        Parallel.For(0, 64, _ =>
        {
            ContentKinds.Register<ConcurrentDef>(name);
            Assert.True(ContentKinds.TryGet(name, out var named));
            Assert.Equal(typeof(ConcurrentDef), named!.DefinitionType);
            Assert.True(ContentKinds.TryGet(typeof(ConcurrentDef), out var typed));
            Assert.Equal(name, typed!.Name);
            Assert.Single(ContentKinds.All, kind => kind.Name == name);
        });
    }
}
