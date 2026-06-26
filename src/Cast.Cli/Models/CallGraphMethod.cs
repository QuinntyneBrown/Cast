using System.Collections.Generic;

namespace Cast.Cli.Models;

/// <summary>
/// One method (or exported function) of the inspected subject, together with the outbound calls it
/// makes, in source order. The <c>calls</c> command renders one interaction per method.
/// </summary>
/// <param name="Name">The member's declared name.</param>
/// <param name="Signature">A display signature — the name plus its parameter names (e.g. <c>placeOrder(orderId)</c>).</param>
/// <param name="Visibility">The member's access level; the command diagrams public members by default.</param>
/// <param name="IsStatic"><see langword="true"/> for a <c>static</c> member.</param>
/// <param name="IsAsync"><see langword="true"/> for an <c>async</c> member.</param>
/// <param name="Calls">The outbound calls the member makes, de-duplicated, in the order they first appear in its body.</param>
public sealed record CallGraphMethod(
    string Name,
    string Signature,
    MethodVisibility Visibility,
    bool IsStatic,
    bool IsAsync,
    IReadOnlyList<MethodCall> Calls);
