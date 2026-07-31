namespace Cast.Core.Models;

/// <summary>
/// A single outbound call discovered inside a method body: who receives it and what is invoked.
/// </summary>
/// <param name="ReceiverKey">
/// Stable identity of the receiving lifeline, used to merge repeated calls to the same receiver
/// into one participant. Receivers are keyed by their resolved type when known (so two fields of
/// the same type share a lifeline), else by the written identifier; a self call carries the
/// subject's own name. Never rendered directly.
/// </param>
/// <param name="ReceiverLabel">Human-friendly label for the receiving lifeline (e.g. <c>HttpClient</c>).</param>
/// <param name="Kind">How the call is drawn (self / collaborator / construction).</param>
/// <param name="Method">
/// The invoked method name (e.g. <c>save</c>). Empty for a <see cref="CallKind.Construction"/>,
/// which is rendered as a "«create»" arrow instead.
/// </param>
public sealed record MethodCall(string ReceiverKey, string ReceiverLabel, CallKind Kind, string Method);
