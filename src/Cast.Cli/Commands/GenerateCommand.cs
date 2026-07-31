using System.CommandLine;
using Cast.Cli.Hosting;
using Cast.Cli.Models;
using Cast.Cli.Services;
using Cast.Core.Models;

namespace Cast.Cli.Commands;

/// <summary>
/// The <c>generate</c> command (alias <c>gen</c>): the primary scaffolding entry point. It owns
/// only the mapping from command-line options to a <see cref="ScaffoldRequest"/>; all real work
/// is delegated to <see cref="IScaffoldService"/>.
/// </summary>
public sealed class GenerateCommand : ICliCommand
{
    /// <summary>Where the diagram lands when neither <c>--output</c> nor <c>--stdout</c> is given.</summary>
    private const string DefaultOutputFileName = "cast.puml";

    private readonly IScaffoldService _scaffold;

    private readonly Option<string[]> _participants;
    private readonly Option<string[]> _messages;
    private readonly Option<string?> _title;
    private readonly Option<bool> _autoNumber;
    private readonly Option<string?> _theme;
    private readonly Option<string?> _output;
    private readonly Option<bool> _stdout;
    private readonly Option<bool> _force;
    private readonly Option<bool> _noSample;
    private readonly Option<string?> _outerBoxColor;
    private readonly Option<string?> _innerBoxColor;
    private readonly Option<bool> _noOpen;

    public GenerateCommand(IScaffoldService scaffold)
    {
        _scaffold = scaffold;

        // One value per flag (repeat the flag to add more). A display name or label
        // containing spaces must be quoted as a single argument; leaving it unquoted then
        // surfaces a clear "unrecognized argument" error instead of silently splitting it
        // into extra participants/messages.
        _participants = new Option<string[]>("--participant", "-p")
        {
            Description = "A participant as '[kind:]alias[:Display Name]'. Repeatable. Run 'cast kinds' to list kinds.",
            Required = true,
            Arity = ArgumentArity.OneOrMore,
            AllowMultipleArgumentsPerToken = false,
            HelpName = "spec",
        };

        _messages = new Option<string[]>("--message", "-m")
        {
            Description = "A message as 'Source -> Target : label'. Repeatable.",
            Arity = ArgumentArity.ZeroOrMore,
            AllowMultipleArgumentsPerToken = false,
            HelpName = "spec",
        };

        _title = new Option<string?>("--title", "-t")
        {
            Description = "Diagram title.",
            HelpName = "text",
        };

        _autoNumber = new Option<bool>("--autonumber")
        {
            Description = "Emit 'autonumber' so PlantUML numbers each message.",
        };

        _theme = new Option<string?>("--theme")
        {
            Description = "PlantUML theme name (emits '!theme <name>').",
            HelpName = "name",
        };

        _output = new Option<string?>("--output", "-o")
        {
            Description = "Write the diagram to this file (defaults to cast.puml in the current directory).",
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

        _noSample = new Option<bool>("--no-sample")
        {
            Description = "When no --message is supplied, do not generate a placeholder flow.",
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
        var command = new Command("generate", "Scaffold a PlantUML sequence diagram from participants and messages.");
        command.Aliases.Add("gen");

        command.Options.Add(_participants);
        command.Options.Add(_messages);
        command.Options.Add(_title);
        command.Options.Add(_autoNumber);
        command.Options.Add(_theme);
        command.Options.Add(_output);
        command.Options.Add(_stdout);
        command.Options.Add(_force);
        command.Options.Add(_noSample);
        command.Options.Add(_outerBoxColor);
        command.Options.Add(_innerBoxColor);
        command.Options.Add(_noOpen);

        command.SetAction(async (parseResult, cancellationToken) =>
        {
            // Default destination: cast.puml in the working directory, unless --stdout is
            // requested or an explicit --output path is given.
            string? outputPath = parseResult.GetValue(_stdout)
                ? null
                : parseResult.GetValue(_output) ?? DefaultOutputFileName;

            var request = new ScaffoldRequest(
                Participants: parseResult.GetValue(_participants) ?? [],
                Messages: parseResult.GetValue(_messages) ?? [],
                Title: parseResult.GetValue(_title),
                AutoNumber: parseResult.GetValue(_autoNumber),
                Theme: parseResult.GetValue(_theme),
                OutputPath: outputPath,
                Force: parseResult.GetValue(_force),
                IncludeSampleFlow: !parseResult.GetValue(_noSample),
                OuterBoxColor: parseResult.GetValue(_outerBoxColor),
                InnerBoxColor: parseResult.GetValue(_innerBoxColor),
                OpenInEditor: !parseResult.GetValue(_noOpen));

            ScaffoldStatus status = await _scaffold.ExecuteAsync(request, cancellationToken).ConfigureAwait(false);

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
