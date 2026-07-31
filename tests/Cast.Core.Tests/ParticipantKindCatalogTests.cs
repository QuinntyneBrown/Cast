using Cast.Core.Models;
using Cast.Core.Services;

namespace Cast.Core.Tests;

public sealed class ParticipantKindCatalogTests
{
    private static readonly ParticipantKindCatalog Catalog = new();

    [Fact]
    public void Kinds_CoversEveryEnumValue()
    {
        Assert.Equal(System.Enum.GetValues<ParticipantKind>().Length, Catalog.Kinds.Count);
    }

    [Theory]
    [InlineData(ParticipantKind.Participant, "participant")]
    [InlineData(ParticipantKind.Actor, "actor")]
    [InlineData(ParticipantKind.Database, "database")]
    [InlineData(ParticipantKind.Collections, "collections")]
    public void KeywordFor_ReturnsLowercaseKeyword(ParticipantKind kind, string expected)
    {
        Assert.Equal(expected, Catalog.KeywordFor(kind));
    }

    [Theory]
    [InlineData("actor", ParticipantKind.Actor)]
    [InlineData("ACTOR", ParticipantKind.Actor)]
    [InlineData("Database", ParticipantKind.Database)]
    public void TryResolve_KnownKeyword_ResolvesCaseInsensitively(string keyword, ParticipantKind expected)
    {
        Assert.True(Catalog.TryResolve(keyword, out ParticipantKind kind));
        Assert.Equal(expected, kind);
    }

    [Theory]
    [InlineData("widget")]
    [InlineData("")]
    public void TryResolve_UnknownKeyword_ReturnsFalse(string keyword)
    {
        Assert.False(Catalog.TryResolve(keyword, out _));
    }
}
