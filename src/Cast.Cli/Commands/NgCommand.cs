using System;
using System.CommandLine;
using Cast.Cli.Hosting;
using Cast.Cli.Models;
using Cast.Cli.Services;
using Cast.Core.Models;

namespace Cast.Cli.Commands;

/// <summary>
/// The <c>ng</c> command: inspects an Angular <c>.ts</c> file and emits a PlantUML sequence diagram
/// explaining how Angular's injector supplies that consumer's dependencies. The consumer can be any
/// construct that uses <c>inject(...)</c> — a service, component, directive, pipe, interceptor,
/// guard, resolver, or an exported function. Like every other command it owns only the mapping from
/// command-line options to an <see cref="AngularDiagramRequest"/>; all real work is delegated to
/// <see cref="IAngularDiagramService"/>.
/// </summary>
public sealed class NgCommand : ICliCommand
{
    private readonly IAngularDiagramService _service;

    private readonly Option<string> _servicePath;
    private readonly Option<string?> _title;
    private readonly Option<string?> _output;
    private readonly Option<bool> _stdout;
    private readonly Option<bool> _force;
    private readonly Option<string?> _outerBoxColor;
    private readonly Option<string?> _innerBoxColor;
    private readonly Option<bool> _noOpen;

    public NgCommand(IAngularDiagramService service)
    {
        _service = service;

        _servicePath = new Option<string>("--service", "-s", "--file")
        {
            Description = "Path to the Angular .ts file to inspect (service, component, directive, pipe, interceptor, guard, resolver, or function).",
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
            Description = "Write the diagram to this file (defaults to the source path with a .puml extension, e.g. foo.service.ts -> foo.service.puml).",
            HelpName = "file",
        };

        _stdout = new Option<bool>("--stdout")
        {
            Description = "Write the diagram to standard output instead of a file (takes precedence over --output).",
        };

        _force = new Option<bool>("--force")
        {
            Description = "Overwrite the output file if it already exists.",
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

        _noOpen = new Option<bool>("--no-open")
        {
            Description = "Do not open the written file in Notepad.",
        };
    }

    /// <inheritdoc />
    public Command Build()
    {
        var command = new Command("ng", "Diagram how Angular injects dependencies into any inject()-using construct (service, component, function, …).");
        command.Aliases.Add("angular");

        command.Options.Add(_servicePath);
        command.Options.Add(_title);
        command.Options.Add(_output);
        command.Options.Add(_stdout);
        command.Options.Add(_force);
        command.Options.Add(_outerBoxColor);
        command.Options.Add(_innerBoxColor);
        command.Options.Add(_noOpen);

        command.SetAction(async (parseResult, cancellationToken) =>
        {
            string servicePath = parseResult.GetValue(_servicePath) ?? string.Empty;

            // Default destination: a .puml file beside the source (foo.service.ts -> foo.service.puml),
            // unless --stdout is requested or an explicit --output path is given.
            string? outputPath = parseResult.GetValue(_stdout)
                ? null
                : parseResult.GetValue(_output) ?? DeriveDefaultOutputPath(servicePath);

            var request = new AngularDiagramRequest(
                ServicePath: servicePath,
                Title: parseResult.GetValue(_title),
                OutputPath: outputPath,
                Force: parseResult.GetValue(_force),
                OuterBoxColor: parseResult.GetValue(_outerBoxColor),
                InnerBoxColor: parseResult.GetValue(_innerBoxColor),
                OpenInEditor: !parseResult.GetValue(_noOpen));

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

    /// <summary>
    /// Derives the default output path from the source path by replacing a trailing <c>.ts</c> with
    /// <c>.puml</c> (so <c>foo.service.ts</c> becomes <c>foo.service.puml</c>); a path that does not end
    /// in <c>.ts</c> simply gains a <c>.puml</c> suffix.
    /// </summary>
    private static string DeriveDefaultOutputPath(string servicePath) =>
        servicePath.EndsWith(".ts", StringComparison.OrdinalIgnoreCase)
            ? string.Concat(servicePath.AsSpan(0, servicePath.Length - 3), ".puml")
            : servicePath + ".puml";
}
