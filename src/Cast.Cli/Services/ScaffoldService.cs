using System.Collections.Generic;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using Cast.Cli.Models;
using Cast.Core.Diagnostics;
using Cast.Core.Models;
using Cast.Core.Services;
using Microsoft.Extensions.Logging;

namespace Cast.Cli.Services;

/// <summary>
/// Default <see cref="IScaffoldService"/>. Coordinates the focused services — it validates and
/// parses the raw specs through <see cref="IDiagramSpecValidator"/>, optionally fills in a sample
/// flow, renders, writes, and opens the result in an editor when asked — but contains no parsing,
/// formatting, or I/O logic of its own. Failures are reported as a <see cref="ScaffoldStatus"/>;
/// cancellation propagates.
/// </summary>
public sealed class ScaffoldService : IScaffoldService
{
    private readonly IDiagramSpecValidator _validator;
    private readonly ISampleFlowGenerator _sampleFlowGenerator;
    private readonly ISequenceDiagramRenderer _renderer;
    private readonly IDiagramWriter _writer;
    private readonly IFileOpener _opener;
    private readonly ILogger<ScaffoldService> _logger;

    public ScaffoldService(
        IDiagramSpecValidator validator,
        ISampleFlowGenerator sampleFlowGenerator,
        ISequenceDiagramRenderer renderer,
        IDiagramWriter writer,
        IFileOpener opener,
        ILogger<ScaffoldService> logger)
    {
        _validator = validator;
        _sampleFlowGenerator = sampleFlowGenerator;
        _renderer = renderer;
        _writer = writer;
        _opener = opener;
        _logger = logger;
    }

    /// <inheritdoc />
    public async Task<ScaffoldStatus> ExecuteAsync(ScaffoldRequest request, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        try
        {
            IReadOnlyList<Participant> participants = _validator.ParseParticipants(request.Participants);
            IReadOnlyList<Message> messages = ResolveMessages(request, participants);
            _validator.ValidateMetadata(request.Title, request.Theme);

            var diagram = new SequenceDiagram(
                participants,
                messages,
                request.Title,
                request.AutoNumber,
                request.Theme,
                DiagramStyle.FromOptions(request.OuterBoxColor, request.InnerBoxColor));

            cancellationToken.ThrowIfCancellationRequested();

            string content = _renderer.Render(diagram);
            await _writer.WriteAsync(content, request.OutputPath, request.Force, cancellationToken).ConfigureAwait(false);

            if (!string.IsNullOrWhiteSpace(request.OutputPath))
            {
                _logger.LogInformation(
                    "Scaffolded sequence diagram with {ParticipantCount} participant(s) and {MessageCount} message(s) to {OutputPath}.",
                    participants.Count, messages.Count, request.OutputPath);

                if (request.OpenInEditor)
                {
                    _opener.Open(Path.GetFullPath(request.OutputPath));
                }
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

        return _validator.ParseMessages(request.Messages, participants);
    }
}
