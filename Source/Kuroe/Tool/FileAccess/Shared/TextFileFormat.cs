using System.Text;

namespace Kuroe.Tool.FileAccess.Shared;

/// <summary>文本文件的编码格式：解码与写回使用的编码，以及 BOM 的字节长度。
/// 识别 UTF-8、UTF-16 与 UTF-32 的 BOM；没有 BOM 且前 8 KB 不含 NUL 时按 UTF-8 无 BOM 处理。</summary>
/// <param name="Encoding">读写该文件使用的编码实例，BOM 由实例的前导字节决定。</param>
/// <param name="PreambleLength">文件头 BOM 的字节数，没有 BOM 时为 0。</param>
public readonly record struct TextFileFormat(Encoding Encoding, int PreambleLength)
{
    private static readonly byte[] Utf32LeBom = [0xFF, 0xFE, 0x00, 0x00];
    private static readonly byte[] Utf32BeBom = [0x00, 0x00, 0xFE, 0xFF];
    private static readonly byte[] Utf16LeBom = [0xFF, 0xFE];
    private static readonly byte[] Utf16BeBom = [0xFE, 0xFF];
    private static readonly byte[] Utf8Bom = [0xEF, 0xBB, 0xBF];

    private static readonly TextFileFormat Utf32Le = new(
        new UTF32Encoding(bigEndian: false, byteOrderMark: true, throwOnInvalidCharacters: true),
        Utf32LeBom.Length);

    private static readonly TextFileFormat Utf32Be = new(
        new UTF32Encoding(bigEndian: true, byteOrderMark: true, throwOnInvalidCharacters: true),
        Utf32BeBom.Length);

    private static readonly TextFileFormat Utf16Le = new(
        new UnicodeEncoding(bigEndian: false, byteOrderMark: true, throwOnInvalidBytes: true),
        Utf16LeBom.Length);

    private static readonly TextFileFormat Utf16Be = new(
        new UnicodeEncoding(bigEndian: true, byteOrderMark: true, throwOnInvalidBytes: true),
        Utf16BeBom.Length);

    private static readonly TextFileFormat Utf8 = new(
        new UTF8Encoding(encoderShouldEmitUTF8Identifier: true, throwOnInvalidBytes: true),
        Utf8Bom.Length);

    private static readonly TextFileFormat Utf8NoBom = new(
        new UTF8Encoding(encoderShouldEmitUTF8Identifier: false, throwOnInvalidBytes: true),
        0);

    /// <summary>识别文件头的编码与 BOM。含 NUL 且没有已知 BOM 时返回 null，表示二进制文件。</summary>
    public static TextFileFormat? Detect(ReadOnlySpan<byte> head)
    {
        if (head.StartsWith(Utf32LeBom))
        {
            return Utf32Le;
        }

        if (head.StartsWith(Utf32BeBom))
        {
            return Utf32Be;
        }

        if (head.StartsWith(Utf16LeBom))
        {
            return Utf16Le;
        }

        if (head.StartsWith(Utf16BeBom))
        {
            return Utf16Be;
        }

        if (head.StartsWith(Utf8Bom))
        {
            return Utf8;
        }

        return head.Contains((byte)0) ? null : Utf8NoBom;
    }
}