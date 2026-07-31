using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Cast.Cli.Models;
using Cast.Cli.Services;
using Cast.Core.Services;
using Microsoft.Extensions.Logging.Abstractions;

namespace Cast.Cli.Tests;

public sealed class TemplateServiceTests
{
    private sealed class FakeTemplateStore : ITemplateStore
    {
        public Dictionary<string, DiagramTemplate> Templates { get; } = new(StringComparer.Ordinal);

        /// <summary>When set, every store call throws this (simulates filesystem failures).</summary>
        public Exception? ThrowOnAccess { get; set; }

        public Task SaveAsync(DiagramTemplate template, CancellationToken cancellationToken)
        {
            ThrowIfConfigured();
            Templates[template.Name] = template;
            return Task.CompletedTask;
        }

        public Task<DiagramTemplate?> FindAsync(string name, CancellationToken cancellationToken)
        {
            ThrowIfConfigured();
            return Task.FromResult(Templates.TryGetValue(name, out DiagramTemplate? template) ? template : null);
        }

        public Task<IReadOnlyList<string>> ListAsync(CancellationToken cancellationToken)
        {
            ThrowIfConfigured();
            return Task.FromResult<IReadOnlyList<string>>(
                Templates.Keys.Order(StringComparer.Ordinal).ToList());
        }

        public Task<bool> DeleteAsync(string name, CancellationToken cancellationToken)
        {
            ThrowIfConfigured();
            return Task.FromResult(Templates.Remove(name));
        }

        public string EnsureRootDirectory()
        {
            ThrowIfConfigured();
            return "fake-root";
        }

        private void ThrowIfConfigured()
        {
            if (ThrowOnAccess is not null)
            {
                throw ThrowOnAccess;
            }
        }
    }

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

    private static (TemplateService Service, FakeTemplateStore Store, CapturingScaffoldService Scaffold, StringWriter StdOut) CreateService()
    {
        var store = new FakeTemplateStore();
        var scaffold = new CapturingScaffoldService();
        var stdout = new StringWriter();

        var service = new TemplateService(
            store,
            scaffold,
            new DiagramSpecValidator(new ParticipantParser(new ParticipantKindCatalog()), new MessageParser()),
            new FileSystemDiagramWriter(stdout),
            NullLogger<TemplateService>.Instance);

        return (service, store, scaffold, stdout);
    }

    private static DiagramTemplate Template(string name = "acme") => new()
    {
        Name = name,
        Participants = ["actor:User", "OS:Order Service"],
        Messages = ["User -> OS : stored message"],
        Title = "Stored title",
        AutoNumber = true,
        Theme = "stored-theme",
        OuterBoxColor = "#StoredOuter",
        InnerBoxColor = "#StoredInner",
    };

    private static RenderTemplateRequest Render(
        string name = "acme",
        IReadOnlyList<string>? messages = null,
        string? title = null,
        bool autoNumber = false,
        string? theme = null,
        string? outputPath = null,
        bool force = false,
        bool includeSampleFlow = true,
        string? outerBoxColor = null,
        string? innerBoxColor = null,
        bool openInEditor = false) =>
        new(name, messages ?? [], title, autoNumber, theme, outputPath, force, includeSampleFlow,
            outerBoxColor, innerBoxColor, openInEditor);

    [Fact]
    public async Task SaveAsync_ValidTemplate_PersistsAndReturnsSuccess()
    {
        (TemplateService service, FakeTemplateStore store, _, _) = CreateService();

        ScaffoldStatus status = await service.SaveAsync(Template(), CancellationToken.None);

        Assert.Equal(ScaffoldStatus.Success, status);
        Assert.True(store.Templates.ContainsKey("acme"));
    }

    [Fact]
    public async Task SaveAsync_NoParticipants_ReturnsInvalidInput_AndStoreUntouched()
    {
        (TemplateService service, FakeTemplateStore store, _, _) = CreateService();

        ScaffoldStatus status = await service.SaveAsync(
            Template() with { Participants = [] }, CancellationToken.None);

        Assert.Equal(ScaffoldStatus.InvalidInput, status);
        Assert.Empty(store.Templates);
    }

    [Fact]
    public async Task SaveAsync_DuplicateAlias_ReturnsInvalidInput_AndStoreUntouched()
    {
        (TemplateService service, FakeTemplateStore store, _, _) = CreateService();

        ScaffoldStatus status = await service.SaveAsync(
            Template() with { Participants = ["A", "A"] }, CancellationToken.None);

        Assert.Equal(ScaffoldStatus.InvalidInput, status);
        Assert.Empty(store.Templates);
    }

