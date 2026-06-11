using System;
using System.IO;
using Cast.Cli.Diagnostics;
using Cast.Cli.Services;

namespace Cast.Cli.Tests;

public sealed class FileSystemPumlFileLocatorTests : IDisposable
{
    private readonly string _root;
    private readonly FileSystemPumlFileLocator _locator = new();

    public FileSystemPumlFileLocatorTests()
    {
        _root = Path.Combine(Path.GetTempPath(), $"cast-locator-{Guid.NewGuid():N}");
        Directory.CreateDirectory(_root);
    }

    public void Dispose() => Directory.Delete(_root, recursive: true);

    private string CreateFile(string relativePath)
    {
        string fullPath = Path.Combine(_root, relativePath);
        Directory.CreateDirectory(Path.GetDirectoryName(fullPath)!);
        File.WriteAllText(fullPath, "@startuml\n@enduml\n");
        return fullPath;
    }

    [Fact]
    public void Locate_File_ReturnsJustThatFile()
    {
        string file = CreateFile("one.puml");

        Assert.Equal([file], _locator.Locate(file));
    }

    [Fact]
    public void Locate_NonPumlFile_IsAcceptedExplicitly()
    {
        string file = Path.Combine(_root, "diagram.txt");
        File.WriteAllText(file, "@startuml\n@enduml\n");

        Assert.Equal([file], _locator.Locate(file));
    }

    [Fact]
    public void Locate_Folder_FindsPumlFilesRecursively()
    {
        string a = CreateFile("a.puml");
        string nested = CreateFile(Path.Combine("sub", "deeper", "b.puml"));
        CreateFile(Path.Combine("sub", "ignored.txt")); // wrong extension

        var located = _locator.Locate(_root);

        Assert.Equal(2, located.Count);
        Assert.Contains(a, located);
        Assert.Contains(nested, located);
    }

    [Fact]
    public void Locate_EmptyFolder_ReturnsEmpty()
    {
        Assert.Empty(_locator.Locate(_root));
    }

    [Fact]
    public void Locate_MissingPath_Throws()
    {
        Assert.Throws<DiagramFormatException>(
            () => _locator.Locate(Path.Combine(_root, "does-not-exist")));
    }

    [Theory]
    [InlineData("")]
    [InlineData("bad\0path")] // an embedded NUL throws ArgumentException on every platform
    public void Locate_InvalidPathString_ThrowsDiagramFormatException(string path)
    {
        Assert.Throws<DiagramFormatException>(() => _locator.Locate(path));
    }

    [Fact]
    public void Locate_Folder_ReturnsPathsInOrdinalOrder()
    {
        // Ordinal sorts uppercase before lowercase ('Z' < 'b' < 's'), while NTFS enumerates
        // case-insensitively (b < sub < Zed), so an unsorted enumeration fails this test.
        string zed = CreateFile("Zed.puml");
        string b = CreateFile("b.puml");
        string nested = CreateFile(Path.Combine("sub", "a.puml"));

        Assert.Equal([zed, b, nested], _locator.Locate(_root));
    }
}
