using System.CommandLine;
using System.Threading;
using System.Threading.Tasks;
using Cast.Cli.Commands;
using Cast.Cli.Models;
using Cast.Cli.Services;

namespace Cast.Cli.Tests;

public sealed class TemplateCommandTests
{
    private sealed class FakeTemplateService : ITemplateService
    {
        public DiagramTemplate? SavedTemplate { get; private set; }
        public RenderTemplateRequest? RenderedRequest { get; private set; }
        public bool ListCalled { get; private set; }
        public string? ShownName { get; private set; }
        public string? DeletedName { get; private set; }
        public ScaffoldStatus Result { get; set; } = ScaffoldStatus.Success;

        public Task<ScaffoldStatus> SaveAsync(DiagramTemplate template, CancellationToken cancellationToken)
        {
            SavedTemplate = template;
            return Task.FromResult(Result);
        }

        public Task<ScaffoldStatus> RenderAsync(RenderTemplateRequest request, CancellationToken cancellationToken)
        {
            RenderedRequest = request;
            return Task.FromResult(Result);
        }

        public Task<ScaffoldStatus> ListAsync(CancellationToken cancellationToken)
        {
            ListCalled = true;
            return Task.FromResult(Result);
        }

        public Task<ScaffoldStatus> ShowAsync(string name, CancellationToken cancellationToken)
        {
            ShownName = name;
            return Task.FromResult(Result);
        }

        public Task<ScaffoldStatus> DeleteAsync(string name, CancellationToken cancellationToken)
        {
            DeletedName = name;
            return Task.FromResult(Result);
        }
    }

    private static async Task<int> Invoke(ITemplateService service, params string[] args)
    {
        Command command = new TemplateCommand(service).Build();
        var config = new InvocationConfiguration { Output = new System.IO.StringWriter(), Error = new System.IO.StringWriter() };
        return await command.Parse(args).InvokeAsync(config);
    }

    [Fact]
    public async Task Build_HasTplAlias()
    {
        Command command = new TemplateCommand(new FakeTemplateService()).Build();
        Assert.Contains("tpl", command.Aliases);
        await Task.CompletedTask;
    }

    [Fact]
    public async Task Invoke_NameOnly_RendersWithDefaults()
    {
        var fake = new FakeTemplateService();

        int exit = await Invoke(fake, "--name", "acme-ordering");

        Assert.Equal(0, exit);
        RenderTemplateRequest request = Assert.IsType<RenderTemplateRequest>(fake.RenderedRequest);
        Assert.Equal("acme-ordering", request.Name);
        Assert.Empty(request.Messages);
        Assert.Equal("cast.puml", request.OutputPath);
        Assert.False(request.Force);
        Assert.True(request.IncludeSampleFlow);
        Assert.True(request.OpenInEditor);
    }

    [Fact]
    public async Task Invoke_MapsAllRenderOptionsIntoRequest()
    {
        var fake = new FakeTemplateService();

        int exit = await Invoke(fake,
            "-n", "acme",
            "-m", "User -> OS : place order",
            "-m", "OS --> User : confirmation",
            "--title", "Checkout",
            "--autonumber",
            "--theme", "plain",
            "-o", "out.puml",
            "--force",
            "--no-sample",
            "--outer-box-color", "#LightGray",
            "--inner-box-color", "#White");

        Assert.Equal(0, exit);
        RenderTemplateRequest request = Assert.IsType<RenderTemplateRequest>(fake.RenderedRequest);
        Assert.Equal("acme", request.Name);
        Assert.Equal(new[] { "User -> OS : place order", "OS --> User : confirmation" }, request.Messages);
        Assert.Equal("Checkout", request.Title);
        Assert.True(request.AutoNumber);
        Assert.Equal("plain", request.Theme);
        Assert.Equal("out.puml", request.OutputPath);
        Assert.True(request.Force);
        Assert.False(request.IncludeSampleFlow);
        Assert.Equal("#LightGray", request.OuterBoxColor);
        Assert.Equal("#White", request.InnerBoxColor);
        Assert.True(request.OpenInEditor); // no --no-open
    }

    [Fact]
    public async Task Invoke_WithStdout_LeavesOutputPathNull()
    {
        var fake = new FakeTemplateService();

        await Invoke(fake, "-n", "acme", "--stdout");

        Assert.Null(Assert.IsType<RenderTemplateRequest>(fake.RenderedRequest).OutputPath);
    }

