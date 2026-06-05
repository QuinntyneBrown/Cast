using System.Collections.Generic;
using System.Text;
using Cast.Cli.Models;

namespace Cast.Cli.Services;

/// <summary>
/// Renders a <see cref="SequenceDiagram"/> as PlantUML (<c>@startuml … @enduml</c>) source.
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
        var lines = new List<string> { "@startuml", "' Scaffolded by cast" };

        if (!string.IsNullOrWhiteSpace(diagram.Theme))
        {
            lines.Add($"!theme {diagram.Theme.Trim()}");
        }

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
            foreach (Participant participant in diagram.Participants)
            {
                lines.Add(RenderParticipant(participant));
            }
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

    private string RenderParticipant(Participant participant)
    {
        string keyword = _kinds.KeywordFor(participant.Kind);
        return participant.DisplayName is null
            ? $"{keyword} {participant.Alias}"
            : $"{keyword} \"{participant.DisplayName}\" as {participant.Alias}";
    }

    private static string RenderMessage(Message message)
    {
        string connection = $"{message.Source} {message.Arrow} {message.Target}";
        return message.Label is null ? connection : $"{connection} : {message.Label}";
    }
}