    [Fact]
    public async Task SaveAsync_MessageWithUnknownEndpoint_ReturnsInvalidInput_AndStoreUntouched()
    {
        (TemplateService service, FakeTemplateStore store, _, _) = CreateService();

        ScaffoldStatus status = await service.SaveAsync(
            Template() with { Messages = ["User -> Nope : oops"] }, CancellationToken.None);

        Assert.Equal(ScaffoldStatus.InvalidInput, status);
        Assert.Empty(store.Templates);
    }

    [Fact]
    public async Task SaveAsync_BoxColorWithWhitespace_ReturnsInvalidInput_AndStoreUntouched()
    {
        (TemplateService service, FakeTemplateStore store, _, _) = CreateService();

        ScaffoldStatus status = await service.SaveAsync(
            Template() with { OuterBoxColor = "light gray" }, CancellationToken.None);

        Assert.Equal(ScaffoldStatus.InvalidInput, status);
        Assert.Empty(store.Templates);
    }

    [Fact]
    public async Task SaveAsync_StoreIOFailure_ReturnsOutputError()
    {
        (TemplateService service, FakeTemplateStore store, _, _) = CreateService();
        store.ThrowOnAccess = new IOException("disk full");

        ScaffoldStatus status = await service.SaveAsync(Template(), CancellationToken.None);

        Assert.Equal(ScaffoldStatus.OutputError, status);
    }

    [Fact]
    public async Task RenderAsync_MissingTemplate_ReturnsInvalidInput_WithoutScaffolding()
    {
        (TemplateService service, _, CapturingScaffoldService scaffold, _) = CreateService();

        ScaffoldStatus status = await service.RenderAsync(Render("nope"), CancellationToken.None);

        Assert.Equal(ScaffoldStatus.InvalidInput, status);
        Assert.Null(scaffold.Captured);
    }

    [Fact]
    public async Task RenderAsync_NoOverrides_UsesStoredValues()
    {
        (TemplateService service, FakeTemplateStore store, CapturingScaffoldService scaffold, _) = CreateService();
        store.Templates["acme"] = Template();

        ScaffoldStatus status = await service.RenderAsync(Render(), CancellationToken.None);

        Assert.Equal(ScaffoldStatus.Success, status);
        ScaffoldRequest request = Assert.IsType<ScaffoldRequest>(scaffold.Captured);
        Assert.Equal(["actor:User", "OS:Order Service"], request.Participants);
        Assert.Equal(["User -> OS : stored message"], request.Messages);
        Assert.Equal("Stored title", request.Title);
        Assert.True(request.AutoNumber);
        Assert.Equal("stored-theme", request.Theme);
        Assert.Equal("#StoredOuter", request.OuterBoxColor);
        Assert.Equal("#StoredInner", request.InnerBoxColor);
    }

    [Fact]
    public async Task RenderAsync_MessagesProvided_ReplaceStoredMessagesEntirely()
    {
        (TemplateService service, FakeTemplateStore store, CapturingScaffoldService scaffold, _) = CreateService();
        store.Templates["acme"] = Template();

        await service.RenderAsync(
            Render(messages: ["User -> OS : place order"]), CancellationToken.None);

        ScaffoldRequest request = Assert.IsType<ScaffoldRequest>(scaffold.Captured);
        Assert.Equal(["User -> OS : place order"], request.Messages);
    }

    [Fact]
    public async Task RenderAsync_Overrides_WinOverStoredValues()
    {
        (TemplateService service, FakeTemplateStore store, CapturingScaffoldService scaffold, _) = CreateService();
        store.Templates["acme"] = Template();

        await service.RenderAsync(
            Render(title: "Override", theme: "override-theme", outerBoxColor: "#A", innerBoxColor: "#B"),
            CancellationToken.None);

        ScaffoldRequest request = Assert.IsType<ScaffoldRequest>(scaffold.Captured);
        Assert.Equal("Override", request.Title);
        Assert.Equal("override-theme", request.Theme);
        Assert.Equal("#A", request.OuterBoxColor);
        Assert.Equal("#B", request.InnerBoxColor);
    }

    [Fact]
    public async Task RenderAsync_AutoNumberStoredOn_StaysOnWithoutFlag()
    {
        (TemplateService service, FakeTemplateStore store, CapturingScaffoldService scaffold, _) = CreateService();
        store.Templates["acme"] = Template() with { AutoNumber = true };

        await service.RenderAsync(Render(autoNumber: false), CancellationToken.None);

        Assert.True(Assert.IsType<ScaffoldRequest>(scaffold.Captured).AutoNumber);
    }

