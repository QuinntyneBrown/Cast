using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Cast.Cli.Models;
using Cast.Cli.Services;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

namespace Cast.Cli.Tests;

public sealed class StyleServiceTests : IDisposable
{
    private readonly string _root;

    public StyleServiceTests()
    {
        _root = Path.Combine(Path.GetTempPath(), $"cast-style-{Guid.NewGuid():N}");
        Directory.CreateDirectory(_root);
    }

    public void Dispose() => Directory.Delete(_root, recursive: true);

    /// <summary>Wraps the real editor, failing specific paths to exercise the error paths portably.</summary>
    private sealed class FaultInjectingEditor : ITextFileEditor
    {
        private readonly FileSystemTextFileEditor _inner = new();

        public HashSet<string> FailReads { get; } = new(StringComparer.OrdinalIgnoreCase);
        public HashSet<string> FailWrites { get; } = new(StringComparer.OrdinalIgnoreCase);

        public Task<TextFile> ReadAsync(string path, CancellationToken cancellationToken) =>
            FailReads.Contains(Path.GetFileName(path))
                ? throw new IOException($"Simulated read failure for '{path}'.")
                : _inner.ReadAsync(path, cancellationToken);

        public Task WriteAsync(TextFile file, CancellationToken cancellationToken) =>
            FailWrites.Contains(Path.GetFileName(file.Path))
                ? throw new IOException($"Simulated write failure for '{file.Path}'.")
                : _inner.WriteAsync(file, cancellationToken);
    }

    private sealed class CapturingLogger : ILogger<StyleService>
    {
        public List<string> Messages { get; } = [];

        public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;
        public bool IsEnabled(LogLevel logLevel) => true;

        public void Log<TState>(
            LogLevel logLevel, EventId eventId, TState state, Exception? exception,
            Func<TState, Exception?, string> formatter) => Messages.Add(formatter(state, exception));
    }

    private static StyleService CreateService(ITextFileEditor? editor = null, ILogger<StyleService>? logger = null) => new(
        new FileSystemPumlFileLocator(),
        editor ?? new FileSystemTextFileEditor(),
        new SequenceDiagramDetector(),
        new PlantUmlSequenceStyler(),
        logger ?? NullLogger<StyleService>.Instance);

    private string CreateFile(string relativePath, string content)
    {
        string fullPath = Path.Combine(_root, relativePath);
        Directory.CreateDirectory(Path.GetDirectoryName(fullPath)!);
        File.WriteAllText(fullPath, content);
        return fullPath;
    }

    private const string PlainSequence = "@startuml\nactor U\nparticipant A\nU -> A : go\n@enduml\n";
    private const string ClassDiagram = "@startuml\nclass Order {\n  +id : int\n}\n@enduml\n";

    private const string StyledSequence =
        "@startuml\n!pragma teoz true\nskinparam defaultFontSize 10\nactor U #63BEF2\n" +
        "box #PHYSICAL\n  box #AZURE\n    participant A #63BEF2\n  end box\nend box\nU -> A : go\n@enduml\n";

    [Fact]
    public async Task ExecuteAsync_SingleSequenceFile_IsRestyledInPlace()
    {
        string file = CreateFile("seq.puml", PlainSequence);

        ScaffoldStatus status = await CreateService().ExecuteAsync(
            new StyleRequest(file), CancellationToken.None);

        Assert.Equal(ScaffoldStatus.Success, status);
        string updated = await File.ReadAllTextAsync(file);
        Assert.Contains("!pragma teoz true", updated);
        Assert.Contains("skinparam defaultFontSize 10", updated);
        Assert.Contains("box #PHYSICAL\n  box #AZURE\n    participant A #63BEF2\n  end box\nend box", updated);
        Assert.Contains("actor U #63BEF2\nbox #PHYSICAL", updated); // actor outside the box
    }

    [Fact]
    public async Task ExecuteAsync_NonSequenceFile_IsLeftUntouched()
    {
        string file = CreateFile("class.puml", ClassDiagram);

        ScaffoldStatus status = await CreateService().ExecuteAsync(
            new StyleRequest(file), CancellationToken.None);

        Assert.Equal(ScaffoldStatus.Success, status);
        Assert.Equal(ClassDiagram, await File.ReadAllTextAsync(file));
    }

    [Fact]
    public async Task ExecuteAsync_Folder_RestylesOnlySequenceDiagramsRecursively()
    {
        string seq = CreateFile("a.puml", PlainSequence);
        string nested = CreateFile(Path.Combine("sub", "b.puml"), PlainSequence);
        string cls = CreateFile(Path.Combine("sub", "c.puml"), ClassDiagram);
        string text = CreateFile("notes.txt", "not a diagram");

        ScaffoldStatus status = await CreateService().ExecuteAsync(
            new StyleRequest(_root), CancellationToken.None);

        Assert.Equal(ScaffoldStatus.Success, status);
        Assert.Contains("!pragma teoz true", await File.ReadAllTextAsync(seq));
        Assert.Contains("!pragma teoz true", await File.ReadAllTextAsync(nested));
        Assert.Equal(ClassDiagram, await File.ReadAllTextAsync(cls));
        Assert.Equal("not a diagram", await File.ReadAllTextAsync(text));
    }

