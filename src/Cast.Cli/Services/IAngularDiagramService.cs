using System.Threading;
using System.Threading.Tasks;
using Cast.Cli.Models;

namespace Cast.Cli.Services;

/// <summary>
/// The application-level use case behind the <c>ng</c> command: read an Angular source file,
/// extract its dependency-injection graph, render the explanatory sequence diagram, and write it
/// to its destination. Reports the outcome as a <see cref="ScaffoldStatus"/> (the host maps that
/// to an exit code).
/// </summary>
public interface IAngularDiagramService
{
    /// <summary>Runs the read → parse → render → write pipeline and returns its domain outcome.</summary>
    Task<ScaffoldStatus> ExecuteAsync(AngularDiagramRequest request, CancellationToken cancellationToken);
}
