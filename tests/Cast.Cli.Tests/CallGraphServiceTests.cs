using System;
using System.Collections.Generic;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using Cast.Cli.Models;
using Cast.Cli.Services;
using Microsoft.Extensions.Logging.Abstractions;

namespace Cast.Cli.Tests;

public sealed class CallGraphServiceTests
{
    private sealed class StubReader : ISourceFileReader
    {
        private readonly Func<string, string> _read;

        public StubReader(Func<string, string> read) => _read = read;

        public Task<string> ReadAsync(string path, CancellationToken cancellationToken) =>
            Task.FromResult(_read(path));
    }

    private const string OrderSource = """
        import { inject } from '@angular/core';

        export class OrderService {
          private readonly repo = inject(OrderRepository);
          place(id: string): void {
            this.repo.save(id);
          }
          private helper(): void {
            this.repo.flush();
          }
        }
        """;

    private sealed class FakeFileOpener : IFileOpener
    {
        public List<string> OpenedPaths { get; } = [];

        public void Open(string path) => OpenedPaths.Add(path);
    }

    private static (CallGraphService Service, StringWriter StdOut, FakeFileOpener Opener) CreateService(ISourceFileReader reader)
    {
        var stdout = new StringWriter();
        var opener = new FakeFileOpener();
        var service = new CallGraphService(
            reader,
            new TypeScriptCallGraphParser(),
            new PlantUmlCallGraphRenderer(),
            new FileSystemDiagramWriter(stdout),
            opener,
            NullLogger<CallGraphService>.Instance);

        return (service, stdout, opener);
    }

    private static CallGraphRequest Request(
        string path = "order.service.ts",
        string? title = null,
        string? outputPath = null,
        bool force = false,
        IReadOnlyList<string>? methods = null,
        bool includePrivate = false,
        string? outerBoxColor = null,
        string? innerBoxColor = null,
        bool openInEditor = false) =>
        new(path, title, outputPath, force, methods ?? [], includePrivate, outerBoxColor, innerBoxColor, openInEditor);

    [Fact]
    public async Task ExecuteAsync_PublicMethodsOnly_ByDefault()
    {
        (CallGraphService service, StringWriter stdout, _) = CreateService(new StubReader(_ => OrderSource));

        ScaffoldStatus status = await service.ExecuteAsync(Request(), CancellationToken.None);

        Assert.Equal(ScaffoldStatus.Success, status);
        string output = stdout.ToString();
        Assert.Contains("title Call graph of OrderService", output);
        Assert.Contains("== place(id) ==", output);
        Assert.DoesNotContain("== helper() ==", output); // private, excluded by default
    }

    [Fact]
    public async Task ExecuteAsync_IncludePrivate_ShowsNonPublicMembers()
    {
        (CallGraphService service, StringWriter stdout, _) = CreateService(new StubReader(_ => OrderSource));

        await service.ExecuteAsync(Request(includePrivate: true), CancellationToken.None);

        string output = stdout.ToString();
        Assert.Contains("== place(id) ==", output);
        Assert.Contains("== helper() ==", output);
    }

    [Fact]
    public async Task ExecuteAsync_MethodFilter_SelectsNamedMemberRegardlessOfVisibility()
    {
        (CallGraphService service, StringWriter stdout, _) = CreateService(new StubReader(_ => OrderSource));

        ScaffoldStatus status = await service.ExecuteAsync(Request(methods: ["helper"]), CancellationToken.None);

        Assert.Equal(ScaffoldStatus.Success, status);
        string output = stdout.ToString();
        Assert.Contains("== helper() ==", output);
        Assert.DoesNotContain("== place(id) ==", output);
    }

    [Fact]
    public async Task ExecuteAsync_MethodFilter_IsCaseInsensitive()
    {
        (CallGraphService service, StringWriter stdout, _) = CreateService(new StubReader(_ => OrderSource));

        ScaffoldStatus status = await service.ExecuteAsync(Request(methods: ["PLACE"]), CancellationToken.None);

        Assert.Equal(ScaffoldStatus.Success, status);
        Assert.Contains("== place(id) ==", stdout.ToString());
    }

    [Fact]
    public async Task ExecuteAsync_UnknownMethodFilter_ReturnsInvalidInput()
    {
        (CallGraphService service, StringWriter stdout, _) = CreateService(new StubReader(_ => OrderSource));

        ScaffoldStatus status = await service.ExecuteAsync(Request(methods: ["nope"]), CancellationToken.None);

        Assert.Equal(ScaffoldStatus.InvalidInput, status);
        Assert.Equal(string.Empty, stdout.ToString());
    }

    [Fact]
    public async Task ExecuteAsync_NoPublicMethods_ReturnsInvalidInput()
    {
        const string onlyPrivate = """
            export class Secret {
              private only(): void {}
            }
            """;
        (CallGraphService service, _, _) = CreateService(new StubReader(_ => onlyPrivate));

        ScaffoldStatus status = await service.ExecuteAsync(Request(), CancellationToken.None);

        Assert.Equal(ScaffoldStatus.InvalidInput, status);
    }

    [Fact]
    public async Task ExecuteAsync_BoxColors_FlowIntoRenderedOutput()
    {
        (CallGraphService service, StringWriter stdout, _) = CreateService(new StubReader(_ => OrderSource));

        ScaffoldStatus status = await service.ExecuteAsync(
            Request(outerBoxColor: "LightGray", innerBoxColor: "#White"), CancellationToken.None);

        Assert.Equal(ScaffoldStatus.Success, status);
        string output = stdout.ToString();
        Assert.Contains("box #LightGray", output);
        Assert.Contains("box #White", output);
    }

