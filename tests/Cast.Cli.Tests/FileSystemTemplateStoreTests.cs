using System;
using System.Collections.Generic;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using Cast.Cli.Diagnostics;
using Cast.Cli.Models;
using Cast.Cli.Services;

namespace Cast.Cli.Tests;

public sealed class FileSystemTemplateStoreTests
{
    private static string TempRoot() =>
        Path.Combine(Path.GetTempPath(), $"cast-tpl-{Guid.NewGuid():N}");

    private static void Cleanup(string root)
    {
        if (Directory.Exists(root))
        {
            Directory.Delete(root, recursive: true);
        }
    }

    private static DiagramTemplate Template(string name = "acme-ordering") => new()
    {
        Name = name,
        Participants = ["actor:User", "OS:Order Service"],
        Messages = ["User -> OS : place order"],
        Title = "Acme ordering",
        AutoNumber = true,
        Theme = "plain",
        OuterBoxColor = "#LightGray",
        InnerBoxColor = "#White",
    };

    [Fact]
    public async Task SaveAsync_CreatesDirectoryAndIndentedCamelCaseJson()
    {
        string root = TempRoot();
        var store = new FileSystemTemplateStore(root);

        try
        {
            await store.SaveAsync(Template(), CancellationToken.None);

            string path = Path.Combine(root, "acme-ordering.json");
            Assert.True(File.Exists(path));

            string json = await File.ReadAllTextAsync(path);
            Assert.Contains("\"name\": \"acme-ordering\"", json);
            Assert.Contains("\"participants\"", json);
            Assert.Contains("User -> OS : place order", json); // '>' not escaped to >
            Assert.Contains(Environment.NewLine, json); // indented, not minified
        }
        finally
        {
            Cleanup(root);
        }
    }

    [Fact]
    public async Task SaveAsync_ThenFindAsync_RoundTripsAllProperties()
    {
        string root = TempRoot();
        var store = new FileSystemTemplateStore(root);

        try
        {
            DiagramTemplate saved = Template();
            await store.SaveAsync(saved, CancellationToken.None);

            DiagramTemplate? loaded = await store.FindAsync("acme-ordering", CancellationToken.None);

            Assert.NotNull(loaded);
            Assert.Equal(saved.Name, loaded.Name);
            Assert.Equal(saved.Participants, loaded.Participants);
            Assert.Equal(saved.Messages, loaded.Messages);
            Assert.Equal(saved.Title, loaded.Title);
            Assert.Equal(saved.AutoNumber, loaded.AutoNumber);
            Assert.Equal(saved.Theme, loaded.Theme);
            Assert.Equal(saved.OuterBoxColor, loaded.OuterBoxColor);
            Assert.Equal(saved.InnerBoxColor, loaded.InnerBoxColor);
        }
        finally
        {
            Cleanup(root);
        }
    }

    [Fact]
    public async Task SaveAsync_ExistingName_Overwrites()
    {
        string root = TempRoot();
        var store = new FileSystemTemplateStore(root);

        try
        {
            await store.SaveAsync(Template() with { Title = "first" }, CancellationToken.None);
            await store.SaveAsync(Template() with { Title = "second" }, CancellationToken.None);

            DiagramTemplate? loaded = await store.FindAsync("acme-ordering", CancellationToken.None);

            Assert.Equal("second", loaded?.Title);
        }
        finally
        {
            Cleanup(root);
        }
    }

    [Fact]
    public async Task FindAsync_MissingTemplate_ReturnsNull()
    {
        string root = TempRoot();
        var store = new FileSystemTemplateStore(root);

        try
        {
            Assert.Null(await store.FindAsync("nope", CancellationToken.None));
        }
        finally
        {
            Cleanup(root);
        }
    }

    [Fact]
    public async Task FindAsync_FileOmittingOptionalProperties_LoadsWithDefaults()
    {
        string root = TempRoot();
        var store = new FileSystemTemplateStore(root);

        try
        {
            Directory.CreateDirectory(root);
            await File.WriteAllTextAsync(
                Path.Combine(root, "minimal.json"),
                """{ "name": "minimal", "participants": ["A"] }""");

            DiagramTemplate? loaded = await store.FindAsync("minimal", CancellationToken.None);

            Assert.NotNull(loaded);
            Assert.Equal(["A"], loaded.Participants);
            Assert.Empty(loaded.Messages);
            Assert.Null(loaded.Title);
            Assert.False(loaded.AutoNumber);
        }
        finally
        {
            Cleanup(root);
        }
    }

