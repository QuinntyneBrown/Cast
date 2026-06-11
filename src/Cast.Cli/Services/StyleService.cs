using System.Collections.Generic;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using Cast.Cli.Diagnostics;
using Cast.Cli.Models;
using Microsoft.Extensions.Logging;

namespace Cast.Cli.Services;

/// <summary>
/// Default <see cref="IStyleService"/>. Coordinates the focused services — locator, editor,
/// detector, styler — but contains no discovery, classification, formatting, or I/O logic of
/// its own. Each file is processed independently: one unreadable or unwritable file is logged
/// and the run continues, so a single bad file never blocks restyling a whole tree. Files are
/// rewritten in place (preserving their encoding), and only when something actually changed.
/// When failures occurred, a write failure outranks a read failure in the reported status
/// because it may have left a file half-written.
/// </summary>
public sealed class StyleService : IStyleService
{
    private readonly IPumlFileLocator _locator;
    private readonly ITextFileEditor _editor;
    private readonly ISequenceDiagramDetector _detector;
    private readonly ISequenceDiagramStyler _styler;
    private readonly ILogger<StyleService> _logger;

    public StyleService(
        IPumlFileLocator locator,
        ITextFileEditor editor,
        ISequenceDiagramDetector detector,
        ISequenceDiagramStyler styler,
        ILogger<StyleService> logger)
    {
        _locator = locator;
        _editor = editor;
        _detector = detector;
        _styler = styler;
        _logger = logger;
    }

    /// <inheritdoc />
    public async Task<ScaffoldStatus> ExecuteAsync(StyleRequest request, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        DiagramStyle style;
        IReadOnlyList<string> files;
        try
        {
            style = DiagramStyle.FromOptions(request.OuterBoxColor, request.InnerBoxColor);
            files = _locator.Locate(request.Path);
        }
        catch (DiagramFormatException ex)
        {
            _logger.LogError("{Message}", ex.Message);
            return ScaffoldStatus.InvalidInput;
        }

        if (files.Count == 0)
        {
            _logger.LogInformation("No .puml files found under {Path}.", request.Path);
            return ScaffoldStatus.Success;
        }

        int updated = 0;
        int alreadyStyled = 0;
        int skipped = 0;
        int notSequence = 0;
        int readErrors = 0;
        int writeErrors = 0;

        foreach (string path in files)
        {
            cancellationToken.ThrowIfCancellationRequested();

            TextFile file;
            try
            {
                file = await _editor.ReadAsync(path, cancellationToken).ConfigureAwait(false);
            }
            catch (IOException ex)
            {
                _logger.LogError("{Message}", ex.Message);
                readErrors++;
                continue;
            }

            if (!_detector.IsSequenceDiagram(file.Content))
            {
                _logger.LogInformation("Skipped {File}: not a PlantUML sequence diagram.", path);
                notSequence++;
                continue;
            }

            StylerResult result = _styler.Apply(file.Content, style);
            if (result.SkipReason is not null)
            {
                _logger.LogInformation("Skipped {File}: {Reason}.", path, result.SkipReason);
                skipped++;
                continue;
            }

            if (!result.Changed)
            {
                _logger.LogInformation("Unchanged {File}: already styled.", path);
                alreadyStyled++;
                continue;
            }

            try
            {
                await _editor.WriteAsync(file with { Content = result.Content }, cancellationToken).ConfigureAwait(false);
            }
            catch (IOException ex)
            {
                _logger.LogError("{Message}", ex.Message);
                writeErrors++;
                continue;
            }

            _logger.LogInformation("Updated {File}: added {Changes}.", path, Describe(result));
            updated++;
        }

        _logger.LogInformation(
            "Styled {Updated} file(s); {AlreadyStyled} already styled; {Skipped} skipped; {NotSequence} not sequence diagrams; {Errors} failed; {Total} scanned.",
            updated, alreadyStyled, skipped, notSequence, readErrors + writeErrors, files.Count);

        if (writeErrors > 0)
        {
            return ScaffoldStatus.OutputError;
        }

        return readErrors > 0 ? ScaffoldStatus.InvalidInput : ScaffoldStatus.Success;
    }

    private static string Describe(StylerResult result)
    {
        var changes = new List<string>(4);
        if (result.AppliedTeozPragma)
        {
            changes.Add("teoz pragma");
        }

        if (result.AppliedFontSize)
        {
            changes.Add("font size");
        }

        if (result.AppliedParticipantColors)
        {
            changes.Add("participant colors");
        }

        if (result.AppliedBoxes)
        {
            changes.Add("participant boxes");
        }

        return string.Join(", ", changes);
    }
}
