using System.Globalization;
using System.Text;
using ErrorOr;

namespace Kuroe.Tool.FileAccess;

/// <summary>根目录内的文本搜索：展开目录或文件范围，递归收集候选文件，按后缀与字面文本筛选，匹配行文本累计到预算后停止收集。
/// 路径解析与边界沿用文件作用域，递归时跳过路径准入规则命中的目录与重解析点目录。</summary>
/// <param name="scope">搜索的文件访问作用域。</param>
/// <param name="access">路径准入规则，null 表示不限制。</param>
public sealed class FileSearch(FileScope scope, ToolPathAccess? access = null)
{
    /// <summary>二进制判定的探测字节数。</summary>
    private const int ProbeSize = 8192;

    /// <summary>按行读取的字符缓冲大小。</summary>
    private const int BufferSize = 4096;

    private static readonly byte[] Utf32LeBom = [0xFF, 0xFE, 0x00, 0x00];
    private static readonly byte[] Utf32BeBom = [0x00, 0x00, 0xFE, 0xFF];
    private static readonly byte[] Utf16LeBom = [0xFF, 0xFE];
    private static readonly byte[] Utf16BeBom = [0xFE, 0xFF];
    private static readonly byte[] Utf8Bom = [0xEF, 0xBB, 0xBF];

    private static readonly StringComparer PathComparer = OperatingSystem.IsWindows()
        ? StringComparer.OrdinalIgnoreCase
        : StringComparer.Ordinal;

    private readonly FileScope _scope = scope;
    private readonly ToolPathAccess? _access = access;

    /// <summary>按范围、后缀与文本搜索，匹配行文本累计到预算后停止收集。单个文件读取失败跳过。</summary>
    /// <param name="paths">搜索的目录或文件范围，空集合表示路径基准目录。</param>
    /// <param name="query">搜索条件。</param>
    /// <param name="characterBudget">匹配行文本的字符预算，是渲染长度的下界，只限制扫描量，不决定输出是否截断。</param>
    public ErrorOr<TextSearchResult> Search(
        IReadOnlyList<string> paths,
        TextSearchQuery query,
        int characterBudget)
    {
        if (characterBudget < 1)
        {
            return ToolErrors.Argument("字符预算必须大于 0。");
        }

        ErrorOr<IReadOnlyList<string>> candidates = Candidates(paths);
        if (candidates.IsError)
        {
            return candidates.ErrorsOrEmptyList;
        }

        List<TextSearchMatch> matches = [];
        int scannedFiles = 0;
        int skippedFiles = 0;
        int characters = 0;

        foreach (string file in candidates.Value)
        {
            if (!query.MatchesExtension(file))
            {
                continue;
            }

            if (characters >= characterBudget)
            {
                break;
            }

            scannedFiles++;
            if (!SearchFile(file, query, matches, ref characters, characterBudget))
            {
                skippedFiles++;
            }
        }

        return new TextSearchResult(matches, scannedFiles, skippedFiles);
    }

    /// <summary>展开搜索范围并按完整路径去重，结果按序数排序。</summary>
    private ErrorOr<IReadOnlyList<string>> Candidates(IReadOnlyList<string> paths)
    {
        IReadOnlyList<string> scopes = paths.Count == 0 ? [string.Empty] : paths;
        HashSet<string> files = new(PathComparer);

        foreach (string path in scopes)
        {
            ErrorOr<string> resolved = _scope.Resolve(path);
            if (resolved.IsError)
            {
                return resolved.ErrorsOrEmptyList;
            }

            if (_access is not null)
            {
                ErrorOr<Success> allowed = _access.Check(path, resolved.Value, _scope.Root);
                if (allowed.IsError)
                {
                    return allowed.ErrorsOrEmptyList;
                }
            }

            if (File.Exists(resolved.Value))
            {
                files.Add(resolved.Value);
                continue;
            }

            if (!Directory.Exists(resolved.Value))
            {
                return ToolErrors.Path($"{_scope.Display(path)} 不存在。");
            }

            Collect(resolved.Value, files);
        }

        return files.OrderBy(file => file, StringComparer.Ordinal).ToArray();
    }

