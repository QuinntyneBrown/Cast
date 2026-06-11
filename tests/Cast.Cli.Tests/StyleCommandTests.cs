using System.CommandLine;
using System.Threading;
using System.Threading.Tasks;
using Cast.Cli.Commands;
using Cast.Cli.Models;
using Cast.Cli.Services;

namespace Cast.Cli.Tests;

public sealed class StyleCommandTests
{
    private sealed class CapturingStyleService : IStyleService
    {
        public StyleRequest? Captured { get; private set; }
        public ScaffoldStatus Result { get; set; } = ScaffoldStatus.Success;

        public Task<ScaffoldStatus> ExecuteAsync(StyleRequest request, CancellationToken cancellationToken)
        {
            Captured = request;
            return Task.FromResult(Result);
        }
    }

    private static async Task<int> Invoke(IStyleService service, params string[] args)
    {
        Command command = new StyleCommand(service).Build();
        var config = new InvocationConfiguration { Output = new System.IO.StringWriter(), Error = new System.IO.StringWriter() };
        return await command.Parse(args).InvokeAsync(config);
    }

    [Fact]
    public async Task Invoke_MapsAllInputsIntoRequest()
    {
        var fake = new CapturingStyleService();

        int exit = await Invoke(fake,
            "diagrams",
            "--outer-box-color", "#LightGray",
            "--inner-box-color", "#White");

        Assert.Equal(0, exit);
        StyleRequest request = Assert.IsType<StyleRequest>(fake.Captured);
        Assert.Equal("diagrams", request.Path);
        Assert.Equal("#LightGray", request.OuterBoxColor);
        Assert.Equal("#White", request.InnerBoxColor);
    }

    [Fact]
    public async Task Invoke_WithoutBoxColors_LeavesThemNull()
    {
        var fake = new CapturingStyleService();

        await Invoke(fake, "some.puml");

        StyleRequest request = Assert.IsType<StyleRequest>(fake.Captured);
        Assert.Equal("some.puml", request.Path);
        Assert.Null(request.OuterBoxColor);
        Assert.Null(request.InnerBoxColor);
    }

    [Theory]
    [InlineData(ScaffoldStatus.Success, 0)]
    [InlineData(ScaffoldStatus.InvalidInput, 1)]
    [InlineData(ScaffoldStatus.OutputError, 2)]
    public async Task Invoke_MapsStatusToExitCode(ScaffoldStatus status, int expectedExit)
    {
        var fake = new CapturingStyleService { Result = status };

        int exit = await Invoke(fake, "some.puml");

        Assert.Equal(expectedExit, exit);
    }

    [Fact]
    public async Task Invoke_MissingPath_FailsWithoutCallingService()
    {
        var fake = new CapturingStyleService();

        int exit = await Invoke(fake);

        Assert.NotEqual(0, exit);
        Assert.Null(fake.Captured);
    }
}
