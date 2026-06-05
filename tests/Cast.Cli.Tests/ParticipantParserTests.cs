using Cast.Cli.Diagnostics;
using Cast.Cli.Models;
using Cast.Cli.Services;

namespace Cast.Cli.Tests;

public sealed class ParticipantParserTests
{
    private static ParticipantParser CreateParser() => new(new ParticipantKindCatalog());

    [Fact]
    public void Parse_BareName_DefaultsToParticipantKind()
    {
        Participant participant = CreateParser().Parse("User");

        Assert.Equal("User", participant.Alias);
        Assert.Equal(ParticipantKind.Participant, participant.Kind);
        Assert.Null(participant.DisplayName);
        Assert.Equal("User", participant.Label);
    }

    [Theory]
    [InlineData("actor:User", ParticipantKind.Actor, "User")]
    [InlineData("database:DB", ParticipantKind.Database, "DB")]
    [InlineData("queue:Q", ParticipantKind.Queue, "Q")]
    [InlineData("ACTOR:User", ParticipantKind.Actor, "User")] // kind keyword is case-insensitive
    public void Parse_KindPrefix_SetsKind(string spec, ParticipantKind expectedKind, string expectedAlias)
    {
        Participant participant = CreateParser().Parse(spec);

        Assert.Equal(expectedKind, participant.Kind);
        Assert.Equal(expectedAlias, participant.Alias);
    }

    [Fact]
    public void Parse_KindAliasAndDisplay_ParsesAllThree()
    {
        Participant participant = CreateParser().Parse("database:DB:Main Database");

        Assert.Equal(ParticipantKind.Database, participant.Kind);
        Assert.Equal("DB", participant.Alias);
        Assert.Equal("Main Database", participant.DisplayName);
        Assert.Equal("Main Database", participant.Label);
    }

    [Fact]
    public void Parse_DisplayNameWithoutKind_DefaultsKind()
    {
        Participant participant = CreateParser().Parse("OS:Order Service");

        Assert.Equal(ParticipantKind.Participant, participant.Kind);
        Assert.Equal("OS", participant.Alias);
        Assert.Equal("Order Service", participant.DisplayName);
    }

    [Fact]
    public void Parse_DisplayNameContainingColon_KeepsTrailingColons()
    {
        Participant participant = CreateParser().Parse("API:GET: /orders");

        Assert.Equal("API", participant.Alias);
        Assert.Equal("GET: /orders", participant.DisplayName);
    }

    [Fact]
    public void Parse_BareKeywordWithoutColon_TreatedAsAlias()
    {
        // "actor" with no following colon is an alias, not a kind prefix.
        Participant participant = CreateParser().Parse("actor");

        Assert.Equal(ParticipantKind.Participant, participant.Kind);
        Assert.Equal("actor", participant.Alias);
    }

    [Fact]
    public void Parse_SurroundingWhitespace_IsTrimmed()
    {
        Participant participant = CreateParser().Parse("  actor : Customer  ");

        Assert.Equal(ParticipantKind.Actor, participant.Kind);
        Assert.Equal("Customer", participant.Alias);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("actor:")]               // missing alias after kind
    [InlineData("Order Service")]        // space in alias
    [InlineData("1User")]                // starts with a digit
    [InlineData("user-service")]         // hyphen not allowed in an alias
    public void Parse_InvalidSpec_Throws(string spec)
    {
        Assert.Throws<DiagramFormatException>(() => CreateParser().Parse(spec));
    }
}
