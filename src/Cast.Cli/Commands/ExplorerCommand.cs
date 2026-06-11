using System.CommandLine;
using Cast.Cli.Hosting;
using Cast.Cli.Services;

namespace Cast.Cli.Commands;

/// <summary>
/// The <c>explorer</c> command: opens the folder where templates are stored
/// (<c>%APPDATA%\cast\templates</c>) in Visual Studio Code, so the hand-editable template JSON
/// files are one command away. Like every other command it only dispatches; all real work is
/// delegated to <see cref="IExplorerService"/>.
/// </summary>
public sealed class ExplorerCommand : ICliCommand
{
    private readonly IExplorerService _service;

    public ExplorerCommand(IExplorerService service) => _service = service;

    /// <inheritdoc />
    public Command Build()
    {
        var command = new Command("explorer", "Open the folder where templates are stored in Visual Studio Code.");

        command.SetAction(async (parseResult, cancellationToken) =>
        {
            ScaffoldStatus status = await _service.ExecuteAsync(cancellationToken).ConfigureAwait(false);

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
