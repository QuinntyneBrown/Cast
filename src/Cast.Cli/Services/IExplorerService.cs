using System.Threading;
using System.Threading.Tasks;

namespace Cast.Cli.Services;

/// <summary>
/// Orchestrates the <c>explorer</c> command: opens the folder where templates are stored in
/// Visual Studio Code, creating the folder first when no template has been saved yet.
/// </summary>
public interface IExplorerService
{
    /// <summary>Opens the templates folder in the editor and reports the outcome.</summary>
    Task<ScaffoldStatus> ExecuteAsync(CancellationToken cancellationToken);
}
