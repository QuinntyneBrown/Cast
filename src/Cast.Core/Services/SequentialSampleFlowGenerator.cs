using System.Collections.Generic;
using Cast.Core.Models;

namespace Cast.Core.Services;

/// <summary>
/// Default <see cref="ISampleFlowGenerator"/>. Models a simple call/return chain: a solid
/// request arrow from each participant to the next, followed by dashed response arrows
/// unwinding back to the first. A lone participant gets a single self-message. Every label is
/// a <c>TODO</c> placeholder inviting the author to fill in real interactions.
/// </summary>
public sealed class SequentialSampleFlowGenerator : ISampleFlowGenerator
{
    private const string RequestArrow = "->";
    private const string ResponseArrow = "-->";
    private const string RequestLabel = "TODO: describe request";
    private const string ResponseLabel = "TODO: describe response";

    /// <inheritdoc />
    public IReadOnlyList<Message> Generate(IReadOnlyList<Participant> participants)
    {
        if (participants.Count == 0)
        {
            return [];
        }

        if (participants.Count == 1)
        {
            string only = participants[0].Alias;
            return [new Message(only, only, RequestArrow, "TODO: describe step")];
        }

        var messages = new List<Message>();

        // Forward request chain: p0 -> p1 -> ... -> p(n-1).
        for (int i = 0; i < participants.Count - 1; i++)
        {
            messages.Add(new Message(participants[i].Alias, participants[i + 1].Alias, RequestArrow, RequestLabel));
        }

        // Response chain unwinding back to the first participant.
        for (int i = participants.Count - 1; i > 0; i--)
        {
            messages.Add(new Message(participants[i].Alias, participants[i - 1].Alias, ResponseArrow, ResponseLabel));
        }

        return messages;
    }
}
