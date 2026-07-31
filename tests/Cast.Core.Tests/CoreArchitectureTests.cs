using System.Reflection;
using Cast.Core.Models;

namespace Cast.Core.Tests;

public sealed class CoreArchitectureTests
{
    [Fact]
    public void Assembly_DoesNotReferenceCliOrHostingPackages()
    {
        Assembly assembly = typeof(DiagramStyle).Assembly;
        string[] references = assembly.GetReferencedAssemblies()
            .Select(reference => reference.Name ?? string.Empty)
            .ToArray();

        Assert.DoesNotContain("cast", references, StringComparer.OrdinalIgnoreCase);
        Assert.DoesNotContain("System.CommandLine", references, StringComparer.OrdinalIgnoreCase);
        Assert.DoesNotContain(
            references,
            reference => reference.StartsWith("Microsoft.Extensions.", StringComparison.Ordinal));
    }

    [Fact]
    public void PublicApi_DoesNotExposeCliIoAdapters()
    {
        string[] typeNames = typeof(DiagramStyle).Assembly
            .GetExportedTypes()
            .Select(type => type.Name)
            .ToArray();

        Assert.DoesNotContain(typeNames, name => name.StartsWith("FileSystem", StringComparison.Ordinal));
        Assert.DoesNotContain("IDiagramWriter", typeNames);
        Assert.DoesNotContain("ISourceFileReader", typeNames);
        Assert.DoesNotContain("ITextFileEditor", typeNames);
    }
}
