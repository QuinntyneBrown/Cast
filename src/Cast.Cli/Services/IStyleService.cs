using System.Threading;
using System.Threading.Tasks;
using Cast.Cli.Models;

namespace Cast.Cli.Services;

/// <summary>
/// Orchestrates the <c>style</c> command: locate the <c>.puml</c> files designated by the
/// request, and rewrite every sequence diagram among them that is missing part of the house
/// style. Implementations report the outcome as a <see cref="ScaffoldStatus"/> rather than
/// throwing, mirroring <see cref="IScaffoldService"/>.
/// </summary>
public interface IStyleService
{
    /// <summary>Executes the request; returns the domain outcome (never throws for expected failures).</summary>
    Task<ScaffoldStatus> ExecuteAsync(StyleRequest request, CancellationToken cancellationToken);
}
