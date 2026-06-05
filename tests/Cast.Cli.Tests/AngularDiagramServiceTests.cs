using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using Cast.Cli.Models;
using Cast.Cli.Services;
using Microsoft.Extensions.Logging.Abstractions;

namespace Cast.Cli.Tests;

public sealed class AngularDiagramServiceTests
{
    private sealed class StubReader : ISourceFileReader
    {
        private readonly Func<string, string> _read;

        public StubReader(Func<string, string> read) => _read = read;

        public Task<string> ReadAsync(string path, CancellationToken cancellationToken) =>
            Task.FromResult(_read(path));
    }

    private const string DashboardSource = """
        import { HttpClient } from '@angular/common/http';
        import { Injectable, inject } from '@angular/core';
        import { API_BASE_URL } from '../api-base-url.token';

        @Injectable({ providedIn: 'root' })
        export class DashboardService {
          private readonly http = inject(HttpClient);
          private readonly baseUrl = inject(API_BASE_URL);
        }
        """;

    private static (AngularDiagramService Service, StringWriter StdOut) CreateService(ISourceFileReader reader)
    {
        var stdout = new StringWriter();
        var service = new AngularDiagramService(
            reader,
            new AngularServiceParser(),
            new PlantUmlAngularDiagramRenderer(),
            new FileSystemDiagramWriter(stdout),
            NullLogger<AngularDiagramService>.Instance);

        return (service, stdout);
    }

    private static AngularDiagramRequest Request(
        string path = "dashboard.service.ts",
        string? title = null,
        string? outputPath = null,
        bool force = false) =>
        new(path, title, outputPath, force);

    [Fact]
    public async Task ExecuteAsync_ValidService_WritesDiagramToStdOut_AndReturnsSuccess()
    {
        (AngularDiagramService service, StringWriter stdout) = CreateService(new StubReader(_ => DashboardSource));

        ScaffoldStatus status = await service.ExecuteAsync(Request(), CancellationToken.None);

        Assert.Equal(ScaffoldStatus.Success, status);
        string output = stdout.ToString();
        Assert.Contains("@startuml", output);
        Assert.Contains("title How Angular injects dependencies into DashboardService", output);
        Assert.Contains("participant \"HttpClient\\n(injected service)\" as D1", output);
        Assert.Contains("participant \"API_BASE_URL\\n(injected token)\" as D2", output);
    }

    [Fact]
    public async Task ExecuteAsync_CustomTitle_IsApplied()
    {
        (AngularDiagramService service, StringWriter stdout) = CreateService(new StubReader(_ => DashboardSource));

        await service.ExecuteAsync(Request(title: "Custom"), CancellationToken.None);

        Assert.Contains("title Custom\n", stdout.ToString());
    }

    [Fact]
    public async Task ExecuteAsync_FileNotFound_ReturnsInvalidInput()
    {
        (AngularDiagramService service, _) = CreateService(
            new StubReader(path => throw new FileNotFoundException("missing", path)));

        ScaffoldStatus status = await service.ExecuteAsync(Request(), CancellationToken.None);

        Assert.Equal(ScaffoldStatus.InvalidInput, status);
    }

    [Fact]
    public async Task ExecuteAsync_UnparseableSource_ReturnsInvalidInput()
    {
        (AngularDiagramService service, _) = CreateService(new StubReader(_ => "export const x = 1;"));

        ScaffoldStatus status = await service.ExecuteAsync(Request(), CancellationToken.None);

        Assert.Equal(ScaffoldStatus.InvalidInput, status);
    }

    [Fact]
    public async Task ExecuteAsync_ExistingOutputWithoutForce_ReturnsOutputError()
    {
        (AngularDiagramService service, _) = CreateService(new StubReader(_ => DashboardSource));
        string path = Path.Combine(Path.GetTempPath(), $"cast-ng-{Guid.NewGuid():N}.puml");
        await File.WriteAllTextAsync(path, "existing");

        try
        {
            ScaffoldStatus status = await service.ExecuteAsync(
                Request(outputPath: path, force: false), CancellationToken.None);

            Assert.Equal(ScaffoldStatus.OutputError, status);
            Assert.Equal("existing", await File.ReadAllTextAsync(path)); // untouched
        }
        finally
        {
            if (File.Exists(path))
            {
                File.Delete(path);
            }
        }
    }

    [Fact]
    public async Task ExecuteAsync_PreCancelledToken_Throws()
    {
        (AngularDiagramService service, _) = CreateService(new StubReader(_ => DashboardSource));
        using var cts = new CancellationTokenSource();
        cts.Cancel();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(
            () => service.ExecuteAsync(Request(), cts.Token));
    }

    [Fact]
    public async Task ExecuteAsync_OutputPathWithForce_OverwritesFileAndReturnsSuccess()
    {
        (AngularDiagramService service, _) = CreateService(new StubReader(_ => DashboardSource));
        string path = Path.Combine(Path.GetTempPath(), $"cast-ng-{Guid.NewGuid():N}.puml");
        await File.WriteAllTextAsync(path, "existing");

        try
        {
            ScaffoldStatus status = await service.ExecuteAsync(
                Request(outputPath: path, force: true), CancellationToken.None);

            Assert.Equal(ScaffoldStatus.Success, status);
            string written = await File.ReadAllTextAsync(path);
            Assert.StartsWith("@startuml", written);
            Assert.Contains("DashboardService", written);
        }
        finally
        {
            if (File.Exists(path))
            {
                File.Delete(path);
            }
        }
    }

    [Fact]
    public async Task ExecuteAsync_TitleWithControlChar_ReturnsInvalidInput()
    {
        (AngularDiagramService service, StringWriter stdout) = CreateService(new StubReader(_ => DashboardSource));

        ScaffoldStatus status = await service.ExecuteAsync(
            Request(title: "line one\nDI -> Consumer : injected"), CancellationToken.None);

        Assert.Equal(ScaffoldStatus.InvalidInput, status);
        Assert.Equal(string.Empty, stdout.ToString()); // nothing emitted
    }
}
