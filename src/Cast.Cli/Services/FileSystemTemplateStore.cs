using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Encodings.Web;
using System.Text.Json;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using Cast.Cli.Diagnostics;
using Cast.Cli.Models;

namespace Cast.Cli.Services;

/// <summary>
/// Default <see cref="ITemplateStore"/>. Stores each template as an indented camelCase JSON file
/// named <c>&lt;name&gt;.json</c> under the per-user application-data folder
/// (<c>%APPDATA%\cast\templates</c> on Windows, <c>~/.config/cast/templates</c> elsewhere).
/// Template names are validated against a strict whitelist so a name can never escape the
/// templates folder or collide with a reserved device name.
/// </summary>
public sealed class FileSystemTemplateStore : ITemplateStore
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        ReadCommentHandling = JsonCommentHandling.Skip,
        AllowTrailingCommas = true,
        // The file is meant to be hand-editable; the default encoder would store the '>' in
        // message specs as '>'. The output is never embedded in HTML, so relaxed is safe.
        Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
    };

    // Whitelist: must start with a letter or digit, then letters, digits, '.', '_' or '-'.
    // This inherently blocks path separators, '..', drive colons and every other character
    // that could make the name escape the templates folder.
    private static readonly Regex ValidNamePattern =
        new("^[A-Za-z0-9][A-Za-z0-9._-]*$", RegexOptions.CultureInvariant);

    // Names Windows reserves for devices regardless of extension (so 'nul' would create an
    // unusable 'nul.json'). Checked case-insensitively because the filesystem is.
    private static readonly HashSet<string> ReservedDeviceNames = new(StringComparer.OrdinalIgnoreCase)
    {
        "CON", "PRN", "AUX", "NUL",
        "COM1", "COM2", "COM3", "COM4", "COM5", "COM6", "COM7", "COM8", "COM9",
        "LPT1", "LPT2", "LPT3", "LPT4", "LPT5", "LPT6", "LPT7", "LPT8", "LPT9",
    };

    private const int MaxNameLength = 64;

    private readonly string _rootDirectory;

    /// <summary>Creates a store rooted at the per-user application-data folder.</summary>
    public FileSystemTemplateStore()
        : this(DefaultRootDirectory())
    {
    }

    /// <summary>Creates a store rooted at <paramref name="rootDirectory"/> (useful for tests).</summary>
    public FileSystemTemplateStore(string rootDirectory) => _rootDirectory = rootDirectory;

    /// <inheritdoc />
    public async Task SaveAsync(DiagramTemplate template, CancellationToken cancellationToken)
    {
        string path = ResolvePath(template.Name);

        try
        {
            Directory.CreateDirectory(ResolveRoot());
            string json = JsonSerializer.Serialize(template, JsonOptions);
            await File.WriteAllTextAsync(path, json, cancellationToken).ConfigureAwait(false);
        }
        catch (UnauthorizedAccessException ex)
        {
            throw new IOException($"Access to '{path}' was denied.", ex);
        }
    }

    /// <inheritdoc />
    public async Task<DiagramTemplate?> FindAsync(string name, CancellationToken cancellationToken)
    {
        string path = ResolvePath(name);
        if (!File.Exists(path))
        {
            return null;
        }

        string json;
        try
        {
            json = await File.ReadAllTextAsync(path, cancellationToken).ConfigureAwait(false);
        }
        catch (UnauthorizedAccessException ex)
        {
            throw new IOException($"Access to '{path}' was denied.", ex);
        }

        try
        {
            DiagramTemplate? template = JsonSerializer.Deserialize<DiagramTemplate>(json, JsonOptions);
            return template
                ?? throw new DiagramFormatException($"Template file '{path}' does not contain a template definition.");
        }
        catch (JsonException ex)
        {
            throw new DiagramFormatException(
                $"Template file '{path}' is not a valid template: {ex.Message}", ex);
        }
    }

    /// <inheritdoc />
    public Task<IReadOnlyList<string>> ListAsync(CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        string root = ResolveRoot();
        if (!Directory.Exists(root))
        {
            return Task.FromResult<IReadOnlyList<string>>([]);
        }

        try
        {
            IReadOnlyList<string> names = Directory.EnumerateFiles(root, "*.json")
                .Select(Path.GetFileNameWithoutExtension)
                .OfType<string>()
                .Order(StringComparer.Ordinal)
                .ToList();

            return Task.FromResult(names);
        }
        catch (UnauthorizedAccessException ex)
        {
            throw new IOException($"Access to '{root}' was denied.", ex);
        }
    }

    /// <inheritdoc />
    public Task<bool> DeleteAsync(string name, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        string path = ResolvePath(name);
        if (!File.Exists(path))
        {
            return Task.FromResult(false);
        }

        try
        {
            File.Delete(path);
            return Task.FromResult(true);
        }
        catch (UnauthorizedAccessException ex)
        {
            throw new IOException($"Access to '{path}' was denied.", ex);
        }
    }

    /// <inheritdoc />
    public string EnsureRootDirectory()
    {
        string root = ResolveRoot();

        try
        {
            Directory.CreateDirectory(root);
        }
        catch (UnauthorizedAccessException ex)
        {
            throw new IOException($"Access to '{root}' was denied.", ex);
        }

        return root;
    }

    private static string DefaultRootDirectory()
    {
        string appData = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
        return string.IsNullOrEmpty(appData) ? string.Empty : Path.Combine(appData, "cast", "templates");
    }

    /// <summary>
    /// Returns the templates folder, failing lazily (never at construction, so building the DI
    /// container stays safe) when the per-user application-data folder cannot be determined —
    /// possible in bare containers without a user profile.
    /// </summary>
    private string ResolveRoot() =>
        string.IsNullOrEmpty(_rootDirectory)
            ? throw new IOException(
                "The per-user application-data folder could not be determined, so templates are unavailable on this system.")
            : _rootDirectory;

    private string ResolvePath(string name)
    {
        ValidateName(name);
        return Path.Combine(ResolveRoot(), name + ".json");
    }

    private static void ValidateName(string name)
    {
        if (string.IsNullOrWhiteSpace(name))
        {
            throw new DiagramFormatException("Template name must not be empty.");
        }

        if (name.Length > MaxNameLength)
        {
            throw new DiagramFormatException(
                $"Template name '{name}' is too long; use at most {MaxNameLength} characters.");
        }

        if (!ValidNamePattern.IsMatch(name) || name.EndsWith('.'))
        {
            throw new DiagramFormatException(
                $"Template name '{name}' is invalid. Use letters, digits, '.', '_' or '-', " +
                "starting with a letter or digit and not ending with '.'.");
        }

        // 'nul.orders' would still address the NUL device on Windows, so check the stem.
        string stem = name.Split('.')[0];
        if (ReservedDeviceNames.Contains(stem))
        {
            throw new DiagramFormatException(
                $"Template name '{name}' is a reserved Windows device name; pick another name.");
        }
    }
}
