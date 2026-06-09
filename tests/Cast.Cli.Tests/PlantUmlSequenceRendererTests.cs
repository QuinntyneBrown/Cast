using System;
using Cast.Cli.Models;
using Cast.Cli.Services;

namespace Cast.Cli.Tests;

public sealed class PlantUmlSequenceRendererTests
{
    private static PlantUmlSequenceRenderer CreateRenderer() => new(new ParticipantKindCatalog());

    private static string[] LinesOf(string rendered) =>
        rendered.Split('\n', StringSplitOptions.None);

    [Fact]
    public void Render_AlwaysWrapsInStartAndEnd()
    {
        var diagram = new SequenceDiagram([new Participant("A", ParticipantKind.Participant)], []);

        string[] lines = LinesOf(CreateRenderer().Render(diagram));

        Assert.Equal("@startuml", lines[0]);
        Assert.Contains("@enduml", lines);
    }

    [Fact]
    public void Render_AliasOnly_EmitsKeywordAndAlias()
    {
        var diagram = new SequenceDiagram([new Participant("U", ParticipantKind.Actor)], []);

        string output = CreateRenderer().Render(diagram);

        Assert.Contains("actor U", output);
    }

    [Fact]
    public void Render_DisplayName_EmitsQuotedAliasForm()
    {
        var diagram = new SequenceDiagram(
            [new Participant("DB", ParticipantKind.Database, "Main Database")], []);

        string output = CreateRenderer().Render(diagram);

        Assert.Contains("database \"Main Database\" as DB", output);
    }

    [Fact]
    public void Render_TitleThemeAutonumber_AreEmitted()
    {
        var diagram = new SequenceDiagram(
            [new Participant("A", ParticipantKind.Participant)],
            [],
            Title: "Checkout flow",
            AutoNumber: true,
            Theme: "plain");

        string output = CreateRenderer().Render(diagram);

        Assert.Contains("title Checkout flow", output);
        Assert.Contains("!theme plain", output);
        Assert.Contains("autonumber", output);
    }

    [Fact]
    public void Render_OmitsOptionalDirectives_WhenNotSet()
    {
        var diagram = new SequenceDiagram([new Participant("A", ParticipantKind.Participant)], []);

        string output = CreateRenderer().Render(diagram);

        Assert.DoesNotContain("title", output);
        Assert.DoesNotContain("!theme", output);
        Assert.DoesNotContain("autonumber", output);
    }

    [Fact]
    public void Render_MessageWithLabel_UsesColonSeparator()
    {
        var diagram = new SequenceDiagram(
            [new Participant("A", ParticipantKind.Participant), new Participant("B", ParticipantKind.Participant)],
            [new Message("A", "B", "->", "do it")]);

        string output = CreateRenderer().Render(diagram);

        Assert.Contains("A -> B : do it", output);
    }

    [Fact]
    public void Render_MessageWithoutLabel_HasNoColon()
    {
        var diagram = new SequenceDiagram(
            [new Participant("A", ParticipantKind.Participant), new Participant("B", ParticipantKind.Participant)],
            [new Message("A", "B", "->")]);

        string[] lines = LinesOf(CreateRenderer().Render(diagram));
        string messageLine = Assert.Single(lines, l => l.StartsWith("A -> B"));

        Assert.Equal("A -> B", messageLine);
    }

    [Fact]
    public void Render_EndsWithSingleTrailingNewline()
    {
        var diagram = new SequenceDiagram([new Participant("A", ParticipantKind.Participant)], []);

        string output = CreateRenderer().Render(diagram);

        Assert.EndsWith("@enduml\n", output);
        Assert.DoesNotContain("\r", output);
    }

    [Fact]
    public void Render_WhitespaceOnlyTitleAndTheme_AreOmitted()
    {
        var diagram = new SequenceDiagram(
            [new Participant("A", ParticipantKind.Participant)], [], Title: "   ", Theme: "  ");

        string output = CreateRenderer().Render(diagram);

        Assert.DoesNotContain("title", output);
        Assert.DoesNotContain("!theme", output);
    }

