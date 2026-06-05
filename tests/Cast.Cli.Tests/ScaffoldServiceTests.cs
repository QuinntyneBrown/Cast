using System;
using System.Collections.Generic;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using Cast.Cli.Hosting;
using Cast.Cli.Models;
using Cast.Cli.Services;
using Microsoft.Extensions.Logging.Abstractions;

namespace Cast.Cli.Tests;

public sealed class ScaffoldServiceTests
{
    private static (ScaffoldService Service, StringWriter StdOut) CreateService()
    {
        var catalog = new ParticipantKindCatalog();
        var stdout = new StringWriter();

        var service = new ScaffoldService(
            new ParticipantParser(catalog),
            new MessageParser(),
            new SequentialSampleFlowGenerator(),
            new PlantUmlSequenceRenderer(catalog),
            new FileSystemDiagramWriter(stdout),
            NullLogger<ScaffoldService>.Instance);

        return (service, stdout);
    }

    private static ScaffoldRequest Request(
        IReadOnlyList<string> participants,
        IReadOnlyList<string>? messages = null,
        string? outputPath = null,
        bool force = false,
        bool includeSampleFlow = true,
        string? title = null,
        bool autoNumber = false,
        string? theme = null) =>
        new(participants, messages ?? [], title, autoNumber, theme, outputPath, force, includeSampleFlow);

    [Fact]
    public async Task ExecuteAsync_ValidInput_WritesDiagramToStdOut_AndReturnsSuccess()
    {
        (ScaffoldService service, StringWriter stdout) = CreateService();

        int exit = await service.ExecuteAsync(
            Request(["actor:User", "OS:Order Service"], ["User -> OS : place order"]),
            CancellationToken.None);

        Assert.Equal(ExitCode.Success, exit);
        string output = stdout.ToString();
        Assert.Contains("@startuml", output);
        Assert.Contains("actor User", output);
        Assert.Contains("participant \"Order Service\" as OS", output);
        Assert.Contains("User -> OS : place order", output);
    }

    [Fact]
    public async Task ExecuteAsync_NoMessagesWithSampleFlow_EmitsPlaceholderFlow()
    {
        (ScaffoldService service, StringWriter stdout) = CreateService();

        int exit = await service.ExecuteAsync(
            Request(["A", "B"], includeSampleFlow: true),
            CancellationToken.None);

        Assert.Equal(ExitCode.Success, exit);
        string output = stdout.ToString();
        Assert.Contains("A -> B", output);
        Assert.Contains("B --> A", output);
    }

    [Fact]
    public async Task ExecuteAsync_NoMessagesWithoutSampleFlow_EmitsOnlyParticipants()
    {
        (ScaffoldService service, StringWriter stdout) = CreateService();

        int exit = await service.ExecuteAsync(
            Request(["A", "B"], includeSampleFlow: false),
            CancellationToken.None);

        Assert.Equal(ExitCode.Success, exit);
        string output = stdout.ToString();
        Assert.DoesNotContain("->", output);
    }

    [Fact]
    public async Task ExecuteAsync_DuplicateAlias_ReturnsUsageError()
    {
        (ScaffoldService service, _) = CreateService();

        int exit = await service.ExecuteAsync(
            Request(["A", "A"]),
            CancellationToken.None);

        Assert.Equal(ExitCode.UsageError, exit);
    }

    [Fact]
    public async Task ExecuteAsync_MessageReferencingUnknownParticipant_ReturnsUsageError()
    {
        (ScaffoldService service, _) = CreateService();

        int exit = await service.ExecuteAsync(
            Request(["A", "B"], ["A -> Z : oops"]),
            CancellationToken.None);

        Assert.Equal(ExitCode.UsageError, exit);
    }

    [Fact]
    public async Task ExecuteAsync_OutputToFile_WritesFile()
    {
        (ScaffoldService service, _) = CreateService();
        string path = Path.Combine(Path.GetTempPath(), $"cast-test-{Guid.NewGuid():N}.puml");

        try
        {
            int exit = await service.ExecuteAsync(
                Request(["A", "B"], ["A -> B : hi"], outputPath: path),
                CancellationToken.None);

            Assert.Equal(ExitCode.Success, exit);
            Assert.True(File.Exists(path));
            Assert.Contains("A -> B : hi", await File.ReadAllTextAsync(path));
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
    public async Task ExecuteAsync_ExistingFileWithoutForce_ReturnsIoError()
    {
        (ScaffoldService service, _) = CreateService();
        string path = Path.Combine(Path.GetTempPath(), $"cast-test-{Guid.NewGuid():N}.puml");
        await File.WriteAllTextAsync(path, "existing");

        try
        {
            int exit = await service.ExecuteAsync(
                Request(["A", "B"], ["A -> B : hi"], outputPath: path, force: false),
                CancellationToken.None);

            Assert.Equal(ExitCode.IoError, exit);
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
    public async Task ExecuteAsync_ExistingFileWithForce_Overwrites()
    {
        (ScaffoldService service, _) = CreateService();
        string path = Path.Combine(Path.GetTempPath(), $"cast-test-{Guid.NewGuid():N}.puml");
        await File.WriteAllTextAsync(path, "existing");

        try
        {
            int exit = await service.ExecuteAsync(
                Request(["A", "B"], ["A -> B : hi"], outputPath: path, force: true),
                CancellationToken.None);

            Assert.Equal(ExitCode.Success, exit);
            Assert.Contains("@startuml", await File.ReadAllTextAsync(path));
        }
        finally
        {
            if (File.Exists(path))
            {
                File.Delete(path);
            }
        }
    }
}
