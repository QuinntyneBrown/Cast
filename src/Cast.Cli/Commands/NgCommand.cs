using System.CommandLine;
using Cast.Cli.Hosting;
using Cast.Cli.Models;
using Cast.Cli.Services;

namespace Cast.Cli.Commands;

/// <summary>
/// The <c>ng</c> command: inspects an Angular <c>.ts</c> file and emits a PlantUML sequence
/// diagram explaining how Angular's injector supplies that consumer's dependencies. Like every
/// other command it owns only the mapping from command-line options to an
/// <see cref="AngularDiagramRequest"/>; all real work is delegated to
/// <see cref="IAngularDiagramService"/>.
/// </summary>
public sealed class NgCommand : ICliCommand
{
    private readonly IAngularDiagramService _service;

    private readonly Option<string> _servicePath;
    private readonly Option<string?> _title;
    private readonly Option<string?> _output;
    private readonly Option<bool> _force;

    public NgCommand(IAngularDiagramService service)
    {
        _service = service;

        _servicePath = new Option<string>("--service", "-s")
        {
            Description = "Path to the Angular .ts file to inspect (a service, interceptor, guard, resolver, …).",
            Required = true,
            HelpName = "file",
        };

        _title = new Option<string?>("--title", "-t")
        {
            Description = "Diagram title (defaults to a generated 'How Angular injects … into <Name>').",
            HelpName = "text",
        };

        _output = new Option<string?>("--output", "-o")
        {
            Description = "Write the diagram to this file instead of standard output.",
            HelpName = "file",
        };

        _force = new Option<bool>("--force")
        {
            Description = "Overwrite the output file if it already exists.",
        };
    }

    /// <inheritdoc />
    public Command Build()
    {
        var command = new Command("ng", "Generate a PlantUML diagram of how Angular injects dependencies into a service.");
        command.Aliases.Add("angular");

        command.Options.Add(_servicePath);
        command.Options.Add(_title);
        command.Options.Add(_output);
        command.Options.Add(_force);

        command.SetAction(async (parseResult, cancellationToken) =>
        {
            var request = new AngularDiagramRequest(
                ServicePath: parseResult.GetValue(_servicePath) ?? string.Empty,
                Title: parseResult.GetValue(_title),
                OutputPath: parseResult.GetValue(_output),
                Force: parseResult.GetValue(_force));

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
