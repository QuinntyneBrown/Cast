using System.Collections.Generic;
using System.CommandLine;
using Cast.Cli.Commands;

namespace Cast.Cli.Hosting;

/// <summary>
/// Assembles the <see cref="RootCommand"/> from every registered <see cref="ICliCommand"/>.
/// The factory is oblivious to which concrete commands exist — it just attaches whatever the
/// container provides — so the command set is configured entirely through DI registration.
/// </summary>
public sealed class RootCommandFactory
{
    private const string Description =
        "cast — scaffold initial PlantUML sequence diagrams from the command line.";

    private readonly IEnumerable<ICliCommand> _commands;

    public RootCommandFactory(IEnumerable<ICliCommand> commands) => _commands = commands;

    /// <summary>Builds the root command with all subcommands attached.</summary>
    public RootCommand Create()
    {
        var root = new RootCommand(Description);
        foreach (ICliCommand command in _commands)
        {
            root.Subcommands.Add(command.Build());
        }

        return root;
    }
}
