using System.Text;
using ErrorOr;

namespace Kuroe.Tool.FileAccess;

/// <summary>一个文本文件的内容与格式：原文、归一换行的文本、换行风格、编码，以及归一文本到原文的索引映射。
/// 匹配在归一文本上进行，替换写回原文，未匹配部分的换行不被改写。</summary>
public sealed class TextFileContent
{
    private readonly string _normalized;
    private readonly Encoding _encoding;
    private readonly int[] _map;

    private TextFileContent(string original, string normalized, string newLine, Encoding encoding, int[] map)
    {
        Original = original;
        _normalized = normalized;
        NewLine = newLine;
        _encoding = encoding;
        _map = map;
    }

    /// <summary>文件原文，保留原有换行。</summary>
    public string Original { get; }

    /// <summary>文件首处换行的风格，没有换行时为 LF。</summary>
    public string NewLine { get; }

    /// <summary>读取文本文件。二进制文件与读取失败返回错误。</summary>
    public static ErrorOr<TextFileContent> Read(string file)
    {
        try
        {
            return TextFileProbe.Detect(file) is { } format
                ? Read(file, format)
                : ToolErrors.ReadFailed("文件是二进制文件。");
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            return ToolErrors.ReadFailed($"读取失败：{ex.Message}");
        }
    }

    /// <summary>按已识别的格式读取文本文件。读取失败返回错误。</summary>
    public static ErrorOr<TextFileContent> Read(string file, TextFileFormat format)
    {
        try
        {
            byte[] bytes = File.ReadAllBytes(file);
            if (bytes.Length < format.PreambleLength)
            {
                return ToolErrors.ReadFailed("文件长度小于 BOM 长度。");
            }

            string original = format.Encoding.GetString(
                bytes,
                format.PreambleLength,
                bytes.Length - format.PreambleLength);
            string normalized = TextNewLines.Normalize(original);

            return new TextFileContent(
                original,
                normalized,
                TextNewLines.Detect(original),
                format.Encoding,
                BuildMap(original, normalized));
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or DecoderFallbackException)
        {
            return ToolErrors.ReadFailed($"读取失败：{ex.Message}");
        }
    }

    /// <summary>按替换条件替换原文，一次扫描完成匹配与替换。replacement 的换行转换为文件原有风格，未匹配内容保持原样。</summary>
    public TextFileReplacement Replace(TextReplacement replacement)
    {
        IReadOnlyList<int> matches = Find(replacement.Pattern, replacement.Comparison);
        if (matches.Count == 0)
        {
            return new TextFileReplacement(Original, 0, []);
        }

        string text = TextNewLines.Convert(replacement.Replacement, NewLine);
        var result = new StringBuilder(Original.Length);
        int last = 0;
        foreach (int match in matches)
        {
            int start = _map[match];
            int end = _map[match + replacement.Pattern.Length];
            result.Append(Original, last, start - last);
            result.Append(text);
            last = end;
        }

        result.Append(Original, last, Original.Length - last);

        return new TextFileReplacement(result.ToString(), matches.Count, [.. LinesOf(matches).Distinct()]);
    }

    /// <summary>在归一文本中查找全部不重叠匹配，返回匹配起始位置。pattern 须已归一换行。</summary>
    private List<int> Find(string pattern, StringComparison comparison)
    {
        if (pattern.Length == 0)
        {
            return [];
        }

        List<int> matches = [];
        int index = 0;
        while (index <= _normalized.Length - pattern.Length)
        {
            int found = _normalized.IndexOf(pattern, index, comparison);
            if (found < 0)
            {
                break;
            }

            matches.Add(found);
            index = found + pattern.Length;
        }

        return matches;
    }

    /// <summary>取每个匹配起始位置在归一文本中的行号，行号从 1 起。matches 须按起始位置升序。</summary>
    private List<int> LinesOf(IReadOnlyList<int> matches)
    {
        List<int> lines = [];
        int line = 1;
        int cursor = 0;
        foreach (int match in matches)
        {
            while (cursor < match)
            {
                if (_normalized[cursor] == '\n')
                {
                    line++;
                }

                cursor++;
            }

            lines.Add(line);
        }

        return lines;
    }

    /// <summary>把内容写回文件。encodingOverride 为 null 时使用读取时的编码与 BOM。</summary>
    public ErrorOr<Success> Write(string file, string content, Encoding? encodingOverride)
    {
        try
        {
            File.WriteAllText(file, content, encodingOverride ?? _encoding);

            return Result.Success;
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or EncoderFallbackException)
        {
            return ToolErrors.WriteFailed($"写入失败：{ex.Message}");
        }
    }

    /// <summary>建立归一文本下标到原文下标的映射，CRLF 与 CR 在归一文本中各占一个下标并映射到原文起点。</summary>
    private static int[] BuildMap(string original, string normalized)
    {
        int[] map = new int[normalized.Length + 1];
        int originalIndex = 0;
        for (int normalizedIndex = 0; normalizedIndex < normalized.Length; normalizedIndex++)
        {
            map[normalizedIndex] = originalIndex;
            originalIndex += original[originalIndex] == '\r'
                && originalIndex + 1 < original.Length
                && original[originalIndex + 1] == '\n'
                ? 2
                : 1;
        }

        map[normalized.Length] = original.Length;

        return map;
    }
}