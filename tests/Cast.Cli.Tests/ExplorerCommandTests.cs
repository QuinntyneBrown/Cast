using System.CommandLine;
using System.Threading;
using System.Threading.Tasks;
using Cast.Cli.Commands;
using Cast.Cli.Services;

namespace Cast.Cli.Tests;

public sealed class ExplorerCommandTests
{
    private sealed class FakeExplorerService : IExplorerService
    {
        public int Calls { get; private set; }
        public ScaffoldStatus Result { get; set; } = ScaffoldStatus.Success;

        public Task<ScaffoldStatus> ExecuteAsync(CancellationToken cancellationToken)
        {
            Calls++;
            return Task.FromResult(Result);
        }
    }

    private static async Task<int> Invoke(IExplorerService service, params string[] args)
    {
        Command command = new ExplorerCommand(service).Build();
        var config = new InvocationConfiguration { Output = new System.IO.StringWriter(), Error = new System.IO.StringWriter() };
        return await command.Parse(args).InvokeAsync(config);
    }

    [Fact]
    public async Task Invoke_DispatchesToService()
    {
        var fake = new FakeExplorerService();

        int exit = await Invoke(fake);

        Assert.Equal(0, exit);
        Assert.Equal(1, fake.Calls);
    }

    [Theory]
    [InlineData(ScaffoldStatus.Success, 0)]
    [InlineData(ScaffoldStatus.InvalidInput, 1)]
    [InlineData(ScaffoldStatus.OutputError, 2)]
    public async Task Invoke_MapsStatusToExitCode(ScaffoldStatus status, int expectedExit)
    {
        var fake = new FakeExplorerService { Result = status };

        int exit = await Invoke(fake);

        Assert.Equal(expectedExit, exit);
    }
}
