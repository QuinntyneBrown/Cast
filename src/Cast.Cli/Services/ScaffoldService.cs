using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Cast.Cli.Diagnostics;
using Cast.Cli.Hosting;
using Cast.Cli.Models;
using Microsoft.Extensions.Logging;

namespace Cast.Cli.Services;

/// <summary>
/// Default <see cref="IScaffoldService"/>. Coordinates the focused services — it parses
/// participants and messages, optionally fills in a sample flow, validates cross-references,
/// renders, and writes — but contains no parsing, formatting, or I/O logic of its own.
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
    public async Task<int> ExecuteAsync(ScaffoldRequest request, CancellationToken cancellationToken)
    {
        try
        {
            IReadOnlyList<Participant> participants = ParseParticipants(request.Participants);
            IReadOnlyList<Message> messages = ResolveMessages(request, participants);

            var diagram = new SequenceDiagram(
                participants,
                messages,
                request.Title,
                request.AutoNumber,
                request.Theme);

            string content = _renderer.Render(diagram);
            await _writer.WriteAsync(content, request.OutputPath, request.Force, cancellationToken).ConfigureAwait(false);

            if (!string.IsNullOrWhiteSpace(request.OutputPath))
            {
                _logger.LogInformation(
                    "Scaffolded sequence diagram with {ParticipantCount} participant(s) and {MessageCount} message(s) to {OutputPath}.",
                    participants.Count, messages.Count, request.OutputPath);
            }

            return ExitCode.Success;
        }
        catch (DiagramFormatException ex)
        {
            _logger.LogError("{Message}", ex.Message);
            return ExitCode.UsageError;
        }
        catch (IOException ex)
        {
            _logger.LogError("{Message}", ex.Message);
            return ExitCode.IoError;
        }
    }

    private IReadOnlyList<Participant> ParseParticipants(IReadOnlyList<string> specs)
    {
        var participants = new List<Participant>(specs.Count);
        var seen = new HashSet<string>(System.StringComparer.Ordinal);

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

        var aliases = participants.Select(p => p.Alias).ToHashSet(System.StringComparer.Ordinal);
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
}
