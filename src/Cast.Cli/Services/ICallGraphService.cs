using System.Threading;
using System.Threading.Tasks;
using Cast.Cli.Models;

namespace Cast.Cli.Services;

/// <summary>
/// The application-level use case behind the <c>calls</c> command: read a TypeScript source file,
/// extract its call graph, filter to the requested members, render the sequence diagram, and write
/// it to its destination. Reports the outcome as a <see cref="ScaffoldStatus"/> (the host maps that
/// to an exit code).
/// </summary>
public interface ICallGraphService
{
    /// <summary>Runs the read → parse → filter → render → write pipeline and returns its domain outcome.</summary>
    Task<ScaffoldStatus> ExecuteAsync(CallGraphRequest request, CancellationToken cancellationToken);
}
