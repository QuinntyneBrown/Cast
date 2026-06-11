namespace Cast.Cli.Services;

/// <summary>
/// Best-effort launcher that opens a written file in an editor for the user. Opening is a
/// convenience on top of a command that has already succeeded, so implementations must never
/// throw — a failed launch is logged and swallowed.
/// </summary>
public interface IFileOpener
{
    /// <summary>Opens <paramref name="path"/> in an editor without waiting for it to close. Never throws.</summary>
    void Open(string path);
}
