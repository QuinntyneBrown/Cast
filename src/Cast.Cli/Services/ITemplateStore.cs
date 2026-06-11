using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Cast.Cli.Models;

namespace Cast.Cli.Services;

/// <summary>
/// The persistence boundary for named diagram templates — the single abstraction that knows
/// templates live as files. Filesystem failures surface as <see cref="System.IO.IOException"/>;
/// user-input problems (an invalid template name, a corrupt template file) surface as
/// <see cref="Diagnostics.DiagramFormatException"/>. Implementations do not log; the
/// orchestrator does.
/// </summary>
public interface ITemplateStore
{
    /// <summary>Creates or overwrites (upserts) the template stored under <see cref="DiagramTemplate.Name"/>.</summary>
    Task SaveAsync(DiagramTemplate template, CancellationToken cancellationToken);

    /// <summary>Loads the template named <paramref name="name"/>, or <see langword="null"/> when it does not exist.</summary>
    Task<DiagramTemplate?> FindAsync(string name, CancellationToken cancellationToken);

    /// <summary>Lists the stored template names, ordinal-sorted. Empty when none have been saved yet.</summary>
    Task<IReadOnlyList<string>> ListAsync(CancellationToken cancellationToken);

    /// <summary>Deletes the template named <paramref name="name"/>; <see langword="false"/> when it did not exist.</summary>
    Task<bool> DeleteAsync(string name, CancellationToken cancellationToken);
}
