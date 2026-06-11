namespace Cast.Cli.Services;

/// <summary>
/// Opens a folder in an editor for the user. Unlike <see cref="IFileOpener"/> — a best-effort
/// side effect of an already-successful command — launching the editor is the entire point of the
/// command that uses this, so a failed launch surfaces as an <see cref="System.IO.IOException"/>
/// with a user-facing message instead of being swallowed.
/// </summary>
public interface IFolderOpener
{
    /// <summary>
    /// Opens <paramref name="path"/> in an editor without waiting for it to close. Throws
    /// <see cref="System.IO.IOException"/> when the editor cannot be launched.
    /// </summary>
    void Open(string path);
}