    [Fact]
    public async Task ExecuteAsync_BoxColorWithWhitespace_ReturnsInvalidInput()
    {
        (CallGraphService service, StringWriter stdout, _) = CreateService(new StubReader(_ => OrderSource));

        ScaffoldStatus status = await service.ExecuteAsync(
            Request(outerBoxColor: "light gray"), CancellationToken.None);

        Assert.Equal(ScaffoldStatus.InvalidInput, status);
        Assert.Equal(string.Empty, stdout.ToString());
    }

    [Fact]
    public async Task ExecuteAsync_TitleWithControlChar_ReturnsInvalidInput()
    {
        (CallGraphService service, StringWriter stdout, _) = CreateService(new StubReader(_ => OrderSource));

        ScaffoldStatus status = await service.ExecuteAsync(
            Request(title: "line one\nC0 -> C0 : injected"), CancellationToken.None);

        Assert.Equal(ScaffoldStatus.InvalidInput, status);
        Assert.Equal(string.Empty, stdout.ToString());
    }

    [Fact]
    public async Task ExecuteAsync_FileNotFound_ReturnsInvalidInput()
    {
        (CallGraphService service, _, _) = CreateService(
            new StubReader(path => throw new FileNotFoundException("missing", path)));

        ScaffoldStatus status = await service.ExecuteAsync(Request(), CancellationToken.None);

        Assert.Equal(ScaffoldStatus.InvalidInput, status);
    }

    [Fact]
    public async Task ExecuteAsync_UnparseableSource_ReturnsInvalidInput()
    {
        (CallGraphService service, _, _) = CreateService(new StubReader(_ => "export const x = 1;"));

        ScaffoldStatus status = await service.ExecuteAsync(Request(), CancellationToken.None);

        Assert.Equal(ScaffoldStatus.InvalidInput, status);
    }

    [Fact]
    public async Task ExecuteAsync_ExistingOutputWithoutForce_ReturnsOutputError()
    {
        (CallGraphService service, _, _) = CreateService(new StubReader(_ => OrderSource));
        string path = Path.Combine(Path.GetTempPath(), $"cast-calls-{Guid.NewGuid():N}.puml");
        await File.WriteAllTextAsync(path, "existing");

        try
        {
            ScaffoldStatus status = await service.ExecuteAsync(
                Request(outputPath: path, force: false), CancellationToken.None);

            Assert.Equal(ScaffoldStatus.OutputError, status);
            Assert.Equal("existing", await File.ReadAllTextAsync(path));
        }
        finally
        {
            if (File.Exists(path)) File.Delete(path);
        }
    }

    [Fact]
    public async Task ExecuteAsync_OutputPathWithForce_OverwritesFileAndReturnsSuccess()
    {
        (CallGraphService service, _, _) = CreateService(new StubReader(_ => OrderSource));
        string path = Path.Combine(Path.GetTempPath(), $"cast-calls-{Guid.NewGuid():N}.puml");
        await File.WriteAllTextAsync(path, "existing");

        try
        {
            ScaffoldStatus status = await service.ExecuteAsync(
                Request(outputPath: path, force: true), CancellationToken.None);

            Assert.Equal(ScaffoldStatus.Success, status);
            string written = await File.ReadAllTextAsync(path);
            Assert.StartsWith("@startuml", written);
            Assert.Contains("OrderService", written);
        }
        finally
        {
            if (File.Exists(path)) File.Delete(path);
        }
    }

    [Fact]
    public async Task ExecuteAsync_OpenInEditorWithFileOutput_OpensWrittenFileOnce()
    {
        (CallGraphService service, _, FakeFileOpener opener) = CreateService(new StubReader(_ => OrderSource));
        string path = Path.Combine(Path.GetTempPath(), $"cast-calls-{Guid.NewGuid():N}.puml");

        try
        {
            ScaffoldStatus status = await service.ExecuteAsync(
                Request(outputPath: path, openInEditor: true), CancellationToken.None);

            Assert.Equal(ScaffoldStatus.Success, status);
            string opened = Assert.Single(opener.OpenedPaths);
            Assert.Equal(Path.GetFullPath(path), opened);
        }
        finally
        {
            if (File.Exists(path)) File.Delete(path);
        }
    }

    [Fact]
    public async Task ExecuteAsync_OpenInEditorWithStdOut_DoesNotOpen()
    {
        (CallGraphService service, _, FakeFileOpener opener) = CreateService(new StubReader(_ => OrderSource));

        ScaffoldStatus status = await service.ExecuteAsync(Request(openInEditor: true), CancellationToken.None);

        Assert.Equal(ScaffoldStatus.Success, status);
        Assert.Empty(opener.OpenedPaths);
    }

    [Fact]
    public async Task ExecuteAsync_PreCancelledToken_Throws()
    {
        (CallGraphService service, _, _) = CreateService(new StubReader(_ => OrderSource));
        using var cts = new CancellationTokenSource();
        cts.Cancel();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(
            () => service.ExecuteAsync(Request(), cts.Token));
    }

    [Fact]
    public async Task ExecuteAsync_CancelledDuringRead_ThrowsBeforeWriting()
    {
        // Not cancelled at entry, but cancelled while reading — the post-parse check must catch it.
        using var cts = new CancellationTokenSource();
        var reader = new StubReader(_ =>
        {
            cts.Cancel();
            return OrderSource;
        });
        (CallGraphService service, StringWriter stdout, _) = CreateService(reader);

        await Assert.ThrowsAnyAsync<OperationCanceledException>(
            () => service.ExecuteAsync(Request(), cts.Token));
        Assert.Equal(string.Empty, stdout.ToString());
    }
}
