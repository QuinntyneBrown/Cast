using System;
using System.Linq;
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
        string messageLine = Assert.Single(lines.Where(l => l.StartsWith("A -> B")));

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
}
