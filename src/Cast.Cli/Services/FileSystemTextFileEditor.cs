using System;
using System.IO;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Cast.Cli.Models;

namespace Cast.Cli.Services;

/// <summary>
/// Default <see cref="ITextFileEditor"/>. Detects the encoding by sniffing the file's
/// byte-order mark (UTF-8, UTF-16 LE/BE, UTF-32 LE/BE; anything unmarked is read as UTF-8
/// without BOM) and writes content back with exactly the detected encoding and BOM, so an
/// in-place edit round-trips every byte it does not deliberately change. Failures are
/// translated into <see cref="FileNotFoundException"/>/<see cref="IOException"/>, mirroring
/// <see cref="FileSystemSourceReader"/>.
/// </summary>
public sealed class FileSystemTextFileEditor : ITextFileEditor
{
    /// <inheritdoc />
    public async Task<TextFile> ReadAsync(string path, CancellationToken cancellationToken)
    {
        string fullPath;
        try
        {
            fullPath = Path.GetFullPath(path);
        }
        catch (Exception ex) when (ex is ArgumentException or NotSupportedException)
        {
            throw new FileNotFoundException($"'{path}' is not a valid file path.", path, ex);
        }

        if (!File.Exists(fullPath))
        {
            throw new FileNotFoundException($"File '{fullPath}' was not found.", fullPath);
        }

        byte[] bytes;
        try
        {
            bytes = await File.ReadAllBytesAsync(fullPath, cancellationToken).ConfigureAwait(false);
        }
        catch (UnauthorizedAccessException ex)
        {
            throw new IOException($"Access to '{fullPath}' was denied.", ex);
        }

        TextFileEncoding kind = DetectEncoding(bytes);
        (Encoding encoding, int bomLength) = Resolve(kind);
        string content = encoding.GetString(bytes, bomLength, bytes.Length - bomLength);
        return new TextFile(fullPath, content, kind);
    }

    /// <inheritdoc />
    public async Task WriteAsync(TextFile file, CancellationToken cancellationToken)
    {
        (Encoding encoding, int bomLength) = Resolve(file.Encoding);
        byte[] payload = encoding.GetBytes(file.Content);

        byte[] bytes;
        if (bomLength > 0)
        {
            byte[] preamble = Preamble(file.Encoding);
            bytes = new byte[preamble.Length + payload.Length];
            preamble.CopyTo(bytes, 0);
            payload.CopyTo(bytes, preamble.Length);
        }
        else
        {
            bytes = payload;
        }

        try
        {
            await File.WriteAllBytesAsync(file.Path, bytes, cancellationToken).ConfigureAwait(false);
        }
        catch (UnauthorizedAccessException ex)
        {
            throw new IOException($"Access to '{file.Path}' was denied.", ex);
        }
    }

    private static TextFileEncoding DetectEncoding(byte[] bytes) => bytes switch
    {
        // UTF-32 LE starts FF FE 00 00, so it must be tested before UTF-16 LE's FF FE.
        [0xFF, 0xFE, 0x00, 0x00, ..] => TextFileEncoding.Utf32LittleEndian,
        [0x00, 0x00, 0xFE, 0xFF, ..] => TextFileEncoding.Utf32BigEndian,
        [0xEF, 0xBB, 0xBF, ..] => TextFileEncoding.Utf8WithBom,
        [0xFF, 0xFE, ..] => TextFileEncoding.Utf16LittleEndian,
        [0xFE, 0xFF, ..] => TextFileEncoding.Utf16BigEndian,
        _ => TextFileEncoding.Utf8,
    };

    /// <summary>The BOM-less encoding plus the BOM length consumed/emitted for each kind.</summary>
    private static (Encoding Encoding, int BomLength) Resolve(TextFileEncoding kind) => kind switch
    {
        TextFileEncoding.Utf8WithBom => (new UTF8Encoding(encoderShouldEmitUTF8Identifier: false), 3),
        TextFileEncoding.Utf16LittleEndian => (new UnicodeEncoding(bigEndian: false, byteOrderMark: false), 2),
        TextFileEncoding.Utf16BigEndian => (new UnicodeEncoding(bigEndian: true, byteOrderMark: false), 2),
        TextFileEncoding.Utf32LittleEndian => (new UTF32Encoding(bigEndian: false, byteOrderMark: false), 4),
        TextFileEncoding.Utf32BigEndian => (new UTF32Encoding(bigEndian: true, byteOrderMark: false), 4),
        _ => (new UTF8Encoding(encoderShouldEmitUTF8Identifier: false), 0),
    };

    private static byte[] Preamble(TextFileEncoding kind) => kind switch
    {
        TextFileEncoding.Utf8WithBom => [0xEF, 0xBB, 0xBF],
        TextFileEncoding.Utf16LittleEndian => [0xFF, 0xFE],
        TextFileEncoding.Utf16BigEndian => [0xFE, 0xFF],
        TextFileEncoding.Utf32LittleEndian => [0xFF, 0xFE, 0x00, 0x00],
        TextFileEncoding.Utf32BigEndian => [0x00, 0x00, 0xFE, 0xFF],
        _ => [],
    };
}