    [Fact]
    public void Render_TitleAndTheme_AreTrimmed()
    {
        var diagram = new SequenceDiagram(
            [new Participant("A", ParticipantKind.Participant)], [], Title: "  Hello  ", Theme: "  plain  ");

        string output = CreateRenderer().Render(diagram);

        Assert.Contains("title Hello\n", output);
        Assert.Contains("!theme plain\n", output);
    }

    [Fact]
    public void Render_AlwaysEmitsTeozPragmaAndFontSize()
    {
        var diagram = new SequenceDiagram([new Participant("A", ParticipantKind.Participant)], []);

        string output = CreateRenderer().Render(diagram);

        Assert.Contains("!pragma teoz true\n", output);
        Assert.Contains("skinparam defaultFontSize 10\n", output);
    }

    [Fact]
    public void Render_DefaultStyle_WrapsNonActorsInDoubleNestedBox()
    {
        var diagram = new SequenceDiagram(
            [new Participant("A", ParticipantKind.Participant), new Participant("DB", ParticipantKind.Database)], []);

        string output = CreateRenderer().Render(diagram);

        Assert.Contains("box #PHYSICAL\n  box #AZURE\n    participant A\n    database DB\n  end box\nend box\n", output);
    }

    [Fact]
    public void Render_Actor_StaysOutsideTheBoxes()
    {
        var diagram = new SequenceDiagram(
            [new Participant("A", ParticipantKind.Participant), new Participant("U", ParticipantKind.Actor)], []);

        string[] lines = LinesOf(CreateRenderer().Render(diagram));

        // The actor is declared before the box opens, never inside it.
        Assert.Equal("actor U", Assert.Single(lines, l => l.Contains("actor U")));
        Assert.True(Array.IndexOf(lines, "actor U") < Array.IndexOf(lines, "box #PHYSICAL"));
    }

    [Fact]
    public void Render_OnlyActors_EmitsNoBox()
    {
        var diagram = new SequenceDiagram([new Participant("U", ParticipantKind.Actor)], []);

        string output = CreateRenderer().Render(diagram);

        Assert.DoesNotContain("box", output);
    }

    [Fact]
    public void Render_CustomStyle_UsesConfiguredBoxColors()
    {
        var diagram = new SequenceDiagram(
            [new Participant("A", ParticipantKind.Participant)], [],
            Style: new DiagramStyle("#DeepSkyBlue", "#FFFFFF"));

        string output = CreateRenderer().Render(diagram);

        Assert.Contains("box #DeepSkyBlue\n  box #FFFFFF\n", output);
        Assert.DoesNotContain("#PHYSICAL", output);
        Assert.DoesNotContain("#AZURE", output);
    }

    // Golden master: pins the full output — ordering, header comment, and blank-line separators.
    [Fact]
    public void Render_RepresentativeDiagram_MatchesExactOutput()
    {
        var diagram = new SequenceDiagram(
            [
                new Participant("User", ParticipantKind.Actor),
                new Participant("OS", ParticipantKind.Participant, "Order Service"),
            ],
            [
                new Message("User", "OS", "->", "place order"),
                new Message("OS", "User", "-->", "ack"),
            ],
            Title: "Checkout",
            AutoNumber: true,
            Theme: "plain");

        string expected =
            "@startuml\n" +
            "' Scaffolded by cast\n" +
            "!pragma teoz true\n" +
            "!theme plain\n" +
            "skinparam defaultFontSize 10\n" +
            "title Checkout\n" +
            "autonumber\n" +
            "\n" +
            "actor User\n" +
            "box #PHYSICAL\n" +
            "  box #AZURE\n" +
            "    participant \"Order Service\" as OS\n" +
            "  end box\n" +
            "end box\n" +
            "\n" +
            "User -> OS : place order\n" +
            "OS --> User : ack\n" +
            "@enduml\n";

        Assert.Equal(expected, CreateRenderer().Render(diagram));
    }
}
