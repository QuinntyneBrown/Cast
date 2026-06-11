using System.Collections.Generic;
using Cast.Cli.Diagnostics;
using Cast.Cli.Models;
using Cast.Cli.Services;

namespace Cast.Cli.Tests;

public sealed class DiagramSpecValidatorTests
{
    private static DiagramSpecValidator CreateValidator() =>
        new(new ParticipantParser(new ParticipantKindCatalog()), new MessageParser());

    [Fact]
    public void ParseParticipants_ValidSpecs_ReturnsParsedParticipants()
    {
        DiagramSpecValidator validator = CreateValidator();

        IReadOnlyList<Participant> participants =
            validator.ParseParticipants(["actor:User", "OS:Order Service"]);

        Assert.Equal(2, participants.Count);
        Assert.Equal("User", participants[0].Alias);
        Assert.Equal("Order Service", participants[1].DisplayName);
    }

    [Fact]
    public void ParseParticipants_DuplicateAlias_Throws()
    {
        DiagramSpecValidator validator = CreateValidator();

        var ex = Assert.Throws<DiagramFormatException>(() => validator.ParseParticipants(["A", "A"]));

        Assert.Contains("Duplicate participant alias 'A'", ex.Message);
    }

    [Fact]
    public void ParseMessages_ValidSpecs_ReturnsParsedMessages()
    {
        DiagramSpecValidator validator = CreateValidator();
        IReadOnlyList<Participant> participants = validator.ParseParticipants(["User", "OS"]);

        IReadOnlyList<Message> messages =
            validator.ParseMessages(["User -> OS : place order"], participants);

        Message message = Assert.Single(messages);
        Assert.Equal("User", message.Source);
        Assert.Equal("OS", message.Target);
        Assert.Equal("place order", message.Label);
    }

    [Fact]
    public void ParseMessages_UnknownEndpoint_ThrowsListingKnownAliases()
    {
        DiagramSpecValidator validator = CreateValidator();
        IReadOnlyList<Participant> participants = validator.ParseParticipants(["A", "B"]);

        var ex = Assert.Throws<DiagramFormatException>(
            () => validator.ParseMessages(["A -> Z : oops"], participants));

        Assert.Contains("unknown participant 'Z'", ex.Message);
        Assert.Contains("Known aliases: A, B", ex.Message);
    }

    [Fact]
    public void ValidateMetadata_SingleLineTitleAndSingleTokenTheme_Passes()
    {
        DiagramSpecValidator validator = CreateValidator();

        validator.ValidateMetadata("Checkout flow", "plain");
    }

    [Fact]
    public void ValidateMetadata_TitleWithControlChar_Throws()
    {
        DiagramSpecValidator validator = CreateValidator();

        Assert.Throws<DiagramFormatException>(() => validator.ValidateMetadata("Bad\nTitle", null));
    }

    [Fact]
    public void ValidateMetadata_ThemeWithWhitespace_Throws()
    {
        DiagramSpecValidator validator = CreateValidator();

        Assert.Throws<DiagramFormatException>(() => validator.ValidateMetadata(null, "my theme"));
    }
}
