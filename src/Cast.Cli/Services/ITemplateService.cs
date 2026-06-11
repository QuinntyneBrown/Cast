using System.Threading;
using System.Threading.Tasks;
using Cast.Cli.Models;

namespace Cast.Cli.Services;

/// <summary>
/// Orchestrates the <c>template</c> command's use cases: persisting named diagram definitions and
/// rendering them through the regular scaffolding pipeline. Every method reports its outcome as a
/// <see cref="ScaffoldStatus"/> so the command layer maps results to exit codes uniformly.
/// </summary>
public interface ITemplateService
{
    /// <summary>Validates and creates-or-overwrites (upserts) <paramref name="template"/>. A template that fails validation is never persisted.</summary>
    Task<ScaffoldStatus> SaveAsync(DiagramTemplate template, CancellationToken cancellationToken);

    /// <summary>Loads the named template, merges the render-time overrides, and scaffolds the diagram.</summary>
    Task<ScaffoldStatus> RenderAsync(RenderTemplateRequest request, CancellationToken cancellationToken);

    /// <summary>Writes the stored template names to standard output, one per line.</summary>
    Task<ScaffoldStatus> ListAsync(CancellationToken cancellationToken);

    /// <summary>Writes the named template's stored definition (canonical JSON) to standard output.</summary>
    Task<ScaffoldStatus> ShowAsync(string name, CancellationToken cancellationToken);

    /// <summary>Deletes the named template.</summary>
    Task<ScaffoldStatus> DeleteAsync(string name, CancellationToken cancellationToken);
}
