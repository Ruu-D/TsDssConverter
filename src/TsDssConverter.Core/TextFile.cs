using System.Text;

namespace TsDssConverter.Core;

/// <summary>
/// Reads the small text files that people edit by hand (materials.csv, columns-li.txt, columns-lp.txt).
/// Notepad and Excel do not all save the same encoding, so the text is decoded with a fallback.
/// </summary>
public static class TextFile
{
    public static string ReadAllText(string path)
    {
        // FileShare.ReadWrite: Excel or Notepad may have the file open.
        using var stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
        using var memory = new MemoryStream();
        stream.CopyTo(memory);
        return Decode(memory.ToArray());
    }

    /// <summary>
    /// UTF-8 (with or without BOM) first. If the bytes are not valid UTF-8 the file was probably saved
    /// as "ANSI" (Excel "CSV", an old Notepad), so fall back to Windows-1252. "Chêne" survives both ways.
    /// </summary>
    public static string Decode(byte[] bytes)
    {
        // Skip the UTF-8 byte order mark (EF BB BF) if there is one.
        int start = 0;
        if (bytes.Length >= 3 && bytes[0] == 0xEF && bytes[1] == 0xBB && bytes[2] == 0xBF)
        {
            start = 3;
        }

        try
        {
            // throwOnInvalidBytes: true -> invalid UTF-8 throws instead of silently giving '?' characters.
            var strictUtf8 = new UTF8Encoding(encoderShouldEmitUTF8Identifier: false, throwOnInvalidBytes: true);
            return strictUtf8.GetString(bytes, start, bytes.Length - start);
        }
        catch (DecoderFallbackException)
        {
            Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);
            return Encoding.GetEncoding(1252).GetString(bytes, start, bytes.Length - start);
        }
    }
}
