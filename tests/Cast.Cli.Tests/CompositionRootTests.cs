using System.CommandLine;
using System.Linq;
using Cast.Cli.Hosting;
using Cast.Cli.Services;
using Microsoft.Extensions.DependencyInjection;

namespace Cast.Cli.Tests;

public sealed class CompositionRootTests
{
    [Fact]
    public void AddCast_ResolvesServiceGraph()
    {
        using ServiceProvider provider = new ServiceCollection().AddCast().BuildServiceProvider();

        // Resolving the orchestrators forces the whole graph (parsers, renderer, writer) to resolve.
        Assert.NotNull(provider.GetRequiredService<IScaffoldService>());
        Assert.NotNull(provider.GetRequiredService<IAngularDiagramService>());
        Assert.NotNull(provider.GetRequiredService<RootCommandFactory>());
    }

    [Fact]
    public void RootCommandFactory_AttachesAllSubcommands()
    {
        using ServiceProvider provider = new ServiceCollection().AddCast().BuildServiceProvider();

        RootCommand root = provider.GetRequiredService<RootCommandFactory>().Create();
        var names = root.Subcommands.Select(c => c.Name).ToHashSet();

        Assert.Contains("generate", names);
        Assert.Contains("kinds", names);
        Assert.Contains("ng", names);
    }
}
