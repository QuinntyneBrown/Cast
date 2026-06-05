using System;
using System.CommandLine;
using System.IO;
using Cast.Cli.Services;

namespace Cast.Cli.Commands;

/// <summary>
/// The <c>kinds</c> command (alias <c>list-kinds</c>): prints the participant kinds accepted as
/// the optional <c>kind:</c> prefix in a participant spec. A second command beyond
/// <see cref="GenerateCommand"/> demonstrates the one-command-per-file, DI-discovered pattern.
/// </summary>
public sealed class ListKindsCommand : ICliCommand
{
    private readonly IParticipantKindCatalog _catalog;

    public ListKindsCommand(IParticipantKindCatalog catalog) => _catalog = catalog;

    /// <inheritdoc />
    public Command Build()
    {
        var command = new Command("kinds", "List the participant kinds usable as the 'kind:' prefix in --participant.");
        command.Aliases.Add("list-kinds");

        command.SetAction(parseResult =>
        {
            TextWriter output = parseResult.InvocationConfiguration?.Output ?? Console.Out;

            output.WriteLine("Participant kinds (use as the optional 'kind:' prefix in --participant):");
            output.WriteLine();
            foreach (var (_, keyword) in _catalog.Kinds)
            {
                output.WriteLine($"  {keyword}");
            }

            output.WriteLine();
            output.WriteLine("Examples:");
            output.WriteLine("  --participant User                       (defaults to 'participant')");
            output.WriteLine("  --participant actor:Customer");
            output.WriteLine("  --participant \"database:DB:Main Database\"");
        });

        return command;
    }
}
