using System.Collections.Generic;

namespace Cast.Cli.Models;

/// <summary>
/// The render-ready model of one Angular consumer and the dependencies Angular injects into it,
/// as extracted from a single <c>.ts</c> source file. This is the hand-off type between the
/// parsing stage (<see cref="Services.IAngularServiceParser"/>) and the diagram renderer
/// (<see cref="Services.IAngularDiagramRenderer"/>).
/// </summary>
/// <param name="Name">The consumer's declared name (class name or exported const name).</param>
/// <param name="Kind">The consumer kind, used only to pick friendly diagram wording.</param>
/// <param name="IsFunctional">
/// <see langword="true"/> for a functional provider (an exported arrow function such as an
/// interceptor or guard), which <em>pulls</em> its dependencies via <c>inject()</c> while it runs;
/// <see langword="false"/> for a class, which Angular <em>constructs</em> with its dependencies.
/// </param>
/// <param name="IsSingleton">
/// <see langword="true"/> when the consumer is provided as an application-wide singleton
/// (<c>@Injectable({ providedIn: 'root' | 'platform' })</c>).
/// </param>
/// <param name="ProvidedIn">The <c>providedIn</c> value when present (e.g. <c>root</c>), otherwise <see langword="null"/>.</param>
/// <param name="Dependencies">The injected dependencies, in the order they were discovered.</param>
public sealed record AngularService(
    string Name,
    ConsumerKind Kind,
    bool IsFunctional,
    bool IsSingleton,
    string? ProvidedIn,
    IReadOnlyList<AngularDependency> Dependencies);
