using System.IO;
using System.Threading;
using System.Threading.Tasks;
using Cast.Cli.Diagnostics;
using Cast.Cli.Models;
using Microsoft.Extensions.Logging;

namespace Cast.Cli.Services;

/// <summary>
/// Default <see cref="IAngularDiagramService"/>. Coordinates the focused services — reader, parser,
/// renderer, writer — but contains no parsing, formatting, or I/O logic of its own. A failure to
/// read or parse the input is reported as <see cref="ScaffoldStatus.InvalidInput"/>; a failure to
/// write the output as <see cref="ScaffoldStatus.OutputError"/>; cancellation propagates.
/// </summary>
public sealed class AngularDiagramService : IAngularDiagramService
{
    private readonly ISourceFileReader _reader;
    private readonly IAngularServiceParser _parser;
    private readonly IAngularDiagramRenderer _renderer;
    private readonly IDiagramWriter _writer;
    private readonly IFileOpener _opener;
    private readonly ILogger<AngularDiagramService> _logger;

    public AngularDiagramService(
        ISourceFileReader reader,
        IAngularServiceParser parser,
        IAngularDiagramRenderer renderer,
        IDiagramWriter writer,
        IFileOpener opener,
        ILogger<AngularDiagramService> logger)
    {
        _reader = reader;
        _parser = parser;
        _renderer = renderer;
        _writer = writer;
        _opener = opener;
        _logger = logger;
    }

    /// <inheritdoc />
    public async Task<ScaffoldStatus> ExecuteAsync(AngularDiagramRequest request, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        string source;
        try
        {
            source = await _reader.ReadAsync(request.ServicePath, cancellationToken).ConfigureAwait(false);
        }
        catch (IOException ex)
        {
            // A missing or unreadable input file is a usage problem, not an output problem.
            _logger.LogError("{Message}", ex.Message);
            return ScaffoldStatus.InvalidInput;
        }

        string content;
        try
        {
            ValidateTitle(request.Title);
            DiagramStyle style = DiagramStyle.FromOptions(request.OuterBoxColor, request.InnerBoxColor);
            AngularService service = _parser.Parse(source, Path.GetFileName(request.ServicePath));
            content = _renderer.Render(service, request.Title, style);

            _logger.LogInformation(
                "Parsed {ConsumerName} with {DependencyCount} injected dependenc(ies) from {ServicePath}.",
                service.Name, service.Dependencies.Count, request.ServicePath);
        }
        catch (DiagramFormatException ex)
        {
            _logger.LogError("{Message}", ex.Message);
            return ScaffoldStatus.InvalidInput;
        }

        cancellationToken.ThrowIfCancellationRequested();

        try
        {
            await _writer.WriteAsync(content, request.OutputPath, request.Force, cancellationToken).ConfigureAwait(false);
        }
        catch (IOException ex)
        {
            _logger.LogError("{Message}", ex.Message);
            return ScaffoldStatus.OutputError;
        }

        if (!string.IsNullOrWhiteSpace(request.OutputPath))
        {
            _logger.LogInformation("Wrote Angular DI diagram to {OutputPath}.", request.OutputPath);

            if (request.OpenInEditor)
            {
                _opener.Open(Path.GetFullPath(request.OutputPath));
            }
        }

        return ScaffoldStatus.Success;
    }

    /// <summary>
    /// Rejects a title containing a control character (such as a line break) so the single free-text
    /// value the command accepts cannot inject extra lines into the line-oriented PlantUML output —
    /// mirroring the guard the <c>generate</c> command applies to its title.
    /// </summary>
    private static void ValidateTitle(string? title)
    {
        if (string.IsNullOrWhiteSpace(title))
        {
            return;
        }

        foreach (char c in title)
        {
            if (char.IsControl(c))
            {
                throw new DiagramFormatException(
                    "The title contains a control character (such as a line break). Use a single-line title.");
            }
        }
    }
}
