namespace Cast.Cli.Models;

/// <summary>
/// The access level of a class member as written in TypeScript. A member is <see cref="Public"/>
/// unless it is marked <c>private</c>, <c>protected</c>, or uses the <c>#</c> hard-private prefix.
/// Drives which members the <c>calls</c> command diagrams by default (public only) versus with
/// <c>--include-private</c>. Exported module functions are always <see cref="Public"/>.
/// </summary>
public enum MethodVisibility
{
    /// <summary>Reachable from outside the class — the TypeScript default when no modifier is present.</summary>
    Public,

    /// <summary>Reachable from subclasses only (<c>protected</c>).</summary>
    Protected,

    /// <summary>Reachable from within the class only (<c>private</c> or a <c>#name</c> field).</summary>
    Private,
}
