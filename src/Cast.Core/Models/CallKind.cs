namespace Cast.Core.Models;

/// <summary>
/// Classifies one outbound call a method makes, by who receives it. Drives how the call is drawn:
/// a <see cref="Self"/> call loops back to the subject's own lifeline, a <see cref="Collaborator"/>
/// call goes to another lifeline, and a <see cref="Construction"/> draws a "«create»" arrow to the
/// constructed type.
/// </summary>
public enum CallKind
{
    /// <summary>A call to another member of the same subject (<c>this.foo()</c>, <c>super.foo()</c>, or a bare call to a sibling method).</summary>
    Self,

    /// <summary>A call to a method on a collaborator — an injected field, a constructor parameter, a local, or a named class.</summary>
    Collaborator,

    /// <summary>A <c>new T(...)</c> construction.</summary>
    Construction,
}
