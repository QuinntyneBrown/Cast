using System.Collections.Generic;
using System.Linq;
using Cast.Cli.Models;

namespace Cast.Cli.Services;

/// <summary>
/// Renders a <see cref="SequenceDiagram"/> as PlantUML (<c>@startuml … @enduml</c>) source.
/// Every diagram runs the teoz engine (<c>!pragma teoz true</c>) with a default font size of 10,
/// colors every lifeline <see cref="DiagramStyle.ParticipantColor"/>, and wraps the non-actor
/// participants in a double nested box (outer/inner colors from <see cref="DiagramStyle"/>).
/// Actors always stay outside the boxes, so they are declared first — the box must be one
/// contiguous block.
/// Output uses <c>\n</c> line endings for deterministic, platform-independent results.
/// </summary>
public sealed class PlantUmlSequenceRenderer : ISequenceDiagramRenderer
{
    private const string Newline = "\n";

    private readonly IParticipantKindCatalog _kinds;

    public PlantUmlSequenceRenderer(IParticipantKindCatalog kinds) => _kinds = kinds;

    /// <inheritdoc />
    public string Render(SequenceDiagram diagram)
    {
        DiagramStyle style = diagram.Style ?? DiagramStyle.Default;
        var lines = new List<string> { "@startuml", "' Scaffolded by cast", "!pragma teoz true" };

        if (!string.IsNullOrWhiteSpace(diagram.Theme))
        {
            lines.Add($"!theme {diagram.Theme.Trim()}");
        }

        // After the theme so the mandated font size wins even when the theme sets its own.
        lines.Add("skinparam defaultFontSize 10");

        if (!string.IsNullOrWhiteSpace(diagram.Title))
        {
            lines.Add($"title {diagram.Title.Trim()}");
        }

        if (diagram.AutoNumber)
        {
            lines.Add("autonumber");
        }

        if (diagram.Participants.Count > 0)
        {
            lines.Add(string.Empty);
            RenderParticipants(lines, diagram.Participants, style);
        }

        if (diagram.Messages.Count > 0)
        {
            lines.Add(string.Empty);
            foreach (Message message in diagram.Messages)
            {
                lines.Add(RenderMessage(message));
            }
        }

        lines.Add("@enduml");

        // Trailing newline so the file ends cleanly / concatenates well.
        return string.Join(Newline, lines) + Newline;
    }

    private void RenderParticipants(List<string> lines, IReadOnlyList<Participant> participants, DiagramStyle style)
    {
        foreach (Participant actor in participants.Where(p => p.Kind == ParticipantKind.Actor))
        {
            lines.Add(RenderParticipant(actor));
        }

        List<Participant> boxed = participants.Where(p => p.Kind != ParticipantKind.Actor).ToList();
        if (boxed.Count == 0)
        {
            return;
        }

        lines.Add($"box {style.OuterBoxColor}");
        lines.Add($"  box {style.InnerBoxColor}");
        foreach (Participant participant in boxed)
        {
            lines.Add($"    {RenderParticipant(participant)}");
        }

        lines.Add("  end box");
        lines.Add("end box");
    }

    private string RenderParticipant(Participant participant)
    {
        string keyword = _kinds.KeywordFor(participant.Kind);
        string declaration = participant.DisplayName is null
            ? $"{keyword} {participant.Alias}"
            : $"{keyword} \"{participant.DisplayName}\" as {participant.Alias}";
        return $"{declaration} {DiagramStyle.ParticipantColor}";
    }

    private static string RenderMessage(Message message)
    {
        string connection = $"{message.Source} {message.Arrow} {message.Target}";
        return message.Label is null ? connection : $"{connection} : {message.Label}";
    }
}
