using System.CommandLine;
using System.Threading;
using System.Threading.Tasks;
using Cast.Cli.Commands;
using Cast.Cli.Models;
using Cast.Cli.Services;

namespace Cast.Cli.Tests;

public sealed class GenerateCommandTests
{
    private sealed class CapturingScaffoldService : IScaffoldService
    {
        public ScaffoldRequest? Captured { get; private set; }
        public ScaffoldStatus Result { get; set; } = ScaffoldStatus.Success;

        public Task<ScaffoldStatus> ExecuteAsync(ScaffoldRequest request, CancellationToken cancellationToken)
        {
            Captured = request;
            return Task.FromResult(Result);
        }
    }

    private static async Task<int> Invoke(IScaffoldService service, params string[] args)
    {
        Command command = new GenerateCommand(service).Build();
        var config = new InvocationConfiguration { Output = new System.IO.StringWriter(), Error = new System.IO.StringWriter() };
        return await command.Parse(args).InvokeAsync(config);
    }

    [Fact]
    public async Task Build_HasGenAlias()
    {
        Command command = new GenerateCommand(new CapturingScaffoldService()).Build();
        Assert.Contains("gen", command.Aliases);
        await Task.CompletedTask;
    }

    [Fact]
    public async Task Invoke_MapsAllOptionsIntoRequest()
    {
        var fake = new CapturingScaffoldService();

        int exit = await Invoke(fake,
            "-p", "actor:User",
            "-p", "OS",
            "-m", "User -> OS : place order",
            "--title", "Checkout",
            "--autonumber",
            "--theme", "plain",
            "-o", "out.puml",
            "--force");

        Assert.Equal(0, exit);
        ScaffoldRequest request = Assert.IsType<ScaffoldRequest>(fake.Captured);
        Assert.Equal(new[] { "actor:User", "OS" }, request.Participants);
        Assert.Equal(new[] { "User -> OS : place order" }, request.Messages);
        Assert.Equal("Checkout", request.Title);
        Assert.True(request.AutoNumber);
        Assert.Equal("plain", request.Theme);
        Assert.Equal("out.puml", request.OutputPath);
        Assert.True(request.Force);
        Assert.True(request.IncludeSampleFlow); // no --no-sample
    }

    [Fact]
    public async Task Invoke_WithoutNoSample_IncludesSampleFlow()
    {
        var fake = new CapturingScaffoldService();

        await Invoke(fake, "-p", "A");

        Assert.True(Assert.IsType<ScaffoldRequest>(fake.Captured).IncludeSampleFlow);
    }

    [Fact]
    public async Task Invoke_WithNoSample_DisablesSampleFlow()
    {
        var fake = new CapturingScaffoldService();

        await Invoke(fake, "-p", "A", "--no-sample");

        Assert.False(Assert.IsType<ScaffoldRequest>(fake.Captured).IncludeSampleFlow);
    }

    [Theory]
    [InlineData(ScaffoldStatus.Success, 0)]
    [InlineData(ScaffoldStatus.InvalidInput, 1)]
    [InlineData(ScaffoldStatus.OutputError, 2)]
    public async Task Invoke_MapsStatusToExitCode(ScaffoldStatus status, int expectedExit)
    {
        var fake = new CapturingScaffoldService { Result = status };

        int exit = await Invoke(fake, "-p", "A");

        Assert.Equal(expectedExit, exit);
    }

    [Fact]
    public async Task Invoke_MissingRequiredParticipant_FailsWithoutCallingService()
    {
        var fake = new CapturingScaffoldService();

        int exit = await Invoke(fake, "--title", "no participants");

        Assert.NotEqual(0, exit);
        Assert.Null(fake.Captured);
    }
}
