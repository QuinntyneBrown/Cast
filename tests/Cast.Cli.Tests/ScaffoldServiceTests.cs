using System;
using System.Collections.Generic;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using Cast.Cli.Models;
using Cast.Cli.Services;
using Cast.Core.Services;
using Microsoft.Extensions.Logging.Abstractions;

namespace Cast.Cli.Tests;

public sealed class ScaffoldServiceTests
{
    private sealed class FakeFileOpener : IFileOpener
    {
        public List<string> OpenedPaths { get; } = [];

        public void Open(string path) => OpenedPaths.Add(path);
    }

    private static (ScaffoldService Service, StringWriter StdOut, FakeFileOpener Opener) CreateService()
    {
        var catalog = new ParticipantKindCatalog();
        var stdout = new StringWriter();
        var opener = new FakeFileOpener();

        var service = new ScaffoldService(
            new DiagramSpecValidator(new ParticipantParser(catalog), new MessageParser()),
            new SequentialSampleFlowGenerator(),
            new PlantUmlSequenceRenderer(catalog),
            new FileSystemDiagramWriter(stdout),
            opener,
            NullLogger<ScaffoldService>.Instance);

        return (service, stdout, opener);
    }

    private static ScaffoldRequest Request(
        IReadOnlyList<string> participants,
        IReadOnlyList<string>? messages = null,
        string? outputPath = null,
        bool force = false,
        bool includeSampleFlow = true,
        string? title = null,
        bool autoNumber = false,
        string? theme = null,
        string? outerBoxColor = null,
        string? innerBoxColor = null,
        bool openInEditor = false) =>
        new(participants, messages ?? [], title, autoNumber, theme, outputPath, force, includeSampleFlow,
            outerBoxColor, innerBoxColor, openInEditor);

    [Fact]
    public async Task ExecuteAsync_ValidInput_WritesDiagramToStdOut_AndReturnsSuccess()
    {
        (ScaffoldService service, StringWriter stdout, _) = CreateService();

        ScaffoldStatus status = await service.ExecuteAsync(
            Request(["actor:User", "OS:Order Service"], ["User -> OS : place order"]),
            CancellationToken.None);

        Assert.Equal(ScaffoldStatus.Success, status);
        string output = stdout.ToString();
        Assert.Contains("@startuml", output);
        Assert.Contains("actor User", output);
        Assert.Contains("participant \"Order Service\" as OS", output);
        Assert.Contains("User -> OS : place order", output);
    }

    [Fact]
    public async Task ExecuteAsync_NoMessagesWithSampleFlow_EmitsPlaceholderFlow()
    {
        (ScaffoldService service, StringWriter stdout, _) = CreateService();

        ScaffoldStatus status = await service.ExecuteAsync(
            Request(["A", "B"], includeSampleFlow: true),
            CancellationToken.None);

        Assert.Equal(ScaffoldStatus.Success, status);
        string output = stdout.ToString();
        Assert.Contains("A -> B", output);
        Assert.Contains("B --> A", output);
    }

    [Fact]
    public async Task ExecuteAsync_NoMessagesWithoutSampleFlow_EmitsOnlyParticipants()
    {
        (ScaffoldService service, StringWriter stdout, _) = CreateService();

        ScaffoldStatus status = await service.ExecuteAsync(
            Request(["A", "B"], includeSampleFlow: false),
            CancellationToken.None);

        Assert.Equal(ScaffoldStatus.Success, status);
        Assert.DoesNotContain("->", stdout.ToString());
    }

    [Fact]
    public async Task ExecuteAsync_DuplicateAlias_ReturnsInvalidInput()
    {
        (ScaffoldService service, _, _) = CreateService();

        ScaffoldStatus status = await service.ExecuteAsync(Request(["A", "A"]), CancellationToken.None);

        Assert.Equal(ScaffoldStatus.InvalidInput, status);
    }

    [Fact]
    public async Task ExecuteAsync_MessageReferencingUnknownParticipant_ReturnsInvalidInput()
    {
        (ScaffoldService service, _, _) = CreateService();

        ScaffoldStatus status = await service.ExecuteAsync(
            Request(["A", "B"], ["A -> Z : oops"]),
            CancellationToken.None);

        Assert.Equal(ScaffoldStatus.InvalidInput, status);
    }

    [Fact]
    public async Task ExecuteAsync_AliasesAreCaseSensitive()
    {
        (ScaffoldService service, StringWriter stdout, _) = CreateService();

        // 'A' and 'a' are distinct lifelines (Ordinal), matching PlantUML's case sensitivity.
        ScaffoldStatus status = await service.ExecuteAsync(
            Request(["A", "a"], ["A -> a : call"]),
            CancellationToken.None);

        Assert.Equal(ScaffoldStatus.Success, status);
        Assert.Contains("A -> a : call", stdout.ToString());
    }

    [Fact]
    public async Task ExecuteAsync_BoxColors_FlowIntoRenderedOutput()
    {
        (ScaffoldService service, StringWriter stdout, _) = CreateService();

        ScaffoldStatus status = await service.ExecuteAsync(
            Request(["actor:User", "OS"], outerBoxColor: "LightGray", innerBoxColor: "#White"),
            CancellationToken.None);

        Assert.Equal(ScaffoldStatus.Success, status);
        string output = stdout.ToString();
        Assert.Contains("box #LightGray", output);
        Assert.Contains("box #White", output);
    }

