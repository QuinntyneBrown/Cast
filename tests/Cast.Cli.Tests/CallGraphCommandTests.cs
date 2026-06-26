using System.IO;
using System.Threading;
using System.Threading.Tasks;
using System.CommandLine;
using Cast.Cli.Commands;
using Cast.Cli.Models;
using Cast.Cli.Services;
using Microsoft.Extensions.Logging.Abstractions;

namespace Cast.Cli.Tests;

public sealed class CallGraphCommandTests
{
    private sealed class CapturingCallGraphService : ICallGraphService
    {
        public CallGraphRequest? Captured { get; private set; }
        public ScaffoldStatus Result { get; set; } = ScaffoldStatus.Success;

        public Task<ScaffoldStatus> ExecuteAsync(CallGraphRequest request, CancellationToken cancellationToken)
        {
            Captured = request;
            return Task.FromResult(Result);
        }
    }

    private sealed class StubReader : ISourceFileReader
    {
        private readonly string _content;

        public StubReader(string content) => _content = content;

        public Task<string> ReadAsync(string path, CancellationToken cancellationToken) => Task.FromResult(_content);
    }

    private sealed class NoopFileOpener : IFileOpener
    {
        public void Open(string path)
        {
        }
    }

    private static async Task<int> Invoke(ICallGraphService service, params string[] args)
    {
        Command command = new CallGraphCommand(service).Build();
        var config = new InvocationConfiguration { Output = new System.IO.StringWriter(), Error = new System.IO.StringWriter() };
        return await command.Parse(args).InvokeAsync(config);
    }

    [Fact]
    public async Task Build_HasCallgraphAlias()
    {
        Command command = new CallGraphCommand(new CapturingCallGraphService()).Build();
        Assert.Contains("callgraph", command.Aliases);
        await Task.CompletedTask;
    }

    [Fact]
    public async Task Invoke_MapsAllOptionsIntoRequest()
    {
        var fake = new CapturingCallGraphService();

        int exit = await Invoke(fake,
            "--source", "src/app/order.service.ts",
            "--title", "Calls",
            "-o", "out.puml",
            "--force",
            "--method", "placeOrder",
            "--method", "getTotal",
            "--include-private",
            "--outer-box-color", "#LightGray",
            "--inner-box-color", "#White");

        Assert.Equal(0, exit);
        CallGraphRequest request = Assert.IsType<CallGraphRequest>(fake.Captured);
        Assert.Equal("src/app/order.service.ts", request.SourcePath);
        Assert.Equal("Calls", request.Title);
        Assert.Equal("out.puml", request.OutputPath);
        Assert.True(request.Force);
        Assert.Equal(["placeOrder", "getTotal"], request.Methods);
        Assert.True(request.IncludePrivate);
        Assert.Equal("#LightGray", request.OuterBoxColor);
        Assert.Equal("#White", request.InnerBoxColor);
        Assert.True(request.OpenInEditor); // no --no-open
    }

    [Fact]
    public async Task Invoke_OnlySource_DefaultsOutputToPumlBesideSource()
    {
        var fake = new CapturingCallGraphService();

        await Invoke(fake, "-s", "order.service.ts");

        CallGraphRequest request = Assert.IsType<CallGraphRequest>(fake.Captured);
        Assert.Null(request.Title);
        Assert.Equal("order.service.puml", request.OutputPath);
        Assert.False(request.Force);
        Assert.Empty(request.Methods);
        Assert.False(request.IncludePrivate);
    }

    [Theory]
    [InlineData("foo.service.ts", "foo.service.puml")]
    [InlineData("src/app/bar.component.ts", "src/app/bar.component.puml")]
    [InlineData("baz.ts", "baz.puml")]
    public async Task Invoke_DefaultOutput_ReplacesTsExtensionWithPuml(string sourcePath, string expectedOutput)
    {
        var fake = new CapturingCallGraphService();

        await Invoke(fake, "-s", sourcePath);

        Assert.Equal(expectedOutput, Assert.IsType<CallGraphRequest>(fake.Captured).OutputPath);
    }

    [Fact]
    public async Task Invoke_Stdout_LeavesOutputPathNull()
    {
        var fake = new CapturingCallGraphService();

        await Invoke(fake, "-s", "a.service.ts", "--stdout");

        Assert.Null(Assert.IsType<CallGraphRequest>(fake.Captured).OutputPath);
    }

    [Fact]
    public async Task Invoke_FileAlias_IsAccepted()
    {
        var fake = new CapturingCallGraphService();

        await Invoke(fake, "--file", "a.service.ts");

        Assert.Equal("a.service.ts", Assert.IsType<CallGraphRequest>(fake.Captured).SourcePath);
    }

    [Fact]
    public async Task Invoke_WithNoOpen_SuppressesEditorOpen()
    {
        var fake = new CapturingCallGraphService();

        await Invoke(fake, "-s", "a.service.ts", "--no-open");

        Assert.False(Assert.IsType<CallGraphRequest>(fake.Captured).OpenInEditor);
    }

    [Theory]
    [InlineData(ScaffoldStatus.Success, 0)]
    [InlineData(ScaffoldStatus.InvalidInput, 1)]
    [InlineData(ScaffoldStatus.OutputError, 2)]
    public async Task Invoke_MapsStatusToExitCode(ScaffoldStatus status, int expectedExit)
    {
        var fake = new CapturingCallGraphService { Result = status };

        int exit = await Invoke(fake, "-s", "a.service.ts");

        Assert.Equal(expectedExit, exit);
    }

    [Fact]
    public async Task Invoke_MissingRequiredSource_FailsWithoutCallingService()
    {
        var fake = new CapturingCallGraphService();

        int exit = await Invoke(fake, "--title", "no source");

        Assert.NotEqual(0, exit);
        Assert.Null(fake.Captured);
    }

    [Fact]
    public async Task Invoke_InvalidMethodFilter_ReturnsUsageExitCode_EndToEnd()
    {
        // Drive the real service so the command -> service -> exit-code wiring is exercised together.
        const string source = """
            export class OrderService {
              place(): void {}
            }
            """;
        var service = new CallGraphService(
            new StubReader(source),
            new TypeScriptCallGraphParser(),
            new PlantUmlCallGraphRenderer(),
            new FileSystemDiagramWriter(new StringWriter()),
            new NoopFileOpener(),
            NullLogger<CallGraphService>.Instance);

        int exit = await Invoke(service, "-s", "order.service.ts", "--method", "nope", "--stdout");

        Assert.Equal(1, exit);
    }
}
