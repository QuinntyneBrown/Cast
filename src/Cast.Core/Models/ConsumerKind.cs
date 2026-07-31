namespace Cast.Core.Models;

/// <summary>
/// The kind of Angular construct that receives the injected dependencies. Used only to choose
/// friendly wording (titles, the caller lifeline, the "ask" message) in the generated diagram —
/// the dependency-resolution story is the same for all kinds.
/// </summary>
public enum ConsumerKind
{
    /// <summary>An <c>@Injectable</c> class (the default; covers most <c>*Service</c> classes).</summary>
    Service,

    /// <summary>A signal/state <c>*Store</c> class.</summary>
    Store,

    /// <summary>An <c>@Component</c> class.</summary>
    Component,

    /// <summary>An <c>@Directive</c> class.</summary>
    Directive,

    /// <summary>An <c>@Pipe</c> class.</summary>
    Pipe,

    /// <summary>A functional or class HTTP interceptor (<c>HttpInterceptorFn</c>).</summary>
    Interceptor,

    /// <summary>A functional or class route guard (<c>CanActivateFn</c> and friends).</summary>
    Guard,

    /// <summary>A functional or class route data resolver (<c>ResolveFn</c>).</summary>
    Resolver,

    /// <summary>Any other class or injectable function not matched above.</summary>
    Class,
}
