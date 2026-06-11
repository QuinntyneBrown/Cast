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
            "--force",
            "--outer-box-color", "#LightGray",
            "--inner-box-color", "#White");

        Assert.Equal(0, exit);
        AngularDiagramRequest request = Assert.IsType<AngularDiagramRequest>(fake.Captured);
        Assert.Equal("src/app/foo.service.ts", request.ServicePath);
        Assert.Equal("Wiring", request.Title);
        Assert.Equal("out.puml", request.OutputPath);
        Assert.True(request.Force);
        Assert.Equal("#LightGray", request.OuterBoxColor);
        Assert.Equal("#White", request.InnerBoxColor);
        Assert.True(request.OpenInEditor); // no --no-open
    }

    [Fact]
    public async Task Invoke_WithoutNoOpen_RequestsEditorOpen()
    {
        var fake = new CapturingAngularDiagramService();

        await Invoke(fake, "-s", "a.service.ts");

        Assert.True(Assert.IsType<AngularDiagramRequest>(fake.Captured).OpenInEditor);
    }

    [Fact]
    public async Task Invoke_WithNoOpen_SuppressesEditorOpen()
    {
        var fake = new CapturingAngularDiagramService();

        await Invoke(fake, "-s", "a.service.ts", "--no-open");

        Assert.False(Assert.IsType<AngularDiagramRequest>(fake.Captured).OpenInEditor);
    }

    [Fact]
    public async Task Invoke_WithoutBoxColors_LeavesThemNull()
    {
        var fake = new CapturingAngularDiagramService();

        await Invoke(fake, "-s", "a.service.ts");

        AngularDiagramRequest request = Assert.IsType<AngularDiagramRequest>(fake.Captured);
        Assert.Null(request.OuterBoxColor);
        Assert.Null(request.InnerBoxColor);
    }

    [Fact]
    public async Task Invoke_ShortServiceAlias_IsAccepted()
    {
        var fake = new CapturingAngularDiagramService();

        await Invoke(fake, "-s", "a.service.ts");

        Assert.Equal("a.service.ts", Assert.IsType<AngularDiagramRequest>(fake.Captured).ServicePath);
    }

    [Fact]
    public async Task Invoke_OnlyService_DefaultsOutputToPumlBesideSource()
    {
        var fake = new CapturingAngularDiagramService();

        await Invoke(fake, "-s", "a.service.ts");

        AngularDiagramRequest request = Assert.IsType<AngularDiagramRequest>(fake.Captured);
        Assert.Null(request.Title);
        Assert.Equal("a.service.puml", request.OutputPath);
        Assert.False(request.Force);
    }

    [Theory]
    [InlineData("foo.service.ts", "foo.service.puml")]
    [InlineData("src/app/foo.component.ts", "src/app/foo.component.puml")]
    [InlineData("auth.interceptor.ts", "auth.interceptor.puml")]
    [InlineData("bar.ts", "bar.puml")]
    public async Task Invoke_DefaultOutput_ReplacesTsExtensionWithPuml(string servicePath, string expectedOutput)
    {
        var fake = new CapturingAngularDiagramService();

        await Invoke(fake, "-s", servicePath);

        Assert.Equal(expectedOutput, Assert.IsType<AngularDiagramRequest>(fake.Captured).OutputPath);
    }

    [Fact]
    public async Task Invoke_ExplicitOutput_OverridesDefault()
    {
        var fake = new CapturingAngularDiagramService();

        await Invoke(fake, "-s", "a.service.ts", "-o", "custom.puml");

        Assert.Equal("custom.puml", Assert.IsType<AngularDiagramRequest>(fake.Captured).OutputPath);
    }

    [Fact]
    public async Task Invoke_Stdout_LeavesOutputPathNull()
    {
        var fake = new CapturingAngularDiagramService();

        await Invoke(fake, "-s", "a.service.ts", "--stdout");

        Assert.Null(Assert.IsType<AngularDiagramRequest>(fake.Captured).OutputPath);
    }

    [Fact]
    public async Task Invoke_FileAlias_IsAccepted()
    {
        var fake = new CapturingAngularDiagramService();

        await Invoke(fake, "--file", "a.service.ts");

        Assert.Equal("a.service.ts", Assert.IsType<AngularDiagramRequest>(fake.Captured).ServicePath);
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
