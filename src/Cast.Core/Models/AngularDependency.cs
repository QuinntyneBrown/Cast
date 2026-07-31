namespace Cast.Core.Models;

/// <summary>
/// A single dependency Angular injects into a consumer, as discovered from its source: the name
/// of the injected symbol (a class or token), how it is resolved, and whether it was marked
/// optional.
/// </summary>
/// <param name="Name">The injected symbol exactly as written in the source (e.g. <c>HttpClient</c>, <c>API_BASE_URL</c>).</param>
/// <param name="Kind">Whether the symbol is a class the injector builds or a token it looks up.</param>
/// <param name="Optional">
/// <see langword="true"/> when the dependency was requested with <c>@Optional()</c> or
/// <c>inject(X, { optional: true })</c>, meaning Angular supplies <c>null</c> if it is unavailable.
/// </param>
public sealed record AngularDependency(string Name, DependencyKind Kind, bool Optional = false);
