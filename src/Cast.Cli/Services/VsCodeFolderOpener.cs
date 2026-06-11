using System;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;

namespace Cast.Cli.Services;

/// <summary>
/// Default <see cref="IFolderOpener"/>: opens the folder in Visual Studio Code via the
/// <c>code</c> command, fire-and-forget. Launched through ShellExecute so the spawned editor
/// never inherits the console's output handles (a CreateProcess launch would make anything
/// capturing cast's output block until the editor closed).
/// </summary>
public sealed class VsCodeFolderOpener : IFolderOpener
{
    // "code" resolves on every platform where the shell command is installed; the explicit
    // Windows .cmd shim is the fallback for shells that don't apply PATHEXT.
    private static readonly string[] CommandCandidates = ["code", "code.cmd"];

    /// <inheritdoc />
    public void Open(string path)
    {
        foreach (string command in CommandCandidates)
        {
            try
            {
                using Process? process = Process.Start(new ProcessStartInfo(command)
                {
                    ArgumentList = { path },
                    UseShellExecute = true,
                });

                return;
            }
            catch (Exception ex) when (ex is Win32Exception or InvalidOperationException or PlatformNotSupportedException)
            {
                // This candidate could not be launched; try the next spelling.
            }
        }

        throw new IOException(
            "Could not launch Visual Studio Code: the 'code' command was not found. Install VS Code and " +
            "make sure 'code' is on the PATH (in VS Code: \"Shell Command: Install 'code' command in PATH\").");
    }
}
