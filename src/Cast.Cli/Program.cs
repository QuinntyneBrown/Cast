using System.CommandLine;
using Cast.Cli.Hosting;
using Microsoft.Extensions.DependencyInjection;

// Composition root: build the container, resolve the root command, and invoke it.
var services = new ServiceCollection();
services.AddCast();

await using ServiceProvider provider = services.BuildServiceProvider();

RootCommand root = provider.GetRequiredService<RootCommandFactory>().Create();

return await root.Parse(args).InvokeAsync();