    [Fact]
    public async Task FindAsync_CorruptJson_ThrowsDiagramFormatException()
    {
        string root = TempRoot();
        var store = new FileSystemTemplateStore(root);

        try
        {
            Directory.CreateDirectory(root);
            await File.WriteAllTextAsync(Path.Combine(root, "broken.json"), "{ not json");

            await Assert.ThrowsAsync<DiagramFormatException>(
                () => store.FindAsync("broken", CancellationToken.None));
        }
        finally
        {
            Cleanup(root);
        }
    }

    [Fact]
    public async Task ListAsync_MissingDirectory_ReturnsEmpty()
    {
        var store = new FileSystemTemplateStore(TempRoot());

        Assert.Empty(await store.ListAsync(CancellationToken.None));
    }

    [Fact]
    public async Task ListAsync_ReturnsOrdinalSortedNames()
    {
        string root = TempRoot();
        var store = new FileSystemTemplateStore(root);

        try
        {
            await store.SaveAsync(Template("zeta"), CancellationToken.None);
            await store.SaveAsync(Template("alpha"), CancellationToken.None);
            await store.SaveAsync(Template("Beta"), CancellationToken.None);

            IReadOnlyList<string> names = await store.ListAsync(CancellationToken.None);

            Assert.Equal(["Beta", "alpha", "zeta"], names); // ordinal: uppercase first
        }
        finally
        {
            Cleanup(root);
        }
    }

    [Fact]
    public async Task DeleteAsync_ExistingTemplate_ReturnsTrueAndRemovesFile()
    {
        string root = TempRoot();
        var store = new FileSystemTemplateStore(root);

        try
        {
            await store.SaveAsync(Template(), CancellationToken.None);

            Assert.True(await store.DeleteAsync("acme-ordering", CancellationToken.None));
            Assert.False(File.Exists(Path.Combine(root, "acme-ordering.json")));
        }
        finally
        {
            Cleanup(root);
        }
    }

    [Fact]
    public async Task DeleteAsync_MissingTemplate_ReturnsFalse()
    {
        string root = TempRoot();
        var store = new FileSystemTemplateStore(root);

        try
        {
            Assert.False(await store.DeleteAsync("nope", CancellationToken.None));
        }
        finally
        {
            Cleanup(root);
        }
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("../evil")]
    [InlineData("a/b")]
    [InlineData(@"a\b")]
    [InlineData("nul")]
    [InlineData("NUL.orders")]
    [InlineData("com1")]
    [InlineData("name.")]
    [InlineData(".")]
    [InlineData("-leading-dash")]
    public async Task InvalidName_ThrowsDiagramFormatException(string name)
    {
        string root = TempRoot();
        var store = new FileSystemTemplateStore(root);

        try
        {
            await Assert.ThrowsAsync<DiagramFormatException>(
                () => store.SaveAsync(Template() with { Name = name }, CancellationToken.None));
            await Assert.ThrowsAsync<DiagramFormatException>(
                () => store.FindAsync(name, CancellationToken.None));
            await Assert.ThrowsAsync<DiagramFormatException>(
                () => store.DeleteAsync(name, CancellationToken.None));
            Assert.False(Directory.Exists(root)); // nothing was ever written
        }
        finally
        {
            Cleanup(root);
        }
    }

    [Fact]
    public void EnsureRootDirectory_CreatesMissingFolder_AndReturnsItsPath()
    {
        string root = TempRoot();
        var store = new FileSystemTemplateStore(root);

        try
        {
            string ensured = store.EnsureRootDirectory();

            Assert.Equal(root, ensured);
            Assert.True(Directory.Exists(root));
        }
        finally
        {
            Cleanup(root);
        }
    }

    [Fact]
    public void EnsureRootDirectory_UnresolvableRoot_ThrowsIOException()
    {
        var store = new FileSystemTemplateStore(string.Empty);

        Assert.Throws<IOException>(() => store.EnsureRootDirectory());
    }

    [Fact]
    public async Task NameTooLong_ThrowsDiagramFormatException()
    {
        var store = new FileSystemTemplateStore(TempRoot());
        string name = new('a', 65);

        await Assert.ThrowsAsync<DiagramFormatException>(
            () => store.FindAsync(name, CancellationToken.None));
    }
}
