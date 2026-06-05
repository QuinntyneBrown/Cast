using System.CommandLine;
using System.Threading;
using System.Threading.Tasks;
using Cast.Cli.Commands;
using Cast.Cli.Models;
using Cast.Cli.Services;

namespace Cast.Cli.Tests;

public sealed class NgCommandTests
{
    private sealed class CapturingAngularDiagramService : IAngularDiagramService
    {
        public AngularDiagramRequest? Captured { get; private set; }
        public ScaffoldStatus Result { get; set; } = ScaffoldStatus.Success;

        public Task<ScaffoldStatus> ExecuteAsync(AngularDiagramRequest request, CancellationToken cancellationToken)
        {
            Captured = request;
            return Task.FromResult(Result);
        }
    }

    private static async Task<int> Invoke(IAngularDiagramService service, params string[] args)
    {
        Command command = new NgCommand(service).Build();
        var config = new InvocationConfiguration { Output = new System.IO.StringWriter(), Error = new System.IO.StringWriter() };
        return await command.Parse(args).InvokeAsync(config);
    }

    [Fact]
    public async Task Build_HasAngularAlias()
    {
        Command command = new NgCommand(new CapturingAngularDiagramService()).Build();
        Assert.Contains("angular", command.Aliases);
        await Task.CompletedTask;
    }

    [Fact]
    public async Task Invoke_MapsAllOptionsIntoRequest()
    {
        var fake = new CapturingAngularDiagramService();

        int exit = await Invoke(fake,
            "--service", "src/app/foo.service.ts",
            "--title", "Wiring",
            "-o", "out.puml",
            "--force");

        Assert.Equal(0, exit);
        AngularDiagramRequest request = Assert.IsType<AngularDiagramRequest>(fake.Captured);
        Assert.Equal("src/app/foo.service.ts", request.ServicePath);
        Assert.Equal("Wiring", request.Title);
        Assert.Equal("out.puml", request.OutputPath);
        Assert.True(request.Force);
    }

    [Fact]
    public async Task Invoke_ShortServiceAlias_IsAccepted()
    {
        var fake = new CapturingAngularDiagramService();

        await Invoke(fake, "-s", "a.service.ts");

        Assert.Equal("a.service.ts", Assert.IsType<AngularDiagramRequest>(fake.Captured).ServicePath);
    }

    [Fact]
    public async Task Invoke_OnlyService_LeavesOptionalsAtDefaults()
    {
        var fake = new CapturingAngularDiagramService();

        await Invoke(fake, "-s", "a.service.ts");

        AngularDiagramRequest request = Assert.IsType<AngularDiagramRequest>(fake.Captured);
        Assert.Null(request.Title);
        Assert.Null(request.OutputPath);
        Assert.False(request.Force);
    }

    [Theory]
    [InlineData(ScaffoldStatus.Success, 0)]
    [InlineData(ScaffoldStatus.InvalidInput, 1)]
    [InlineData(ScaffoldStatus.OutputError, 2)]
    public async Task Invoke_MapsStatusToExitCode(ScaffoldStatus status, int expectedExit)
    {
        var fake = new CapturingAngularDiagramService { Result = status };

        int exit = await Invoke(fake, "-s", "a.service.ts");

        Assert.Equal(expectedExit, exit);
    }

    [Fact]
    public async Task Invoke_MissingRequiredService_FailsWithoutCallingService()
    {
        var fake = new CapturingAngularDiagramService();

        int exit = await Invoke(fake, "--title", "no service");

        Assert.NotEqual(0, exit);
        Assert.Null(fake.Captured);
    }
}
