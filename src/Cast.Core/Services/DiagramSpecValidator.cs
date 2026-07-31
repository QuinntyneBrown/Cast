using System;
using System.Collections.Generic;
using System.Linq;
using Cast.Core.Diagnostics;
using Cast.Core.Models;

namespace Cast.Core.Services;

/// <summary>
/// Default <see cref="IDiagramSpecValidator"/>. Wraps the participant and message parsers with the
/// cross-cutting rules — unique aliases, known message endpoints, single-line title, single-token
/// theme — that parsing a spec in isolation cannot enforce.
/// </summary>
public sealed class DiagramSpecValidator : IDiagramSpecValidator
{
    private readonly IParticipantParser _participantParser;
    private readonly IMessageParser _messageParser;

    /// <summary>Initializes the validator with the parsers used for individual specifications.</summary>
    public DiagramSpecValidator(IParticipantParser participantParser, IMessageParser messageParser)
    {
        _participantParser = participantParser;
        _messageParser = messageParser;
    }

    /// <inheritdoc />
    public IReadOnlyList<Participant> ParseParticipants(IReadOnlyList<string> specs)
    {
        var participants = new List<Participant>(specs.Count);
        var seen = new HashSet<string>(StringComparer.Ordinal);

        foreach (string spec in specs)
        {
            Participant participant = _participantParser.Parse(spec);
            if (!seen.Add(participant.Alias))
            {
                throw new DiagramFormatException(
                    $"Duplicate participant alias '{participant.Alias}'. Each participant must have a unique alias.");
            }

            participants.Add(participant);
        }

        return participants;
    }

    /// <inheritdoc />
    public IReadOnlyList<Message> ParseMessages(IReadOnlyList<string> specs, IReadOnlyList<Participant> participants)
    {
        var aliases = participants.Select(p => p.Alias).ToHashSet(StringComparer.Ordinal);
        var messages = new List<Message>(specs.Count);

        foreach (string spec in specs)
        {
            Message message = _messageParser.Parse(spec);
            EnsureKnownEndpoint(message.Source, spec, aliases);
            EnsureKnownEndpoint(message.Target, spec, aliases);
            messages.Add(message);
        }

        return messages;
    }

    /// <inheritdoc />
    public void ValidateMetadata(string? title, string? theme)
    {
        if (!string.IsNullOrWhiteSpace(title) && ContainsControlChar(title))
        {
            throw new DiagramFormatException(
                "The title contains a control character (such as a line break). Use a single-line title.");
        }

        if (!string.IsNullOrWhiteSpace(theme))
        {
            string trimmed = theme.Trim();
            foreach (char c in trimmed)
            {
                if (char.IsWhiteSpace(c) || char.IsControl(c))
                {
                    throw new DiagramFormatException(
                        $"Theme '{trimmed}' must be a single token without whitespace; PlantUML '!theme' expects one name.");
                }
            }
        }
    }

    private static void EnsureKnownEndpoint(string alias, string spec, IReadOnlySet<string> aliases)
    {
        if (!aliases.Contains(alias))
        {
            string known = aliases.Count == 0 ? "(none)" : string.Join(", ", aliases.Order());
            throw new DiagramFormatException(
                $"Message '{spec}' refers to unknown participant '{alias}'. " +
                $"Declare it with --participant first. Known aliases: {known}.");
        }
    }

    private static bool ContainsControlChar(string value)
    {
        foreach (char c in value)
        {
            if (char.IsControl(c))
            {
                return true;
            }
        }

        return false;
    }
}