    [Fact]
    public async Task ExecuteAsync_SecondRun_IsIdempotent()
    {
        string file = CreateFile("seq.puml", PlainSequence);
        StyleService service = CreateService();

        await service.ExecuteAsync(new StyleRequest(file), CancellationToken.None);
        string afterFirst = await File.ReadAllTextAsync(file);
        DateTime stampAfterFirst = File.GetLastWriteTimeUtc(file);

        ScaffoldStatus status = await service.ExecuteAsync(new StyleRequest(file), CancellationToken.None);

        Assert.Equal(ScaffoldStatus.Success, status);
        Assert.Equal(afterFirst, await File.ReadAllTextAsync(file));
        Assert.Equal(stampAfterFirst, File.GetLastWriteTimeUtc(file)); // unchanged files are not rewritten
    }

    [Fact]
    public async Task ExecuteAsync_CustomColors_AreApplied()
    {
        string file = CreateFile("seq.puml", PlainSequence);

        await CreateService().ExecuteAsync(
            new StyleRequest(file, OuterBoxColor: "Gray", InnerBoxColor: "#White"), CancellationToken.None);

        string updated = await File.ReadAllTextAsync(file);
        Assert.Contains("box #Gray", updated);
        Assert.Contains("box #White", updated);
    }

    [Fact]
    public async Task ExecuteAsync_MissingPath_ReturnsInvalidInput()
    {
        ScaffoldStatus status = await CreateService().ExecuteAsync(
            new StyleRequest(Path.Combine(_root, "missing")), CancellationToken.None);

        Assert.Equal(ScaffoldStatus.InvalidInput, status);
    }

    [Fact]
    public async Task ExecuteAsync_InvalidColor_ReturnsInvalidInput_AndTouchesNothing()
    {
        string file = CreateFile("seq.puml", PlainSequence);

        ScaffoldStatus status = await CreateService().ExecuteAsync(
            new StyleRequest(file, OuterBoxColor: "light gray"), CancellationToken.None);

        Assert.Equal(ScaffoldStatus.InvalidInput, status);
        Assert.Equal(PlainSequence, await File.ReadAllTextAsync(file));
    }

    [Fact]
    public async Task ExecuteAsync_EmptyFolder_ReturnsSuccess()
    {
        ScaffoldStatus status = await CreateService().ExecuteAsync(
            new StyleRequest(_root), CancellationToken.None);

        Assert.Equal(ScaffoldStatus.Success, status);
    }

    [Fact]
    public async Task ExecuteAsync_PreCancelledToken_Throws()
    {
        using var cts = new CancellationTokenSource();
        cts.Cancel();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(
            () => CreateService().ExecuteAsync(new StyleRequest(_root), cts.Token));
    }

    // ----- error aggregation -----------------------------------------------------------------

    [Fact]
    public async Task ExecuteAsync_ReadFailureMidFolder_ReturnsInvalidInput_ButProcessesTheRest()
    {
        CreateFile("bad.puml", PlainSequence);
        string good = CreateFile("good.puml", PlainSequence);
        var editor = new FaultInjectingEditor();
        editor.FailReads.Add("bad.puml");

        ScaffoldStatus status = await CreateService(editor).ExecuteAsync(
            new StyleRequest(_root), CancellationToken.None);

        Assert.Equal(ScaffoldStatus.InvalidInput, status);
        Assert.Contains("!pragma teoz true", await File.ReadAllTextAsync(good)); // run continued
    }

    [Fact]
    public async Task ExecuteAsync_WriteFailure_ReturnsOutputError_ButProcessesTheRest()
    {
        string locked = CreateFile("locked.puml", PlainSequence);
        string good = CreateFile("ok.puml", PlainSequence);
        var editor = new FaultInjectingEditor();
        editor.FailWrites.Add("locked.puml");

        ScaffoldStatus status = await CreateService(editor).ExecuteAsync(
            new StyleRequest(_root), CancellationToken.None);

        Assert.Equal(ScaffoldStatus.OutputError, status);
        Assert.Equal(PlainSequence, await File.ReadAllTextAsync(locked)); // failed write left it alone
        Assert.Contains("!pragma teoz true", await File.ReadAllTextAsync(good));
    }

    [Fact]
    public async Task ExecuteAsync_WriteFailureOutranksReadFailure()
    {
        CreateFile("unreadable.puml", PlainSequence);
        CreateFile("unwritable.puml", PlainSequence);
        var editor = new FaultInjectingEditor();
        editor.FailReads.Add("unreadable.puml");
        editor.FailWrites.Add("unwritable.puml");

        ScaffoldStatus status = await CreateService(editor).ExecuteAsync(
            new StyleRequest(_root), CancellationToken.None);

        Assert.Equal(ScaffoldStatus.OutputError, status);
    }