    /// <summary>递归收集目录内的文件，跳过路径准入规则命中的目录与重解析点目录。</summary>
    private void Collect(string directory, HashSet<string> files)
    {
        Queue<string> pending = new();
        pending.Enqueue(directory);

        while (pending.Count > 0)
        {
            string current = pending.Dequeue();
            if (IsExcluded(current) || IsReparsePoint(current))
            {
                continue;
            }

            try
            {
                foreach (string file in Directory.EnumerateFiles(current).OrderBy(path => path, StringComparer.Ordinal))
                {
                    files.Add(file);
                }

                foreach (string child in Directory.EnumerateDirectories(current).OrderBy(path => path, StringComparer.Ordinal))
                {
                    pending.Enqueue(child);
                }
            }
            catch (IOException)
            {
                // 目录读取失败时跳过
            }
            catch (UnauthorizedAccessException)
            {
                // 目录不可访问时跳过
            }
        }
    }

    /// <summary>该目录是否被路径准入规则排除。</summary>
    private bool IsExcluded(string directory) =>
        _access is not null && !_access.Allows(directory, _scope.Root);

    /// <summary>该目录是否是重解析点，读取属性失败时按是处理。</summary>
    private static bool IsReparsePoint(string directory)
    {
        try
        {
            return (File.GetAttributes(directory) & FileAttributes.ReparsePoint) != 0;
        }
        catch (IOException)
        {
            return true;
        }
        catch (UnauthorizedAccessException)
        {
            return true;
        }
    }

    /// <summary>读取一个文件中的匹配，匹配行文本累计到预算时停止。文件为二进制或读取失败时返回 false。</summary>
    private static bool SearchFile(
        string file,
        TextSearchQuery query,
        List<TextSearchMatch> matches,
        ref int characters,
        int characterBudget)
    {
        try
        {
            if (!IsTextFile(file))
            {
                return false;
            }

            using StreamReader reader = new(file, Encoding.UTF8, detectEncodingFromByteOrderMarks: true);
            int lineNumber = 0;
            foreach ((string line, bool truncated) in ReadLines(reader, FileScope.OutputLimit))
            {
                lineNumber++;
                if (!query.Matches(line))
                {
                    continue;
                }

                matches.Add(new TextSearchMatch(file, lineNumber, truncated ? $"{line}…" : line));
                characters += line.Length + lineNumber.ToString(CultureInfo.InvariantCulture).Length + 2;
                if (characters >= characterBudget)
                {
                    break;
                }
            }

            return true;
        }
        catch (IOException)
        {
            return false;
        }
        catch (UnauthorizedAccessException)
        {
            return false;
        }
    }

    /// <summary>按行读取文本，单行最多保留 maxLength 个字符。超出部分丢弃并标记该行被截断。</summary>
    private static IEnumerable<(string Text, bool Truncated)> ReadLines(TextReader reader, int maxLength)
    {
        char[] buffer = new char[BufferSize];
        var line = new StringBuilder();
        bool truncated = false;
        bool started = false;
        bool skipLineFeed = false;
        int read;
        while ((read = reader.Read(buffer, 0, buffer.Length)) > 0)
        {
            for (int index = 0; index < read; index++)
            {
                char current = buffer[index];
                if (skipLineFeed)
                {
                    skipLineFeed = false;
                    if (current == '\n')
                    {
                        continue;
                    }
                }

                if (current is '\r' or '\n')
                {
                    yield return (line.ToString(), truncated);
                    line.Clear();
                    truncated = false;
                    started = false;
                    skipLineFeed = current == '\r';
                    continue;
                }

                started = true;
                if (line.Length < maxLength)
                {
                    line.Append(current);
                }
                else
                {
                    truncated = true;
                }
            }
        }

        if (started)
        {
            yield return (line.ToString(), truncated);
        }
    }

    /// <summary>按前 8 KB 的 BOM 与 NUL 字节判断是否为文本文件。带 UTF 族 BOM 的文件按文本处理。</summary>
    private static bool IsTextFile(string file)
    {
        using FileStream stream = new(file, FileMode.Open, System.IO.FileAccess.Read, FileShare.Read);
        byte[] head = new byte[ProbeSize];
        int read = 0;
        while (read < head.Length)
        {
            int count = stream.Read(head, read, head.Length - read);
            if (count == 0)
            {
                break;
            }

            read += count;
        }

        ReadOnlySpan<byte> bytes = head.AsSpan(0, read);

        return bytes.StartsWith(Utf32LeBom)
            || bytes.StartsWith(Utf32BeBom)
            || bytes.StartsWith(Utf16LeBom)
            || bytes.StartsWith(Utf16BeBom)
            || bytes.StartsWith(Utf8Bom)
            || !bytes.Contains((byte)0);
    }
}