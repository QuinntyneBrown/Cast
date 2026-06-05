using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Cast.Cli.Diagnostics;
using Cast.Cli.Models;
using Microsoft.Extensions.Logging;

namespace Cast.Cli.Services;

/// <summary>
/// Default <see cref="IScaffoldService"/>. Coordinates the focused services — it parses
/// participants and messages, optionally fills in a sample flow, validates cross-references and
/// metadata, renders, and writes — but contains no parsing, formatting, or I/O logic of its own.
/// Failures are reported as a <see cref="ScaffoldStatus"/>; cancellation propagates.
/// </summary>
public sealed class ScaffoldService : IScaffoldService
{
    private readonly IParticipantParser _participantParser;
    private readonly IMessageParser _messageParser;
    private readonly ISampleFlowGenerator _sampleFlowGenerator;
    private readonly ISequenceDiagramRenderer _renderer;
    private readonly IDiagramWriter _writer;
    private readonly ILogger<ScaffoldService> _logger;

    public ScaffoldService(
        IParticipantParser participantParser,
        IMessageParser messageParser,
        ISampleFlowGenerator sampleFlowGenerator,
        ISequenceDiagramRenderer renderer,
        IDiagramWriter writer,
        ILogger<ScaffoldService> logger)
    {
        _participantParser = participantParser;
        _messageParser = messageParser;
        _sampleFlowGenerator = sampleFlowGenerator;
        _renderer = renderer;
        _writer = writer;
        _logger = logger;
    }

    /// <inheritdoc />
    public async Task<ScaffoldStatus> ExecuteAsync(ScaffoldRequest request, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        try
        {
            IReadOnlyList<Participant> participants = ParseParticipants(request.Participants);
            IReadOnlyList<Message> messages = ResolveMessages(request, participants);
            ValidateMetadata(request);

            var diagram = new SequenceDiagram(
                participants,
                messages,
                request.Title,
                request.AutoNumber,
                request.Theme);

            cancellationToken.ThrowIfCancellationRequested();

            string content = _renderer.Render(diagram);
            await _writer.WriteAsync(content, request.OutputPath, request.Force, cancellationToken).ConfigureAwait(false);

            if (!string.IsNullOrWhiteSpace(request.OutputPath))
            {
                _logger.LogInformation(
                    "Scaffolded sequence diagram with {ParticipantCount} participant(s) and {MessageCount} message(s) to {OutputPath}.",
                    participants.Count, messages.Count, request.OutputPath);
            }

            return ScaffoldStatus.Success;
        }
        catch (DiagramFormatException ex)
        {
            _logger.LogError("{Message}", ex.Message);
            return ScaffoldStatus.InvalidInput;
        }
        catch (IOException ex)
        {
            _logger.LogError("{Message}", ex.Message);
            return ScaffoldStatus.OutputError;
        }
    }

    private IReadOnlyList<Participant> ParseParticipants(IReadOnlyList<string> specs)
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

    /// <summary>
    /// Parses the supplied messages, or generates a sample flow when none were given and the
    /// request opts in. Every message endpoint is validated against the declared participants.
    /// </summary>
    private IReadOnlyList<Message> ResolveMessages(ScaffoldRequest request, IReadOnlyList<Participant> participants)
    {
        if (request.Messages.Count == 0)
        {
            return request.IncludeSampleFlow
                ? _sampleFlowGenerator.Generate(participants)
                : [];
        }

        if (!request.IncludeSampleFlow)
        {
            _logger.LogInformation("--no-sample has no effect because one or more --message values were supplied.");
        }

        var aliases = participants.Select(p => p.Alias).ToHashSet(StringComparer.Ordinal);
        var messages = new List<Message>(request.Messages.Count);

        foreach (string spec in request.Messages)
        {
            Message message = _messageParser.Parse(spec);
            EnsureKnownEndpoint(message.Source, spec, aliases);
            EnsureKnownEndpoint(message.Target, spec, aliases);
            messages.Add(message);
        }

        return messages;
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

    /// <summary>Validates the free-text title and theme so they cannot break the line-oriented output.</summary>
    private static void ValidateMetadata(ScaffoldRequest request)
    {
        if (!string.IsNullOrWhiteSpace(request.Title) && ContainsControlChar(request.Title))
        {
            throw new DiagramFormatException(
                "The title contains a control character (such as a line break). Use a single-line title.");
        }

        if (!string.IsNullOrWhiteSpace(request.Theme))
        {
            string theme = request.Theme.Trim();
            foreach (char c in theme)
            {
                if (char.IsWhiteSpace(c) || char.IsControl(c))
                {
                    throw new DiagramFormatException(
                        $"Theme '{theme}' must be a single token without whitespace; PlantUML '!theme' expects one name.");
                }
            }
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
