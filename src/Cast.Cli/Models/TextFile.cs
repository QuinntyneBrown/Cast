namespace Cast.Cli.Models;

/// <summary>
/// A text file read for in-place editing: its resolved path, decoded content, and detected
/// encoding. Writing the record back (typically with a transformed <see cref="Content"/>)
/// reproduces the original encoding and byte-order mark, so an edit changes only the lines it
/// means to change.
/// </summary>
/// <param name="Path">The full path the file was read from.</param>
/// <param name="Content">The decoded text, without any byte-order-mark character.</param>
/// <param name="Encoding">The encoding detected from the file's byte-order mark.</param>
public sealed record TextFile(string Path, string Content, TextFileEncoding Encoding);
