using System.Collections.Generic;
using System.Linq;
using Cast.Core.Models;
using Cast.Core.Services;

namespace Cast.Core.Tests;

public sealed class SequentialSampleFlowGeneratorTests
{
    private static readonly SequentialSampleFlowGenerator Generator = new();

    private static Participant P(string alias) => new(alias, ParticipantKind.Participant);

    [Fact]
    public void Generate_NoParticipants_ReturnsEmpty()
    {
        Assert.Empty(Generator.Generate([]));
    }

    [Fact]
    public void Generate_SingleParticipant_ProducesSelfMessage()
    {
        IReadOnlyList<Message> messages = Generator.Generate([P("A")]);

        Message only = Assert.Single(messages);
        Assert.Equal("A", only.Source);
        Assert.Equal("A", only.Target);
    }

    [Fact]
    public void Generate_ThreeParticipants_ProducesCallReturnChain()
    {
        IReadOnlyList<Message> messages = Generator.Generate([P("A"), P("B"), P("C")]);

        // 2 forward requests + 2 unwinding responses.
        Assert.Equal(4, messages.Count);

        Assert.Equal(("A", "B", "->"), (messages[0].Source, messages[0].Target, messages[0].Arrow));
        Assert.Equal(("B", "C", "->"), (messages[1].Source, messages[1].Target, messages[1].Arrow));
        Assert.Equal(("C", "B", "-->"), (messages[2].Source, messages[2].Target, messages[2].Arrow));
        Assert.Equal(("B", "A", "-->"), (messages[3].Source, messages[3].Target, messages[3].Arrow));

        Assert.All(messages, m => Assert.False(string.IsNullOrWhiteSpace(m.Label)));
    }
}
