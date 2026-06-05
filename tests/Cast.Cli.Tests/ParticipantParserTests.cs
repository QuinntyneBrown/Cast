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
    public void Parse_CapitalisedKeyword_IsAllowedAsAlias()
    {
        // PlantUML keywords are lower-case, so a capitalised look-alike is a valid alias.
        Participant participant = CreateParser().Parse("Database");

        Assert.Equal(ParticipantKind.Participant, participant.Kind);
        Assert.Equal("Database", participant.Alias);
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
    [InlineData("café")]                 // non-ASCII letter not allowed
    public void Parse_InvalidSpec_Throws(string spec)
    {
        Assert.Throws<DiagramFormatException>(() => CreateParser().Parse(spec));
    }

    [Theory]
    [InlineData("as")]
    [InlineData("note")]
    [InlineData("end")]
    [InlineData("actor")]                // bare kind keyword
    [InlineData("database")]
    [InlineData("title")]
    public void Parse_ReservedKeywordAlias_Throws(string spec)
    {
        Assert.Throws<DiagramFormatException>(() => CreateParser().Parse(spec));
    }

    [Theory]
    [InlineData("OS:Order \"Special\" Service")] // double quote would break the quoted string
    [InlineData("OS:Line1\nLine2")]              // control character / line break
    public void Parse_DisplayNameWithUnsupportedCharacter_Throws(string spec)
    {
        Assert.Throws<DiagramFormatException>(() => CreateParser().Parse(spec));
    }
}
