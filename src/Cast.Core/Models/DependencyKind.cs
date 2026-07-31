namespace Cast.Core.Models;

/// <summary>
/// Classifies an injected dependency by how Angular resolves it. The distinction drives the
/// wording in the generated diagram (a class is <em>constructed</em>; a token's <em>value</em> is
/// looked up).
/// </summary>
public enum DependencyKind
{
    /// <summary>A class the injector instantiates (e.g. <c>HttpClient</c>, <c>Router</c>).</summary>
    Service,

    /// <summary>An <c>InjectionToken</c> or other non-class key the injector resolves to a value (e.g. <c>API_BASE_URL</c>).</summary>
    Token,
}
