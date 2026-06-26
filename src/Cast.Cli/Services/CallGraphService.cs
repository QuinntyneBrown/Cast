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
/// Default <see cref="ICallGraphService"/>. Coordinates the focused services — reader, parser,
/// renderer, writer — but contains no parsing, formatting, or I/O logic of its own. It owns one piece
/// of policy: selecting which members to diagram (an explicit <c>--method</c> list, shown regardless
/// of visibility; otherwise public members, or all members with <c>--include-private</c>). A failure
/// to read or parse the input is reported as <see cref="ScaffoldStatus.InvalidInput"/>; a failure to
/// write the output as <see cref="ScaffoldStatus.OutputError"/>; cancellation propagates.
/// </summary>
public sealed class CallGraphService : ICallGraphService
{
    private readonly ISourceFileReader _reader;
    private readonly ITypeScriptCallGraphParser _parser;
    private readonly ICallGraphRenderer _renderer;
    private readonly IDiagramWriter _writer;
    private readonly IFileOpener _opener;
    private readonly ILogger<CallGraphService> _logger;

    public CallGraphService(
        ISourceFileReader reader,
        ITypeScriptCallGraphParser parser,
        ICallGraphRenderer renderer,
        IDiagramWriter writer,
        IFileOpener opener,
        ILogger<CallGraphService> logger)
    {
        _reader = reader;
        _parser = parser;
        _renderer = renderer;
        _writer = writer;
        _opener = opener;
        _logger = logger;
    }

    /// <inheritdoc />
    public async Task<ScaffoldStatus> ExecuteAsync(CallGraphRequest request, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        string source;
        try
        {
            source = await _reader.ReadAsync(request.SourcePath, cancellationToken).ConfigureAwait(false);
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
            CallGraph graph = _parser.Parse(source, Path.GetFileName(request.SourcePath));
            CallGraph selected = Select(graph, request);
            content = _renderer.Render(selected, request.Title, style);

            _logger.LogInformation(
                "Parsed {SubjectName} with {MethodCount} method(s) from {SourcePath}.",
                selected.SubjectName, selected.Methods.Count, request.SourcePath);
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
            _logger.LogInformation("Wrote call-graph diagram to {OutputPath}.", request.OutputPath);

            if (request.OpenInEditor)
            {
                _opener.Open(Path.GetFullPath(request.OutputPath));
            }
        }

        return ScaffoldStatus.Success;
    }

    /// <summary>
    /// Narrows the parsed graph to the members the request asks for: an explicit <c>--method</c> list
    /// (matched case-insensitively, shown regardless of visibility) or, by default, the public members
    /// (all members with <c>--include-private</c>).
    /// </summary>
    /// <exception cref="DiagramFormatException">A requested method is absent, or no member qualifies.</exception>
    private static CallGraph Select(CallGraph graph, CallGraphRequest request)
    {
        if (request.Methods.Count > 0)
        {
            string? missing = request.Methods.FirstOrDefault(
                requested => !graph.Methods.Any(m => NameMatches(m.Name, requested)));
            if (missing is not null)
            {
                string available = graph.Methods.Count == 0
                    ? "the subject declares no methods"
                    : "available: " + string.Join(", ", graph.Methods.Select(m => m.Name));
                throw new DiagramFormatException($"No method named '{missing}' in {graph.SubjectName} ({available}).");
            }

            List<CallGraphMethod> chosen = graph.Methods
                .Where(m => request.Methods.Any(requested => NameMatches(m.Name, requested)))
                .ToList();
            return graph with { Methods = chosen };
        }

        List<CallGraphMethod> visible = request.IncludePrivate
            ? graph.Methods.ToList()
            : graph.Methods.Where(m => m.Visibility == MethodVisibility.Public).ToList();

        if (visible.Count == 0)
        {
            throw new DiagramFormatException(
                $"{graph.SubjectName} has no public methods to diagram. " +
                "Re-run with --include-private, or name one with --method.");
        }

        return graph with { Methods = visible };
    }

    private static bool NameMatches(string name, string requested) =>
        string.Equals(name, requested, StringComparison.OrdinalIgnoreCase);

    /// <summary>
    /// Rejects a title containing a control character (such as a line break) so the single free-text
    /// value the command accepts cannot inject extra lines into the line-oriented PlantUML output —
    /// mirroring the guard the other generating commands apply to their titles.
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
