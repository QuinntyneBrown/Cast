using System;
using System.CommandLine;
using Cast.Cli.Hosting;
using Cast.Cli.Models;
using Cast.Cli.Services;

namespace Cast.Cli.Commands;

/// <summary>
/// The <c>calls</c> command (alias <c>callgraph</c>): inspects a TypeScript <c>.ts</c> file and emits
/// a PlantUML sequence diagram of the call graph — for each public method (or exported function) it
/// shows the calls that member makes to itself and its collaborators. Like every other command it
/// owns only the mapping from command-line options to a <see cref="CallGraphRequest"/>; all real work
/// is delegated to <see cref="ICallGraphService"/>.
/// </summary>
public sealed class CallGraphCommand : ICliCommand
{
    private readonly ICallGraphService _service;

    private readonly Option<string> _sourcePath;
    private readonly Option<string[]> _methods;
    private readonly Option<bool> _includePrivate;
    private readonly Option<string?> _title;
    private readonly Option<string?> _output;
    private readonly Option<bool> _stdout;
    private readonly Option<bool> _force;
    private readonly Option<string?> _outerBoxColor;
    private readonly Option<string?> _innerBoxColor;
    private readonly Option<bool> _noOpen;

    public CallGraphCommand(ICallGraphService service)
    {
        _service = service;

        _sourcePath = new Option<string>("--source", "-s", "--file")
        {
            Description = "Path to the TypeScript .ts file to inspect.",
            Required = true,
            HelpName = "file",
        };

        _methods = new Option<string[]>("--method")
        {
            Description = "Diagram only this method/function (matched by name, any visibility). Repeatable.",
            Arity = ArgumentArity.ZeroOrMore,
            AllowMultipleArgumentsPerToken = false,
            HelpName = "name",
        };

        _includePrivate = new Option<bool>("--include-private")
        {
            Description = "Include non-public members too (default: public members only).",
        };

        _title = new Option<string?>("--title", "-t")
        {
            Description = "Diagram title (defaults to a generated 'Call graph of <Name>').",
            HelpName = "text",
        };

        _output = new Option<string?>("--output", "-o")
        {
            Description = "Write the diagram to this file (defaults to the source path with a .puml extension, e.g. order.service.ts -> order.service.puml).",
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
        var command = new Command("calls", "Diagram the call graph of each public method/function in a TypeScript file.");
        command.Aliases.Add("callgraph");

        command.Options.Add(_sourcePath);
        command.Options.Add(_methods);
        command.Options.Add(_includePrivate);
        command.Options.Add(_title);
        command.Options.Add(_output);
        command.Options.Add(_stdout);
        command.Options.Add(_force);
        command.Options.Add(_outerBoxColor);
        command.Options.Add(_innerBoxColor);
        command.Options.Add(_noOpen);

        command.SetAction(async (parseResult, cancellationToken) =>
        {
            string sourcePath = parseResult.GetValue(_sourcePath) ?? string.Empty;

            // Default destination: a .puml file beside the source (foo.service.ts -> foo.service.puml),
            // unless --stdout is requested or an explicit --output path is given.
            string? outputPath = parseResult.GetValue(_stdout)
                ? null
                : parseResult.GetValue(_output) ?? DeriveDefaultOutputPath(sourcePath);

            var request = new CallGraphRequest(
                SourcePath: sourcePath,
                Title: parseResult.GetValue(_title),
                OutputPath: outputPath,
                Force: parseResult.GetValue(_force),
                Methods: parseResult.GetValue(_methods) ?? [],
                IncludePrivate: parseResult.GetValue(_includePrivate),
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
    /// <c>.puml</c> (so <c>order.service.ts</c> becomes <c>order.service.puml</c>); a path that does
    /// not end in <c>.ts</c> simply gains a <c>.puml</c> suffix.
    /// </summary>
    private static string DeriveDefaultOutputPath(string sourcePath) =>
        sourcePath.EndsWith(".ts", StringComparison.OrdinalIgnoreCase)
            ? string.Concat(sourcePath.AsSpan(0, sourcePath.Length - 3), ".puml")
            : sourcePath + ".puml";
}
