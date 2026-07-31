using System.CommandLine;
using Cast.Cli.Hosting;
using Cast.Cli.Models;
using Cast.Cli.Services;
using Cast.Core.Models;

namespace Cast.Cli.Commands;

/// <summary>
/// The <c>template</c> command family (alias <c>tpl</c>): named diagram templates with full CRUD.
/// The parent command's own action renders a saved template
/// (<c>cast template --name acme-ordering [-m ...]</c>) through the same scaffolding pipeline as
/// <c>generate</c>, with render-time options overriding the stored values; the <c>save</c>,
/// <c>list</c>, <c>show</c> and <c>delete</c> subcommands manage the stored definitions. Like
/// every other command it owns only the mapping from command-line input to request DTOs; all real
/// work is delegated to <see cref="ITemplateService"/>.
/// </summary>
public sealed class TemplateCommand : ICliCommand
{
    /// <summary>Where the rendered diagram lands when neither <c>--output</c> nor <c>--stdout</c> is given.</summary>
    private const string DefaultOutputFileName = "cast.puml";

    private readonly ITemplateService _service;

    // Render options, owned by the parent command. Each subcommand constructs its own Option
    // instances inside its builder — System.CommandLine options must not be shared across
    // commands. The parent's --name is deliberately not Required: enforcing it there would
    // interfere with subcommand invocations, so the render action checks it itself.
    private readonly Option<string?> _name;
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

    public TemplateCommand(ITemplateService service)
    {
        _service = service;

        _name = new Option<string?>("--name", "-n")
        {
            Description = "Name of the saved template to render.",
            HelpName = "name",
        };

        _messages = new Option<string[]>("--message", "-m")
        {
            Description = "A message as 'Source -> Target : label', replacing the template's stored messages. Repeatable.",
            Arity = ArgumentArity.ZeroOrMore,
            AllowMultipleArgumentsPerToken = false,
            HelpName = "spec",
        };

        _title = new Option<string?>("--title", "-t")
        {
            Description = "Diagram title overriding the template's stored title.",
            HelpName = "text",
        };

        _autoNumber = new Option<bool>("--autonumber")
        {
            Description = "Emit 'autonumber' even when the template doesn't (the flag can only switch numbering on).",
        };

        _theme = new Option<string?>("--theme")
        {
            Description = "PlantUML theme name overriding the template's stored theme.",
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
            Description = "When the template stores no messages and no --message is supplied, do not generate a placeholder flow.",
        };

        _outerBoxColor = new Option<string?>("--outer-box-color")
        {
            Description = $"Color of the outer box wrapping the non-actor participants (defaults to the template's stored color, then {DiagramStyle.DefaultOuterBoxColor}).",
            HelpName = "color",
        };

        _innerBoxColor = new Option<string?>("--inner-box-color")
        {
            Description = $"Color of the inner box wrapping the non-actor participants (defaults to the template's stored color, then {DiagramStyle.DefaultInnerBoxColor}).",
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
        var command = new Command(
            "template",
            "Render a saved diagram template, or manage templates with save/list/show/delete.");
        command.Aliases.Add("tpl");

        command.Options.Add(_name);
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
            string? name = parseResult.GetValue(_name);
            if (string.IsNullOrWhiteSpace(name))
            {
                parseResult.InvocationConfiguration.Error.WriteLine(
                    "Specify --name <template> to render, or run 'cast template list' to see the saved templates.");
                return ExitCode.UsageError;
            }

            // Default destination: cast.puml in the working directory, unless --stdout is
            // requested or an explicit --output path is given (mirrors `generate`).
            string? outputPath = parseResult.GetValue(_stdout)
                ? null
                : parseResult.GetValue(_output) ?? DefaultOutputFileName;

            var request = new RenderTemplateRequest(
                Name: name,
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

            ScaffoldStatus status = await _service.RenderAsync(request, cancellationToken).ConfigureAwait(false);
            return ToExitCode(status);
        });

        command.Subcommands.Add(BuildSaveCommand());
        command.Subcommands.Add(BuildListCommand());
        command.Subcommands.Add(BuildShowCommand());
        command.Subcommands.Add(BuildDeleteCommand());

        return command;
    }

