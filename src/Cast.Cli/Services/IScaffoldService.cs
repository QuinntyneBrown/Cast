using System.Threading;
using System.Threading.Tasks;
using Cast.Cli.Models;

namespace Cast.Cli.Services;

/// <summary>
/// The application-level use case: turn a <see cref="ScaffoldRequest"/> into a rendered
/// sequence diagram written to its destination. Owns the parse → validate → render → write
/// pipeline and translates failures into process exit codes.
/// </summary>
public interface IScaffoldService
{
    /// <summary>Runs the scaffold pipeline and returns a process exit code (see <see cref="Hosting.ExitCode"/>).</summary>
    Task<int> ExecuteAsync(ScaffoldRequest request, CancellationToken cancellationToken);
}
