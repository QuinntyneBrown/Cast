using System.Collections.Generic;
using System.IO;
using System.Text.Encodings.Web;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Cast.Cli.Models;
using Cast.Core.Diagnostics;
using Cast.Core.Models;
using Cast.Core.Services;
using Microsoft.Extensions.Logging;

namespace Cast.Cli.Services;

/// <summary>
/// Default <see cref="ITemplateService"/>. Coordinates the store, the spec validator, and the
/// scaffolding pipeline — saving validates everything a template will need at render time so a
/// bad template can never be persisted, and rendering merges the stored definition with the
/// render-time overrides into a plain <see cref="ScaffoldRequest"/> handled by
/// <see cref="IScaffoldService"/>. List/show payloads go through <see cref="IDiagramWriter"/>
/// (the stdout boundary); logs go to stderr.
/// </summary>
public sealed class TemplateService : ITemplateService
{
    /// <summary>Canonical display formatting for <c>show</c> — matches the on-disk shape.</summary>
    private static readonly JsonSerializerOptions DisplayJsonOptions = new()
    {
        WriteIndented = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        // Console output for humans: print the '>' in message specs as-is, not as '>'.
        Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
    };

    private readonly ITemplateStore _store;
    private readonly IScaffoldService _scaffold;
    private readonly IDiagramSpecValidator _validator;
    private readonly IDiagramWriter _writer;
    private readonly ILogger<TemplateService> _logger;

    public TemplateService(
        ITemplateStore store,
        IScaffoldService scaffold,
        IDiagramSpecValidator validator,
        IDiagramWriter writer,
        ILogger<TemplateService> logger)
    {
        _store = store;
        _scaffold = scaffold;
        _validator = validator;
        _writer = writer;
        _logger = logger;
    }

    /// <inheritdoc />
    public async Task<ScaffoldStatus> SaveAsync(DiagramTemplate template, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        try
        {
            if (template.Participants.Count == 0)
            {
                throw new DiagramFormatException(
                    "A template needs at least one participant. Add one with --participant.");
            }

            IReadOnlyList<Participant> participants = _validator.ParseParticipants(template.Participants);
            _validator.ParseMessages(template.Messages, participants);
            _validator.ValidateMetadata(template.Title, template.Theme);
            DiagramStyle.FromOptions(template.OuterBoxColor, template.InnerBoxColor);

            await _store.SaveAsync(template, cancellationToken).ConfigureAwait(false);

            _logger.LogInformation(
                "Saved template '{Name}' with {ParticipantCount} participant(s) and {MessageCount} default message(s).",
                template.Name, template.Participants.Count, template.Messages.Count);

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

    /// <inheritdoc />
    public async Task<ScaffoldStatus> RenderAsync(RenderTemplateRequest request, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        DiagramTemplate? template;
        try
        {
            template = await _store.FindAsync(request.Name, cancellationToken).ConfigureAwait(false);
        }
        catch (DiagramFormatException ex)
        {
            _logger.LogError("{Message}", ex.Message);
            return ScaffoldStatus.InvalidInput;
        }
        catch (IOException ex)
        {
            // A template that cannot be read is a usage problem, not an output problem.
            _logger.LogError("{Message}", ex.Message);
            return ScaffoldStatus.InvalidInput;
        }

        if (template is null)
        {
            LogNotFound(request.Name);
            return ScaffoldStatus.InvalidInput;
        }

        // Render-time values win: messages replace the stored ones entirely, nullable fields
        // fall back to the stored values, and the autonumber flag can only switch numbering on.
        var scaffoldRequest = new ScaffoldRequest(
            Participants: template.Participants,
            Messages: request.Messages.Count > 0 ? request.Messages : template.Messages,
            Title: request.Title ?? template.Title,
            AutoNumber: request.AutoNumber || template.AutoNumber,
            Theme: request.Theme ?? template.Theme,
            OutputPath: request.OutputPath,
            Force: request.Force,
            IncludeSampleFlow: request.IncludeSampleFlow,
            OuterBoxColor: request.OuterBoxColor ?? template.OuterBoxColor,
            InnerBoxColor: request.InnerBoxColor ?? template.InnerBoxColor,
            OpenInEditor: request.OpenInEditor);

        return await _scaffold.ExecuteAsync(scaffoldRequest, cancellationToken).ConfigureAwait(false);
    }

    /// <inheritdoc />
    public async Task<ScaffoldStatus> ListAsync(CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        try
        {
            IReadOnlyList<string> names = await _store.ListAsync(cancellationToken).ConfigureAwait(false);
            if (names.Count == 0)
            {
                _logger.LogInformation("No templates saved yet. Create one with 'cast template save'.");
                return ScaffoldStatus.Success;
            }

            string text = string.Join('\n', names) + "\n";
            await _writer.WriteAsync(text, outputPath: null, overwrite: false, cancellationToken).ConfigureAwait(false);
            return ScaffoldStatus.Success;
        }
        catch (IOException ex)
        {
            _logger.LogError("{Message}", ex.Message);
            return ScaffoldStatus.OutputError;
        }
    }

    /// <inheritdoc />
    public async Task<ScaffoldStatus> ShowAsync(string name, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        try
        {
            DiagramTemplate? template = await _store.FindAsync(name, cancellationToken).ConfigureAwait(false);
            if (template is null)
            {
                LogNotFound(name);
                return ScaffoldStatus.InvalidInput;
            }

            // Re-serializing (rather than echoing the file) yields canonical output and proves
            // the stored definition still parses.
            string json = JsonSerializer.Serialize(template, DisplayJsonOptions) + "\n";
            await _writer.WriteAsync(json, outputPath: null, overwrite: false, cancellationToken).ConfigureAwait(false);
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
            return ScaffoldStatus.InvalidInput;
        }
    }

    /// <inheritdoc />
    public async Task<ScaffoldStatus> DeleteAsync(string name, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        try
        {
            bool deleted = await _store.DeleteAsync(name, cancellationToken).ConfigureAwait(false);
            if (!deleted)
            {
                LogNotFound(name);
                return ScaffoldStatus.InvalidInput;
            }

            _logger.LogInformation("Deleted template '{Name}'.", name);
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

    private void LogNotFound(string name) =>
        _logger.LogError(
            "Template '{Name}' was not found. Run 'cast template list' to see the saved templates.", name);
}