    private Command BuildSaveCommand()
    {
        var name = new Option<string>("--name", "-n")
        {
            Description = "Name of the template to create or update.",
            Required = true,
            HelpName = "name",
        };

        var participants = new Option<string[]>("--participant", "-p")
        {
            Description = "A participant as '[kind:]alias[:Display Name]'. Repeatable. Run 'cast kinds' to list kinds.",
            Required = true,
            Arity = ArgumentArity.OneOrMore,
            AllowMultipleArgumentsPerToken = false,
            HelpName = "spec",
        };

        var messages = new Option<string[]>("--message", "-m")
        {
            Description = "A default message as 'Source -> Target : label'. Repeatable.",
            Arity = ArgumentArity.ZeroOrMore,
            AllowMultipleArgumentsPerToken = false,
            HelpName = "spec",
        };

        var title = new Option<string?>("--title", "-t")
        {
            Description = "Default diagram title.",
            HelpName = "text",
        };

        var autoNumber = new Option<bool>("--autonumber")
        {
            Description = "Emit 'autonumber' by default when rendering this template.",
        };

        var theme = new Option<string?>("--theme")
        {
            Description = "Default PlantUML theme name (emits '!theme <name>').",
            HelpName = "name",
        };

        var outerBoxColor = new Option<string?>("--outer-box-color")
        {
            Description = $"Default color of the outer box wrapping the non-actor participants (defaults to {DiagramStyle.DefaultOuterBoxColor}).",
            HelpName = "color",
        };

        var innerBoxColor = new Option<string?>("--inner-box-color")
        {
            Description = $"Default color of the inner box wrapping the non-actor participants (defaults to {DiagramStyle.DefaultInnerBoxColor}).",
            HelpName = "color",
        };

        var command = new Command("save", "Create or update (upsert) a named template from participants and default messages.");
        command.Options.Add(name);
        command.Options.Add(participants);
        command.Options.Add(messages);
        command.Options.Add(title);
        command.Options.Add(autoNumber);
        command.Options.Add(theme);
        command.Options.Add(outerBoxColor);
        command.Options.Add(innerBoxColor);

        command.SetAction(async (parseResult, cancellationToken) =>
        {
            var template = new DiagramTemplate
            {
                Name = parseResult.GetValue(name) ?? string.Empty,
                Participants = parseResult.GetValue(participants) ?? [],
                Messages = parseResult.GetValue(messages) ?? [],
                Title = parseResult.GetValue(title),
                AutoNumber = parseResult.GetValue(autoNumber),
                Theme = parseResult.GetValue(theme),
                OuterBoxColor = parseResult.GetValue(outerBoxColor),
                InnerBoxColor = parseResult.GetValue(innerBoxColor),
            };

            ScaffoldStatus status = await _service.SaveAsync(template, cancellationToken).ConfigureAwait(false);
            return ToExitCode(status);
        });

        return command;
    }

    private Command BuildListCommand()
    {
        var command = new Command("list", "List the saved template names.");

        command.SetAction(async (parseResult, cancellationToken) =>
        {
            ScaffoldStatus status = await _service.ListAsync(cancellationToken).ConfigureAwait(false);
            return ToExitCode(status);
        });

        return command;
    }

    private Command BuildShowCommand()
    {
        var name = new Option<string>("--name", "-n")
        {
            Description = "Name of the template to show.",
            Required = true,
            HelpName = "name",
        };

        var command = new Command("show", "Print a saved template's stored definition.");
        command.Options.Add(name);

        command.SetAction(async (parseResult, cancellationToken) =>
        {
            ScaffoldStatus status = await _service
                .ShowAsync(parseResult.GetValue(name) ?? string.Empty, cancellationToken)
                .ConfigureAwait(false);
            return ToExitCode(status);
        });

        return command;
    }

    private Command BuildDeleteCommand()
    {
        var name = new Option<string>("--name", "-n")
        {
            Description = "Name of the template to delete.",
            Required = true,
            HelpName = "name",
        };

        var command = new Command("delete", "Delete a saved template.");
        command.Options.Add(name);

        command.SetAction(async (parseResult, cancellationToken) =>
        {
            ScaffoldStatus status = await _service
                .DeleteAsync(parseResult.GetValue(name) ?? string.Empty, cancellationToken)
                .ConfigureAwait(false);
            return ToExitCode(status);
        });

        return command;
    }

    private static int ToExitCode(ScaffoldStatus status) => status switch
    {
        ScaffoldStatus.Success => ExitCode.Success,
        ScaffoldStatus.OutputError => ExitCode.IoError,
        _ => ExitCode.UsageError,
    };
}
