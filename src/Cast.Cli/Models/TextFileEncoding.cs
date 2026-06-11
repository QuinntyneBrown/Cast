namespace Cast.Cli.Models;

/// <summary>
/// The encoding of a text file as detected from its byte-order mark, carried alongside the
/// decoded content so an in-place rewrite can emit exactly the bytes the file arrived with.
/// A file without a recognizable BOM is treated as UTF-8 without BOM.
/// </summary>
public enum TextFileEncoding
{
    /// <summary>UTF-8 without a byte-order mark (the default for unmarked files).</summary>
    Utf8,

    /// <summary>UTF-8 with the <c>EF BB BF</c> byte-order mark.</summary>
    Utf8WithBom,

    /// <summary>UTF-16 little-endian with the <c>FF FE</c> byte-order mark.</summary>
    Utf16LittleEndian,

    /// <summary>UTF-16 big-endian with the <c>FE FF</c> byte-order mark.</summary>
    Utf16BigEndian,

    /// <summary>UTF-32 little-endian with the <c>FF FE 00 00</c> byte-order mark.</summary>
    Utf32LittleEndian,

    /// <summary>UTF-32 big-endian with the <c>00 00 FE FF</c> byte-order mark.</summary>
    Utf32BigEndian,
}
