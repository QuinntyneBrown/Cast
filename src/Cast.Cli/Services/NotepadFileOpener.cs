using System;
using System.Diagnostics;
using Microsoft.Extensions.Logging;

namespace Cast.Cli.Services;

/// <summary>
/// Default <see cref="IFileOpener"/>: opens the file in <c>notepad.exe</c>, fire-and-forget. On
/// non-Windows platforms (the tool ships as a cross-platform dotnet tool) the launch is skipped
/// with a debug log rather than guessing at an editor.
/// </summary>
public sealed class NotepadFileOpener : IFileOpener
{
    private readonly ILogger<NotepadFileOpener> _logger;

    public NotepadFileOpener(ILogger<NotepadFileOpener> logger) => _logger = logger;

    /// <inheritdoc />
    public void Open(string path)
    {
        if (!OperatingSystem.IsWindows())
        {
            _logger.LogDebug("Skipping editor launch: notepad.exe is only available on Windows.");
            return;
        }

        // Catch everything: opening the editor is best-effort and must never turn a successful
        // command into a failure. Disposing the Process wrapper does not close Notepad.
        // UseShellExecute = true matters: a CreateProcess launch would hand Notepad the console's
        // stdout/stderr handles, so anything capturing cast's output (a pipe, a script, an IDE)
        // would block until the user closed Notepad.
        try
        {
            using Process? process = Process.Start(new ProcessStartInfo("notepad.exe", path)
            {
                UseShellExecute = true,
            });
        }
        catch (Exception ex)
        {
            _logger.LogWarning("Could not open '{Path}' in Notepad: {Message}", path, ex.Message);
        }
    }
}