    [Fact]
    public async Task RenderAsync_PassesRenderOnlyFieldsThrough()
    {
        (TemplateService service, FakeTemplateStore store, CapturingScaffoldService scaffold, _) = CreateService();
        store.Templates["acme"] = Template();

        await service.RenderAsync(
            Render(outputPath: "out.puml", force: true, includeSampleFlow: false, openInEditor: true),
            CancellationToken.None);

        ScaffoldRequest request = Assert.IsType<ScaffoldRequest>(scaffold.Captured);
        Assert.Equal("out.puml", request.OutputPath);
        Assert.True(request.Force);
        Assert.False(request.IncludeSampleFlow);
        Assert.True(request.OpenInEditor);
    }

    [Fact]
    public async Task RenderAsync_PropagatesScaffoldStatus()
    {
        (TemplateService service, FakeTemplateStore store, CapturingScaffoldService scaffold, _) = CreateService();
        store.Templates["acme"] = Template();
        scaffold.Result = ScaffoldStatus.OutputError;

        ScaffoldStatus status = await service.RenderAsync(Render(), CancellationToken.None);

        Assert.Equal(ScaffoldStatus.OutputError, status);
    }

    [Fact]
    public async Task ListAsync_WritesSortedNamesToStdOut()
    {
        (TemplateService service, FakeTemplateStore store, _, StringWriter stdout) = CreateService();
        store.Templates["zeta"] = Template("zeta");
        store.Templates["alpha"] = Template("alpha");

        ScaffoldStatus status = await service.ListAsync(CancellationToken.None);

        Assert.Equal(ScaffoldStatus.Success, status);
        Assert.Equal("alpha\nzeta\n", stdout.ToString());
    }

    [Fact]
    public async Task ListAsync_NoTemplates_WritesNothingAndReturnsSuccess()
    {
        (TemplateService service, _, _, StringWriter stdout) = CreateService();

        ScaffoldStatus status = await service.ListAsync(CancellationToken.None);

        Assert.Equal(ScaffoldStatus.Success, status);
        Assert.Equal(string.Empty, stdout.ToString());
    }

    [Fact]
    public async Task ListAsync_StoreIOFailure_ReturnsOutputError()
    {
        (TemplateService service, FakeTemplateStore store, _, _) = CreateService();
        store.ThrowOnAccess = new IOException("denied");

        Assert.Equal(ScaffoldStatus.OutputError, await service.ListAsync(CancellationToken.None));
    }

    [Fact]
    public async Task ShowAsync_WritesCanonicalJsonToStdOut()
    {
        (TemplateService service, FakeTemplateStore store, _, StringWriter stdout) = CreateService();
        store.Templates["acme"] = Template();

        ScaffoldStatus status = await service.ShowAsync("acme", CancellationToken.None);

        Assert.Equal(ScaffoldStatus.Success, status);
        string output = stdout.ToString();
        Assert.Contains("\"name\": \"acme\"", output);
        Assert.Contains("\"participants\"", output);
        Assert.Contains("actor:User", output);
        Assert.Contains("User -> OS : stored message", output); // '>' not escaped to >
    }

    [Fact]
    public async Task ShowAsync_MissingTemplate_ReturnsInvalidInput()
    {
        (TemplateService service, _, _, StringWriter stdout) = CreateService();

        ScaffoldStatus status = await service.ShowAsync("nope", CancellationToken.None);

        Assert.Equal(ScaffoldStatus.InvalidInput, status);
        Assert.Equal(string.Empty, stdout.ToString());
    }

    [Fact]
    public async Task DeleteAsync_ExistingTemplate_RemovesAndReturnsSuccess()
    {
        (TemplateService service, FakeTemplateStore store, _, _) = CreateService();
        store.Templates["acme"] = Template();

        ScaffoldStatus status = await service.DeleteAsync("acme", CancellationToken.None);

        Assert.Equal(ScaffoldStatus.Success, status);
        Assert.Empty(store.Templates);
    }

    [Fact]
    public async Task DeleteAsync_MissingTemplate_ReturnsInvalidInput()
    {
        (TemplateService service, _, _, _) = CreateService();

        Assert.Equal(ScaffoldStatus.InvalidInput, await service.DeleteAsync("nope", CancellationToken.None));
    }

    [Fact]
    public async Task DeleteAsync_StoreIOFailure_ReturnsOutputError()
    {
        (TemplateService service, FakeTemplateStore store, _, _) = CreateService();
        store.ThrowOnAccess = new IOException("denied");

        Assert.Equal(ScaffoldStatus.OutputError, await service.DeleteAsync("acme", CancellationToken.None));
    }
}