    // ----- reporting ---------------------------------------------------------------------------

    [Fact]
    public async Task ExecuteAsync_LogsPerFileAndSummaryLines()
    {
        CreateFile("plain.puml", PlainSequence);
        CreateFile("styled.puml", StyledSequence);
        CreateFile("class.puml", ClassDiagram);
        CreateFile("multi.puml", "@startuml\nA -> B : x\n@enduml\n@startuml\nC -> D : y\n@enduml\n");
        var logger = new CapturingLogger();

        await CreateService(logger: logger).ExecuteAsync(new StyleRequest(_root), CancellationToken.None);

        Assert.Single(logger.Messages, m =>
            m.StartsWith("Updated") && m.EndsWith("added teoz pragma, font size, participant colors, participant boxes."));
        Assert.Single(logger.Messages, m => m.StartsWith("Unchanged") && m.Contains("already styled"));
        Assert.Single(logger.Messages, m => m.Contains("not a PlantUML sequence diagram"));
        Assert.Single(logger.Messages, m => m.Contains("it contains multiple @startuml blocks"));
        Assert.Contains(
            "Styled 1 file(s); 1 already styled; 1 skipped; 1 not sequence diagrams; 0 failed; 4 scanned.",
            logger.Messages);
    }

    [Fact]
    public async Task ExecuteAsync_MultiDiagramFile_IsLeftUntouched()
    {
        string content = "@startuml\nA -> B : x\n@enduml\n@startuml\nC -> D : y\n@enduml\n";
        string file = CreateFile("multi.puml", content);

        ScaffoldStatus status = await CreateService().ExecuteAsync(
            new StyleRequest(file), CancellationToken.None);

        Assert.Equal(ScaffoldStatus.Success, status);
        Assert.Equal(content, await File.ReadAllTextAsync(file));
    }

    // ----- byte-level fidelity -------------------------------------------------------------------

    [Fact]
    public async Task ExecuteAsync_CrlfFileWithoutTrailingNewline_KeepsLineEndingsOnDisk()
    {
        string file = Path.Combine(_root, "crlf.puml");
        await File.WriteAllBytesAsync(file, Encoding.UTF8.GetBytes(
            "@startuml\r\nparticipant A\r\nA -> A : x\r\n@enduml"));

        ScaffoldStatus status = await CreateService().ExecuteAsync(
            new StyleRequest(file), CancellationToken.None);

        Assert.Equal(ScaffoldStatus.Success, status);
        byte[] bytes = await File.ReadAllBytesAsync(file);
        for (int i = 0; i < bytes.Length; i++)
        {
            if (bytes[i] == (byte)'\n')
            {
                Assert.True(i > 0 && bytes[i - 1] == (byte)'\r', $"bare LF at byte offset {i}");
            }
        }

        Assert.NotEqual((byte)'\n', bytes[^1]); // still no trailing newline
        Assert.Contains("!pragma teoz true", Encoding.UTF8.GetString(bytes)); // and it was really rewritten
    }

    [Fact]
    public async Task ExecuteAsync_Utf8BomFile_KeepsItsBom()
    {
        string file = Path.Combine(_root, "bom.puml");
        await File.WriteAllBytesAsync(file, [0xEF, 0xBB, 0xBF, .. Encoding.UTF8.GetBytes(PlainSequence)]);

        ScaffoldStatus status = await CreateService().ExecuteAsync(
            new StyleRequest(file), CancellationToken.None);

        Assert.Equal(ScaffoldStatus.Success, status);
        byte[] bytes = await File.ReadAllBytesAsync(file);
        Assert.Equal([0xEF, 0xBB, 0xBF], bytes[..3]);
        Assert.Contains("!pragma teoz true", Encoding.UTF8.GetString(bytes));
    }

    [Fact]
    public async Task ExecuteAsync_Utf16LeFile_StaysUtf16Le()
    {
        string file = Path.Combine(_root, "utf16.puml");
        await File.WriteAllTextAsync(file, PlainSequence, Encoding.Unicode); // writes the FF FE BOM

        ScaffoldStatus status = await CreateService().ExecuteAsync(
            new StyleRequest(file), CancellationToken.None);

        Assert.Equal(ScaffoldStatus.Success, status);
        byte[] bytes = await File.ReadAllBytesAsync(file);
        Assert.Equal([0xFF, 0xFE], bytes[..2]);
        Assert.Contains("!pragma teoz true", Encoding.Unicode.GetString(bytes, 2, bytes.Length - 2));
    }

    [Fact]
    public async Task ExecuteAsync_BomlessUtf8File_StaysBomless()
    {
        string file = CreateFile("plain.puml", PlainSequence);

        await CreateService().ExecuteAsync(new StyleRequest(file), CancellationToken.None);

        byte[] bytes = await File.ReadAllBytesAsync(file);
        Assert.False(bytes.Length >= 3 && bytes[0] == 0xEF && bytes[1] == 0xBB && bytes[2] == 0xBF,
            "file must not gain a UTF-8 BOM");
    }
}
