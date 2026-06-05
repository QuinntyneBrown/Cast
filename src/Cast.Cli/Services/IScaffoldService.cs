using System.Threading;
using System.Threading.Tasks;
using Cast.Cli.Models;

namespace Cast.Cli.Services;

/// <summary>
/// The application-level use case: turn a <see cref="ScaffoldRequest"/> into a rendered
/// sequence diagram written to its destination. Owns the parse → validate → render → write
/// pipeline and reports the outcome as a <see cref="ScaffoldStatus"/> (the host maps that to an
/// exit code).
/// </summary>
public interface IScaffoldService
{
    /// <summary>Runs the scaffold pipeline and returns its domain outcome.</summary>
    Task<ScaffoldStatus> ExecuteAsync(ScaffoldRequest request, CancellationToken cancellationToken);
}