    [Fact]
    public async Task ExecuteAsync_BoxColorWithWhitespace_ReturnsInvalidInput()
    {
        (ScaffoldService service, _, _) = CreateService();

        ScaffoldStatus status = await service.ExecuteAsync(
            Request(["A", "B"], outerBoxColor: "light gray"),
            CancellationToken.None);

        Assert.Equal(ScaffoldStatus.InvalidInput, status);
    }

    [Theory]
    [InlineData("Bad\nTitle", null)]   // newline in title
    [InlineData(null, "my theme")]     // whitespace in theme
    public async Task ExecuteAsync_InvalidMetadata_ReturnsInvalidInput(string? title, string? theme)
    {
        (ScaffoldService service, _, _) = CreateService();

        ScaffoldStatus status = await service.ExecuteAsync(
            Request(["A", "B"], ["A -> B : x"], title: title, theme: theme),
            CancellationToken.None);

        Assert.Equal(ScaffoldStatus.InvalidInput, status);
    }

    [Fact]
    public async Task ExecuteAsync_PreCancelledToken_Throws()
    {
        (ScaffoldService service, _, _) = CreateService();
        using var cts = new CancellationTokenSource();
        cts.Cancel();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(
            () => service.ExecuteAsync(Request(["A", "B"]), cts.Token));
    }

    [Fact]
    public async Task ExecuteAsync_OutputToFile_WritesUtf8NoBom_ExactContent()
    {
        (ScaffoldService service, _, _) = CreateService();
        string path = Path.Combine(Path.GetTempPath(), $"cast-test-{Guid.NewGuid():N}.puml");

        try
        {
            ScaffoldStatus status = await service.ExecuteAsync(
                Request(["A", "B"], ["A -> B : hi"], outputPath: path),
                CancellationToken.None);

            Assert.Equal(ScaffoldStatus.Success, status);
            Assert.True(File.Exists(path));

            byte[] bytes = await File.ReadAllBytesAsync(path);
            Assert.False(bytes.Length >= 3 && bytes[0] == 0xEF && bytes[1] == 0xBB && bytes[2] == 0xBF,
                "file must not start with a UTF-8 BOM");

            string text = await File.ReadAllTextAsync(path);
            Assert.StartsWith("@startuml", text);
            Assert.EndsWith("@enduml\n", text);
            Assert.DoesNotContain("\r", text);
            Assert.Contains("A -> B : hi", text);
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
    public async Task ExecuteAsync_ExistingFileWithoutForce_ReturnsOutputError()
    {
        (ScaffoldService service, _, _) = CreateService();
        string path = Path.Combine(Path.GetTempPath(), $"cast-test-{Guid.NewGuid():N}.puml");
        await File.WriteAllTextAsync(path, "existing");

        try
        {
            ScaffoldStatus status = await service.ExecuteAsync(
                Request(["A", "B"], ["A -> B : hi"], outputPath: path, force: false),
                CancellationToken.None);

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
    public async Task ExecuteAsync_OpenInEditorWithFileOutput_OpensWrittenFileOnce()
    {
        (ScaffoldService service, _, FakeFileOpener opener) = CreateService();
        string path = Path.Combine(Path.GetTempPath(), $"cast-test-{Guid.NewGuid():N}.puml");

        try
        {
            ScaffoldStatus status = await service.ExecuteAsync(
                Request(["A", "B"], ["A -> B : hi"], outputPath: path, openInEditor: true),
                CancellationToken.None);

            Assert.Equal(ScaffoldStatus.Success, status);
            string opened = Assert.Single(opener.OpenedPaths);
            Assert.Equal(Path.GetFullPath(path), opened);
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
    public async Task ExecuteAsync_OpenInEditorWithStdOut_DoesNotOpen()
    {
        (ScaffoldService service, _, FakeFileOpener opener) = CreateService();

        ScaffoldStatus status = await service.ExecuteAsync(
            Request(["A", "B"], ["A -> B : hi"], openInEditor: true),
            CancellationToken.None);

        Assert.Equal(ScaffoldStatus.Success, status);
        Assert.Empty(opener.OpenedPaths);
    }

    [Fact]
    public async Task ExecuteAsync_OpenInEditorDefaultsOff_DoesNotOpen()
    {
        (ScaffoldService service, _, FakeFileOpener opener) = CreateService();
        string path = Path.Combine(Path.GetTempPath(), $"cast-test-{Guid.NewGuid():N}.puml");

        try
        {
            await service.ExecuteAsync(
                Request(["A", "B"], ["A -> B : hi"], outputPath: path),
                CancellationToken.None);

            Assert.Empty(opener.OpenedPaths);
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
    public async Task ExecuteAsync_OpenInEditorWithFailedWrite_DoesNotOpen()
    {
        (ScaffoldService service, _, FakeFileOpener opener) = CreateService();
        string path = Path.Combine(Path.GetTempPath(), $"cast-test-{Guid.NewGuid():N}.puml");
        await File.WriteAllTextAsync(path, "existing");

        try
        {
            ScaffoldStatus status = await service.ExecuteAsync(
                Request(["A", "B"], ["A -> B : hi"], outputPath: path, force: false, openInEditor: true),
                CancellationToken.None);

            Assert.Equal(ScaffoldStatus.OutputError, status);
            Assert.Empty(opener.OpenedPaths);
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
        (ScaffoldService service, _, _) = CreateService();
        string path = Path.Combine(Path.GetTempPath(), $"cast-test-{Guid.NewGuid():N}.puml");
        await File.WriteAllTextAsync(path, "existing");

        try
        {
            ScaffoldStatus status = await service.ExecuteAsync(
                Request(["A", "B"], ["A -> B : hi"], outputPath: path, force: true),
                CancellationToken.None);

            Assert.Equal(ScaffoldStatus.Success, status);
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