    [Fact]
    public async Task Invoke_WithNoOpen_SuppressesEditorOpen()
    {
        var fake = new FakeTemplateService();

        await Invoke(fake, "-n", "acme", "--no-open");

        Assert.False(Assert.IsType<RenderTemplateRequest>(fake.RenderedRequest).OpenInEditor);
    }

    [Fact]
    public async Task Invoke_WithoutName_FailsWithoutCallingService()
    {
        var fake = new FakeTemplateService();

        int exit = await Invoke(fake);

        Assert.NotEqual(0, exit);
        Assert.Null(fake.RenderedRequest);
        Assert.Null(fake.SavedTemplate);
        Assert.False(fake.ListCalled);
    }

    [Theory]
    [InlineData(ScaffoldStatus.Success, 0)]
    [InlineData(ScaffoldStatus.InvalidInput, 1)]
    [InlineData(ScaffoldStatus.OutputError, 2)]
    public async Task Invoke_MapsStatusToExitCode(ScaffoldStatus status, int expectedExit)
    {
        var fake = new FakeTemplateService { Result = status };

        int exit = await Invoke(fake, "-n", "acme");

        Assert.Equal(expectedExit, exit);
    }

    [Fact]
    public async Task Save_MapsAllOptionsIntoTemplate()
    {
        var fake = new FakeTemplateService();

        int exit = await Invoke(fake,
            "save",
            "-n", "acme",
            "-p", "actor:User",
            "-p", "OS:Order Service",
            "-m", "User -> OS : place order",
            "--title", "Acme ordering",
            "--autonumber",
            "--theme", "plain",
            "--outer-box-color", "#LightGray",
            "--inner-box-color", "#White");

        Assert.Equal(0, exit);
        DiagramTemplate template = Assert.IsType<DiagramTemplate>(fake.SavedTemplate);
        Assert.Equal("acme", template.Name);
        Assert.Equal(new[] { "actor:User", "OS:Order Service" }, template.Participants);
        Assert.Equal(new[] { "User -> OS : place order" }, template.Messages);
        Assert.Equal("Acme ordering", template.Title);
        Assert.True(template.AutoNumber);
        Assert.Equal("plain", template.Theme);
        Assert.Equal("#LightGray", template.OuterBoxColor);
        Assert.Equal("#White", template.InnerBoxColor);
    }

    [Fact]
    public async Task Save_MissingName_FailsWithoutCallingService()
    {
        var fake = new FakeTemplateService();

        int exit = await Invoke(fake, "save", "-p", "actor:User");

        Assert.NotEqual(0, exit);
        Assert.Null(fake.SavedTemplate);
    }

    [Fact]
    public async Task Save_MissingParticipant_FailsWithoutCallingService()
    {
        var fake = new FakeTemplateService();

        int exit = await Invoke(fake, "save", "-n", "acme");

        Assert.NotEqual(0, exit);
        Assert.Null(fake.SavedTemplate);
    }

    [Fact]
    public async Task List_DispatchesToService()
    {
        var fake = new FakeTemplateService();

        int exit = await Invoke(fake, "list");

        Assert.Equal(0, exit);
        Assert.True(fake.ListCalled);
        Assert.Null(fake.RenderedRequest); // the parent render action did not run
    }

    [Fact]
    public async Task Show_DispatchesWithName()
    {
        var fake = new FakeTemplateService();

        int exit = await Invoke(fake, "show", "--name", "acme");

        Assert.Equal(0, exit);
        Assert.Equal("acme", fake.ShownName);
    }

    [Fact]
    public async Task Delete_DispatchesWithName()
    {
        var fake = new FakeTemplateService();

        int exit = await Invoke(fake, "delete", "-n", "acme");

        Assert.Equal(0, exit);
        Assert.Equal("acme", fake.DeletedName);
    }

    [Theory]
    [InlineData(ScaffoldStatus.InvalidInput, 1)]
    [InlineData(ScaffoldStatus.OutputError, 2)]
    public async Task Save_MapsStatusToExitCode(ScaffoldStatus status, int expectedExit)
    {
        var fake = new FakeTemplateService { Result = status };

        int exit = await Invoke(fake, "save", "-n", "acme", "-p", "A");

        Assert.Equal(expectedExit, exit);
    }
}
