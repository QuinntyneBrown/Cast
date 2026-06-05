using System.CommandLine;
using System.IO;
using System.Threading.Tasks;
using Cast.Cli.Commands;
using Cast.Cli.Services;

namespace Cast.Cli.Tests;

public sealed class ListKindsCommandTests
{
    [Fact]
    public void Build_HasListKindsAlias()
    {
        Command command = new ListKindsCommand(new ParticipantKindCatalog()).Build();

        Assert.Contains("list-kinds", command.Aliases);
    }

    [Fact]
    public async Task Invoke_PrintsEveryKindKeyword()
    {
        var catalog = new ParticipantKindCatalog();
        Command command = new ListKindsCommand(catalog).Build();
        var output = new StringWriter();
        var config = new InvocationConfiguration { Output = output, Error = new StringWriter() };

        int exit = await command.Parse([]).InvokeAsync(config);

        Assert.Equal(0, exit);
        string text = output.ToString();
        foreach (var (_, keyword) in catalog.Kinds)
        {
            Assert.Contains(keyword, text);
        }
    }
}
