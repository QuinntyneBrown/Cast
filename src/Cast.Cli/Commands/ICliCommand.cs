using System.CommandLine;

namespace Cast.Cli.Commands;

/// <summary>
/// A self-contained CLI command. Each implementation lives in its own file (one command per
/// file), declares its own options, and wires its own action to the services it needs via
/// constructor injection. The composition root discovers every registered
/// <see cref="ICliCommand"/> and attaches it to the root command, so adding a command is purely
/// additive — no central switchboard to edit.
/// </summary>
public interface ICliCommand
{
    /// <summary>Builds the fully-configured <see cref="Command"/> this module represents.</summary>
    Command Build();
}
