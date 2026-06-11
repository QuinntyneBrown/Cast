using System;
using System.IO;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Cast.Cli.Models;
using Cast.Cli.Services;

namespace Cast.Cli.Tests;

public sealed class FileSystemTextFileEditorTests : IDisposable
{
    private readonly string _root;
    private readonly FileSystemTextFileEditor _editor = new();

    public FileSystemTextFileEditorTests()
    {
        _root = Path.Combine(Path.GetTempPath(), $"cast-editor-{Guid.NewGuid():N}");
        Directory.CreateDirectory(_root);
    }

    public void Dispose() => Directory.Delete(_root, recursive: true);

    private string PathFor(string name) => Path.Combine(_root, name);

    private async Task<string> WriteBytes(string name, byte[] bytes)
    {
        string path = PathFor(name);
        await File.WriteAllBytesAsync(path, bytes);
        return path;
    }

    [Theory]
    [InlineData(new byte[] { (byte)'h', (byte)'i' }, TextFileEncoding.Utf8)]
    [InlineData(new byte[] { 0xEF, 0xBB, 0xBF, (byte)'h', (byte)'i' }, TextFileEncoding.Utf8WithBom)]
    [InlineData(new byte[] { 0xFF, 0xFE, (byte)'h', 0, (byte)'i', 0 }, TextFileEncoding.Utf16LittleEndian)]
    [InlineData(new byte[] { 0xFE, 0xFF, 0, (byte)'h', 0, (byte)'i' }, TextFileEncoding.Utf16BigEndian)]
    [InlineData(new byte[] { 0xFF, 0xFE, 0, 0, (byte)'h', 0, 0, 0, (byte)'i', 0, 0, 0 }, TextFileEncoding.Utf32LittleEndian)]
    [InlineData(new byte[] { 0, 0, 0xFE, 0xFF, 0, 0, 0, (byte)'h', 0, 0, 0, (byte)'i' }, TextFileEncoding.Utf32BigEndian)]
    public async Task ReadAsync_DetectsEncodingAndDecodesWithoutBomCharacter(byte[] bytes, TextFileEncoding expected)
    {
        string path = await WriteBytes($"{expected}.puml", bytes);

        TextFile file = await _editor.ReadAsync(path, CancellationToken.None);

        Assert.Equal(expected, file.Encoding);
        Assert.Equal("hi", file.Content);
    }

    [Theory]
    [InlineData(TextFileEncoding.Utf8)]
    [InlineData(TextFileEncoding.Utf8WithBom)]
    [InlineData(TextFileEncoding.Utf16LittleEndian)]
    [InlineData(TextFileEncoding.Utf16BigEndian)]
    [InlineData(TextFileEncoding.Utf32LittleEndian)]
    [InlineData(TextFileEncoding.Utf32BigEndian)]
    public async Task WriteAsync_ThenReadAsync_RoundTripsContentAndEncoding(TextFileEncoding encoding)
    {
        string path = PathFor($"roundtrip-{encoding}.puml");
        var file = new TextFile(path, "@startuml\nA -> B : café ✓\n@enduml\n", encoding);

        await _editor.WriteAsync(file, CancellationToken.None);
        TextFile readBack = await _editor.ReadAsync(path, CancellationToken.None);

        Assert.Equal(file.Content, readBack.Content);
        Assert.Equal(encoding, readBack.Encoding);
    }

    [Fact]
    public async Task WriteAsync_UnchangedContent_ReproducesExactBytes()
    {
        byte[] original = [0xEF, 0xBB, 0xBF, .. Encoding.UTF8.GetBytes("@startuml\r\nA -> B : x\r\n@enduml")];
        string path = await WriteBytes("exact.puml", original);

        TextFile file = await _editor.ReadAsync(path, CancellationToken.None);
        await _editor.WriteAsync(file, CancellationToken.None);

        Assert.Equal(original, await File.ReadAllBytesAsync(path));
    }

    [Fact]
    public async Task ReadAsync_EmptyFile_IsUtf8WithEmptyContent()
    {
        string path = await WriteBytes("empty.puml", []);

        TextFile file = await _editor.ReadAsync(path, CancellationToken.None);

        Assert.Equal(TextFileEncoding.Utf8, file.Encoding);
        Assert.Equal(string.Empty, file.Content);
    }

    [Fact]
    public async Task ReadAsync_MissingFile_ThrowsFileNotFound()
    {
        await Assert.ThrowsAsync<FileNotFoundException>(
            () => _editor.ReadAsync(PathFor("missing.puml"), CancellationToken.None));
    }
}
