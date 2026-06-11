using System;
using System.Collections.Generic;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using Cast.Cli.Models;
using Cast.Cli.Services;
using Microsoft.Extensions.Logging.Abstractions;

namespace Cast.Cli.Tests;

public sealed class ExplorerServiceTests
{
    private sealed class StubTemplateStore : ITemplateStore
    {
        private readonly Func<string> _ensureRootDirectory;

        public StubTemplateStore(Func<string> ensureRootDirectory) => _ensureRootDirectory = ensureRootDirectory;

        public string EnsureRootDirectory() => _ensureRootDirectory();

        public Task SaveAsync(DiagramTemplate template, CancellationToken cancellationToken) =>
            throw new NotSupportedException();

        public Task<DiagramTemplate?> FindAsync(string name, CancellationToken cancellationToken) =>
            throw new NotSupportedException();

        public Task<IReadOnlyList<string>> ListAsync(CancellationToken cancellationToken) =>
            throw new NotSupportedException();

        public Task<bool> DeleteAsync(string name, CancellationToken cancellationToken) =>
            throw new NotSupportedException();
    }

    private sealed class FakeFolderOpener : IFolderOpener
    {
        public List<string> OpenedPaths { get; } = [];
        public IOException? ThrowOnOpen { get; set; }

        public void Open(string path)
        {
            if (ThrowOnOpen is not null)
            {
                throw ThrowOnOpen;
            }

            OpenedPaths.Add(path);
        }
    }

    [Fact]
    public async Task ExecuteAsync_OpensTheEnsuredTemplatesFolder_AndReturnsSuccess()
    {
        var opener = new FakeFolderOpener();
        var service = new ExplorerService(
            new StubTemplateStore(() => @"C:\some\templates"),
            opener,
            NullLogger<ExplorerService>.Instance);

        ScaffoldStatus status = await service.ExecuteAsync(CancellationToken.None);

        Assert.Equal(ScaffoldStatus.Success, status);
        Assert.Equal(@"C:\some\templates", Assert.Single(opener.OpenedPaths));
    }

    [Fact]
    public async Task ExecuteAsync_StoreUnavailable_ReturnsOutputError_WithoutOpening()
    {
        var opener = new FakeFolderOpener();
        var service = new ExplorerService(
            new StubTemplateStore(() => throw new IOException("no profile")),
            opener,
            NullLogger<ExplorerService>.Instance);

        ScaffoldStatus status = await service.ExecuteAsync(CancellationToken.None);

        Assert.Equal(ScaffoldStatus.OutputError, status);
        Assert.Empty(opener.OpenedPaths);
    }

    [Fact]
    public async Task ExecuteAsync_EditorLaunchFails_ReturnsOutputError()
    {
        var opener = new FakeFolderOpener { ThrowOnOpen = new IOException("code not found") };
        var service = new ExplorerService(
            new StubTemplateStore(() => @"C:\some\templates"),
            opener,
            NullLogger<ExplorerService>.Instance);

        ScaffoldStatus status = await service.ExecuteAsync(CancellationToken.None);

        Assert.Equal(ScaffoldStatus.OutputError, status);
    }

    [Fact]
    public async Task ExecuteAsync_PreCancelledToken_Throws()
    {
        var service = new ExplorerService(
            new StubTemplateStore(() => @"C:\some\templates"),
            new FakeFolderOpener(),
            NullLogger<ExplorerService>.Instance);
        using var cts = new CancellationTokenSource();
        cts.Cancel();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => service.ExecuteAsync(cts.Token));
    }
}
