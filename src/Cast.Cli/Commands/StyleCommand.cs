using System.CommandLine;
using Cast.Cli.Hosting;
using Cast.Cli.Models;
using Cast.Cli.Services;

namespace Cast.Cli.Commands;

/// <summary>
/// The <c>style</c> command: applies the cast house style — <c>!pragma teoz true</c>,
/// <c>skinparam defaultFontSize 10</c>, and the double nested participant box — to existing
/// PlantUML sequence diagrams that are missing it. Accepts a single <c>.puml</c> file or a
/// folder (scanned recursively); non-sequence diagrams and already-styled files are left
/// untouched, so the command is safe to re-run. Like every other command it owns only the
/// mapping from command-line input to a <see cref="StyleRequest"/>; all real work is delegated
/// to <see cref="IStyleService"/>.
/// </summary>
public sealed class StyleCommand : ICliCommand
{
    private readonly IStyleService _service;

    private readonly Argument<string> _path;
    private readonly Option<string?> _outerBoxColor;
    private readonly Option<string?> _innerBoxColor;

    public StyleCommand(IStyleService service)
    {
        _service = service;

        _path = new Argument<string>("path")
        {
            Description = "Path to a .puml file, or a folder to scan recursively for .puml files.",
        };

        _outerBoxColor = new Option<string?>("--outer-box-color")
        {
            Description = $"Color of the outer box wrapping the non-actor participants (defaults to {DiagramStyle.DefaultOuterBoxColor}).",
            HelpName = "color",
        };

        _innerBoxColor = new Option<string?>("--inner-box-color")
        {
            Description = $"Color of the inner box wrapping the non-actor participants (defaults to {DiagramStyle.DefaultInnerBoxColor}).",
            HelpName = "color",
        };
    }

    /// <inheritdoc />
    public Command Build()
    {
        var command = new Command(
            "style",
            "Apply the default styling (teoz pragma, font size 10, double participant box) to existing PlantUML sequence diagrams.");

        command.Arguments.Add(_path);
        command.Options.Add(_outerBoxColor);
        command.Options.Add(_innerBoxColor);

        command.SetAction(async (parseResult, cancellationToken) =>
        {
            var request = new StyleRequest(
                Path: parseResult.GetValue(_path) ?? string.Empty,
                OuterBoxColor: parseResult.GetValue(_outerBoxColor),
                InnerBoxColor: parseResult.GetValue(_innerBoxColor));

            ScaffoldStatus status = await _service.ExecuteAsync(request, cancellationToken).ConfigureAwait(false);

            return status switch
            {
                ScaffoldStatus.Success => ExitCode.Success,
                ScaffoldStatus.OutputError => ExitCode.IoError,
                _ => ExitCode.UsageError,
            };
        });

        return command;
    }
}
