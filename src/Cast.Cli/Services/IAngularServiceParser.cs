using Cast.Cli.Models;

namespace Cast.Cli.Services;

/// <summary>
/// Inspects the source of a single Angular <c>.ts</c> file and extracts the consumer it declares
/// together with the dependencies Angular injects into it.
/// </summary>
/// <remarks>
/// Recognises the two idiomatic Angular dependency-injection shapes:
/// <list type="bullet">
///   <item>the <c>inject(X)</c> function, used in class field initialisers and in functional
///   providers (interceptors, guards, resolvers);</item>
///   <item>classic constructor injection, including <c>@Inject(TOKEN)</c> and <c>@Optional()</c>.</item>
/// </list>
/// Parsing is deliberately lightweight (a comment- and string-aware scan rather than a full
/// TypeScript parse), which keeps <c>cast</c> a self-contained .NET tool with no Node runtime.
/// </remarks>
public interface IAngularServiceParser
{
    /// <summary>
    /// Parses <paramref name="source"/> into an <see cref="AngularService"/>.
    /// </summary>
    /// <param name="source">The full text of the <c>.ts</c> file.</param>
    /// <param name="fileName">Optional file name, used only to enrich error messages.</param>
    /// <exception cref="Diagnostics.DiagramFormatException">
    /// No Angular service, component, or functional provider could be found in <paramref name="source"/>.
    /// </exception>
    AngularService Parse(string source, string? fileName = null);
}
