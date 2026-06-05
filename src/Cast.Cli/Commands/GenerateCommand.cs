using System.CommandLine;
using Cast.Cli.Models;
using Cast.Cli.Services;

namespace Cast.Cli.Commands;

/// <summary>
/// The <c>generate</c> command (alias <c>gen</c>): the primary scaffolding entry point. It owns
/// only the mapping from command-line options to a <see cref="ScaffoldRequest"/>; all real work
/// is delegated to <see cref="IScaffoldService"/>.
/// </summary>
public sealed class GenerateCommand : ICliCommand
{
    private readonly IScaffoldService _scaffold;

    private readonly Option<string[]> _participants;
    private readonly Option<string[]> _messages;
    private readonly Option<string?> _title;
    private readonly Option<bool> _autoNumber;
    private readonly Option<string?> _theme;
    private readonly Option<string?> _output;
    private readonly Option<bool> _force;
    private readonly Option<bool> _noSample;

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
            Description = "Write the diagram to this file instead of standard output.",
            HelpName = "file",
        };

        _force = new Option<bool>("--force")
        {
            Description = "Overwrite the output file if it already exists.",
        };

        _noSample = new Option<bool>("--no-sample")
        {
            Description = "When no --message is supplied, do not generate a placeholder flow.",
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
        command.Options.Add(_force);
        command.Options.Add(_noSample);

        command.SetAction((parseResult, cancellationToken) =>
        {
            var request = new ScaffoldRequest(
                Participants: parseResult.GetValue(_participants) ?? [],
                Messages: parseResult.GetValue(_messages) ?? [],
                Title: parseResult.GetValue(_title),
                AutoNumber: parseResult.GetValue(_autoNumber),
                Theme: parseResult.GetValue(_theme),
                OutputPath: parseResult.GetValue(_output),
                Force: parseResult.GetValue(_force),
                IncludeSampleFlow: !parseResult.GetValue(_noSample));

            return _scaffold.ExecuteAsync(request, cancellationToken);
        });

        return command;
    }
}
