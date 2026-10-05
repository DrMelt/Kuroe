using System.Globalization;
using System.Text;
using ErrorOr;
using Kuroe.Shared.Executions.Tools;

namespace Kuroe.Tools;

/// <summary>工作目录的文件访问工具：读写文本文件与列出目录条目。路径解析后必须落回工作目录内。</summary>
/// <remarks>容器装配用的载体。</remarks>
internal sealed class FileTool(string baseDirectory) : ITool
{
    /// <summary>单条读取内容与提示文本的字符上限，超长截断并提示。</summary>
    private const int OutputLimit = 8000;

    /// <summary>ReadFile 缺省读取的行数，不传范围时只读文件开头这些行。</summary>
    private const int DefaultLines = 50;

    /// <summary>批量读取一次交回的总字符上限，超出后停止并提示。</summary>
    private const int TotalLimit = 40000;

    /// <summary>一次写入的字符上限，超出整段拒绝。</summary>
    private const int ContentLimit = 40000;

    /// <summary>目录列表的条目上限，超出截断并提示。</summary>
    private const int MaxEntries = 200;

    /// <summary>本载体的函数声明。</summary>
    public IReadOnlyList<ToolFunction> Functions { get; } = Declare(baseDirectory);

    /// <summary>声明文件访问函数。</summary>
    private static IReadOnlyList<ToolFunction> Declare(string baseDirectory) =>
    [
        new ToolFunction(new ToolName("ReadFile"),
            "读取工作目录内文本文件指定行号范围的内容。from 与 to 是行号，缺省读前 50 行，to 超出文件行数时读到文件尾。",
            [
                new ToolParameter(new ToolName("path"), "相对工作目录的文件路径", Required: true),
                new ToolParameter(new ToolName("from"), "起始行号，数字，缺省 1"),
                new ToolParameter(new ToolName("to"), "结束行号，数字，缺省 50，超出文件行数时读到文件尾"),
            ],
            arguments => ReadFile(baseDirectory, arguments),
            new ToolPath("files/ReadFile")),
        new ToolFunction(new ToolName("ListDirectory"),
            "列出工作目录内一个目录的直接子项。path 缺省时列出工作目录根的条目。",
            [new ToolParameter(new ToolName("path"), "相对工作目录的目录路径，缺省为工作目录根")],
            arguments => ListDirectory(baseDirectory, arguments),
            new ToolPath("files/ListDirectory")),
        new ToolFunction(new ToolName("ReadFiles"),
            "一次读取工作目录内的多个文本文件。paths 是相对工作目录的文件路径列表，结果按列表顺序分段交回。",
            [new ToolParameter(new ToolName("paths"), "相对工作目录的文件路径列表", Required: true, List: true)],
            arguments => ReadFiles(baseDirectory, arguments),
            new ToolPath("files/ReadFiles")),
        new ToolFunction(new ToolName("WriteFile"),
            "把文本覆盖写入工作目录内的文件，文件不存在时创建，已存在时直接覆盖其内容。content 上限 40000 字符，父目录缺失时自动创建；encoding 指定写回编码，缺省 utf-8 不带 BOM。",
            [
                new ToolParameter(new ToolName("path"), "相对工作目录的文件路径", Required: true),
                new ToolParameter(new ToolName("content"), "要写入文件的文本", Required: true),
                new ToolParameter(new ToolName("encoding"), "写回编码，支持 utf-8、utf-8-bom、utf-16、utf-16be，缺省 utf-8"),
            ],
            arguments => WriteFile(baseDirectory, arguments),
            new ToolPath("files/WriteFile")),
        new ToolFunction(new ToolName("PatchFile"),
            "按行号范围替换工作目录内文件的内容。from 与 to 是行号，from 从 1 起；original 必须是该范围现在的原文，逐行不一致时调用被拒绝并在提示里回给实际内容；replacement 是替换后的新内容，行数可增减，为空时删除该范围；写回统一换行符，编码用 encoding 指定，缺省 utf-8 不带 BOM。",
            [
                new ToolParameter(new ToolName("path"), "相对工作目录的文件路径", Required: true),
                new ToolParameter(new ToolName("from"), "起始行号，数字，第 1 行起", Required: true),
                new ToolParameter(new ToolName("to"), "结束行号，数字", Required: true),
                new ToolParameter(new ToolName("original"), "该范围现在的原文，逐行与文件比对", Required: true),
                new ToolParameter(new ToolName("replacement"), "替换后的新内容，为空时删除该范围", Required: true),
                new ToolParameter(new ToolName("encoding"), "写回编码，支持 utf-8、utf-8-bom、utf-16、utf-16be，缺省 utf-8"),
            ],
            arguments => PatchFile(baseDirectory, arguments),
            new ToolPath("files/PatchFile")),
    ];

    /// <summary>读取指定文本文件指定行号范围的内容，按行截取并按字符上限截断；错误与拒绝以 ErrorOr 表达。行号从 1 起，结束行超出文件长度时读到文件尾。</summary>
    private static ErrorOr<string> ReadFile(string baseDirectory, ToolArguments arguments)
    {
        string? path = arguments.Text(new ToolName("path"));
        if (string.IsNullOrWhiteSpace(path))
        {
            return ToolErrors.Argument("缺少参数 path。");
        }

        ErrorOr<int> from = ParseLineNumber("from", arguments.Text(new ToolName("from")), 1);
        if (from.IsError)
        {
            return from.ErrorsOrEmptyList;
        }

        if (from.Value < 1)
        {
            return ToolErrors.Argument("起始行号 from 从 1 起。");
        }

        ErrorOr<int> to = ParseLineNumber("to", arguments.Text(new ToolName("to")), DefaultLines);
        if (to.IsError)
        {
            return to.ErrorsOrEmptyList;
        }

        if (to.Value < from.Value)
        {
            return ToolErrors.Argument("结束行号 to 不能小于起始行号 from。");
        }

        ResolveResult resolved = Resolve(baseDirectory, path);
        if (resolved.Rejected is { } rejected)
        {
            return rejected;
        }

        if (Directory.Exists(resolved.Path))
        {
            return ToolErrors.Path($"{path} 是目录，用 ListDirectory 列出它的条目。");
        }

        if (!File.Exists(resolved.Path))
        {
            return ToolErrors.Path($"{path} 不存在。");
        }

        try
        {
            using StreamReader reader = new(resolved.Path);

            for (int line = 1; line < from.Value; line++)
            {
                if (reader.ReadLine() is null)
                {
                    return ToolErrors.Argument($"起始行 {from.Value} 超出文件长度，该范围没有内容。");
                }
            }

            int maxLines = to.Value - from.Value + 1;
            var text = new StringBuilder();
            int readLines = 0;
            bool limited = false;
            while (readLines < maxLines && !limited)
            {
                string? line = reader.ReadLine();
                if (line is null)
                {
                    break;
                }

                readLines++;
                if (text.Length > 0)
                {
                    text.Append('\n');
                }

                text.Append(line);
                if (text.Length > OutputLimit)
                {
                    limited = true;
                }
            }

            string content = Truncate(text.ToString());
            if (!limited && readLines < maxLines)
            {
                content += $"（已到文件结尾，实际返回 {readLines} 行）";
            }

            return content;
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            return ToolErrors.ReadFailed($"读取 {path} 失败：{ex.Message}");
        }
    }

    /// <summary>按字符上限截断文本，超长保留前限并在末尾加截断标记。</summary>
    private static string Truncate(string text) =>
        text.Length <= OutputLimit ? text : string.Concat(text.AsSpan(0, OutputLimit), "…内容已截断");

    /// <summary>行号从文本取数。缺省或空白用默认值，其余按整数解析；解析失败给出带参数名与实参的详细错误。</summary>
    private static ErrorOr<int> ParseLineNumber(string parameter, string? text, int @default)
    {
        if (string.IsNullOrWhiteSpace(text))
        {
            return @default;
        }

        return int.TryParse(text, NumberStyles.Integer, CultureInfo.InvariantCulture, out int number)
            ? number
            : ToolErrors.Argument($"参数 {parameter} 要写成数字，收到 {text}。");
    }

    /// <summary>一次读取多个文件，结果按传入顺序分段交回；整体超长截断。单个条目的错误不阻断其它条目。</summary>
    private static ErrorOr<string> ReadFiles(string baseDirectory, ToolArguments arguments)
    {
        IReadOnlyList<string>? paths = arguments.List(new ToolName("paths"));
        if (paths is null || paths.Count == 0)
        {
            return ToolErrors.Argument("至少给一个文件路径。");
        }

        var text = new StringBuilder();
        foreach (string path in paths)
        {
            string section = $"=== {path} ===\n{ToolResult.Render(ReadOne(baseDirectory, path))}\n";
            if (text.Length + section.Length > TotalLimit)
            {
                string marker = $"…批量读取已截断，当前交回 {text.Length} 字符";
                if (text.Length + marker.Length > TotalLimit)
                {
                    text.Length = TotalLimit - marker.Length;
                }

                text.Append(marker);
                return text.ToString();
            }

            text.Append(section);
        }

        return text.ToString().TrimEnd();
    }

    /// <summary>把文本覆盖写入工作目录内的文件，父目录缺失时自动创建，已有文件直接覆盖；错误与拒绝以 ErrorOr 表达。编码按 encoding 参数，缺省 UTF-8 无 BOM。</summary>
    private static ErrorOr<string> WriteFile(string baseDirectory, ToolArguments arguments)
    {
        string? path = arguments.Text(new ToolName("path"));
        if (string.IsNullOrWhiteSpace(path))
        {
            return ToolErrors.Argument("缺少参数 path。");
        }

        string? content = arguments.Text(new ToolName("content"));
        if (content is null)
        {
            return ToolErrors.Argument("缺少参数 content。");
        }

        if (content.Length > ContentLimit)
        {
            return ToolErrors.Argument($"content 超过上限 {ContentLimit} 字符，本次写入不执行。");
        }

        ErrorOr<Encoding> encoding = ParseEncoding(arguments.Text(new ToolName("encoding")));
        if (encoding.IsError)
        {
            return encoding.ErrorsOrEmptyList;
        }

        ResolveResult resolved = Resolve(baseDirectory, path);
        if (resolved.Rejected is { } rejected)
        {
            return rejected;
        }

        if (Directory.Exists(resolved.Path))
        {
            return ToolErrors.Path($"{path} 是目录，不能写入。");
        }

        try
        {
            string? directory = Path.GetDirectoryName(resolved.Path);
            if (!string.IsNullOrEmpty(directory))
            {
                Directory.CreateDirectory(directory);
            }

            File.WriteAllText(resolved.Path, content, encoding.Value);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            return ToolErrors.WriteFailed($"写入 {path} 失败：{ex.Message}");
        }

        return $"已写入 {content.Length} 字符到 {path}。";
    }

    /// <summary>按行号范围替换文件内容，替换区间的原文须与 original 逐行一致；错误与拒绝以 ErrorOr 表达。行号从 1 起，replacement 行数可增减，为空时删除该范围，写回统一换行符并按 encoding 参数编码。</summary>
    private static ErrorOr<string> PatchFile(string baseDirectory, ToolArguments arguments)
    {
        string? path = arguments.Text(new ToolName("path"));
        if (string.IsNullOrWhiteSpace(path))
        {
            return ToolErrors.Argument("缺少参数 path。");
        }

        ErrorOr<int> from = ParseLineNumber("from", arguments.Text(new ToolName("from")), 0);
        if (from.IsError)
        {
            return from.ErrorsOrEmptyList;
        }

        if (from.Value < 1)
        {
            return ToolErrors.Argument("起始行号 from 从 1 起。");
        }

        ErrorOr<int> to = ParseLineNumber("to", arguments.Text(new ToolName("to")), 0);
        if (to.IsError)
        {
            return to.ErrorsOrEmptyList;
        }

        if (to.Value < 1)
        {
            return ToolErrors.Argument("结束行号 to 从 1 起。");
        }

        if (to.Value < from.Value)
        {
            return ToolErrors.Argument("结束行号 to 不能小于起始行号 from。");
        }

        string? original = arguments.Text(new ToolName("original"));
        if (original is null)
        {
            return ToolErrors.Argument("缺少参数 original。");
        }

        if (original.Length > ContentLimit)
        {
            return ToolErrors.Argument($"original 超过上限 {ContentLimit} 字符。");
        }

        string? replacement = arguments.Text(new ToolName("replacement"));
        if (replacement is null)
        {
            return ToolErrors.Argument("缺少参数 replacement。");
        }

        if (replacement.Length > ContentLimit)
        {
            return ToolErrors.Argument($"replacement 超过上限 {ContentLimit} 字符。");
        }

        ErrorOr<Encoding> encoding = ParseEncoding(arguments.Text(new ToolName("encoding")));
        if (encoding.IsError)
        {
            return encoding.ErrorsOrEmptyList;
        }

        ResolveResult resolved = Resolve(baseDirectory, path);
        if (resolved.Rejected is { } rejected)
        {
            return rejected;
        }

        if (Directory.Exists(resolved.Path))
        {
            return ToolErrors.Path($"{path} 是目录，不能替换。");
        }

        if (!File.Exists(resolved.Path))
        {
            return ToolErrors.Path($"{path} 不存在。");
        }

        string[] lines;
        try
        {
            lines = [.. File.ReadLines(resolved.Path)];
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            return ToolErrors.ReadFailed($"读取 {path} 失败：{ex.Message}");
        }

        if (from.Value > lines.Length)
        {
            return ToolErrors.Argument($"{path} 共 {lines.Length} 行，起始行 {from.Value} 超出。");
        }

        if (to.Value > lines.Length)
        {
            return ToolErrors.Argument($"{path} 共 {lines.Length} 行，结束行 {to.Value} 超出。");
        }

        string[] range = lines[(from.Value - 1)..to.Value];
        if (!range.SequenceEqual(SplitLines(original)))
        {
            return ToolErrors.Argument($"第 {from.Value} 到 {to.Value} 行的原文与 original 不一致，实际内容是：\n{Truncate(string.Join('\n', range))}");
        }

        string[] replacementLines = SplitLines(replacement);
        string[] newLines = [.. lines[..(from.Value - 1)], .. replacementLines, .. lines[to.Value..]];
        try
        {
            File.WriteAllText(resolved.Path, string.Join("\n", newLines), encoding.Value);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            return ToolErrors.WriteFailed($"写入 {path} 失败：{ex.Message}");
        }

        return $"已替换 {path} 的第 {from.Value} 到 {to.Value} 行（{range.Length} 行 → {replacementLines.Length} 行）。";
    }

    /// <summary>解析写回编码。缺省或空白为 UTF-8 无 BOM，只支持 UTF 族编码，未知值给出带实参的详细错误。</summary>
    private static ErrorOr<Encoding> ParseEncoding(string? name) =>
        string.IsNullOrWhiteSpace(name)
            ? new UTF8Encoding(false)
            : name.Trim().ToLowerInvariant() switch
            {
                "utf-8" => new UTF8Encoding(false),
                "utf-8-bom" => new UTF8Encoding(true),
                "utf-16" => Encoding.Unicode,
                "utf-16be" => Encoding.BigEndianUnicode,
                _ => ToolErrors.Argument($"encoding 只支持 utf-8、utf-8-bom、utf-16、utf-16be，收到 {name}。"),
            };

    /// <summary>按行拆文本并去行尾回车的行数组，空文本得空数组。</summary>
    private static string[] SplitLines(string text) => text.Length == 0
        ? []
        : [.. text.Split('\n').Select(line => line.TrimEnd('\r'))];

    /// <summary>单个文件的读取结果，按字符上限截断；错误与拒绝以 ErrorOr 表达。编码按 UTF-8，带 BOM 时按 BOM 识别。</summary>
    private static ErrorOr<string> ReadOne(string baseDirectory, string path)
    {
        ResolveResult resolved = Resolve(baseDirectory, path);
        if (resolved.Rejected is { } rejected)
        {
            return rejected;
        }

        if (Directory.Exists(resolved.Path))
        {
            return ToolErrors.Path($"{path} 是目录，用 ListDirectory 列出它的条目。");
        }

        if (!File.Exists(resolved.Path))
        {
            return ToolErrors.Path($"{path} 不存在。");
        }

        try
        {
            using StreamReader reader = new(resolved.Path);
            char[] buffer = new char[OutputLimit + 1];
            int offset = 0;
            while (offset < buffer.Length)
            {
                int read = reader.Read(buffer, offset, buffer.Length - offset);
                if (read == 0)
                {
                    break;
                }

                offset += read;
            }

            return Truncate(new string(buffer, 0, offset));
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            return ToolErrors.ReadFailed($"读取 {path} 失败：{ex.Message}");
        }
    }

    /// <summary>列出目录的直接子项，按名字排序并标注目录；错误与拒绝以 ErrorOr 表达。</summary>
    private static ErrorOr<string> ListDirectory(string baseDirectory, ToolArguments arguments)
    {
        string? path = arguments.Text(new ToolName("path"));
        path ??= string.Empty;

        ResolveResult resolved = Resolve(baseDirectory, path);
        if (resolved.Rejected is { } rejected)
        {
            return rejected;
        }

        if (File.Exists(resolved.Path))
        {
            return ToolErrors.Path($"{Display(path)} 是文件，用 ReadFile 读取它的内容。");
        }

        if (!Directory.Exists(resolved.Path))
        {
            return ToolErrors.Path($"{Display(path)} 不存在。");
        }

        IReadOnlyList<(string Name, bool IsDirectory)> items;
        try
        {
            items = [.. Directory.GetFileSystemEntries(resolved.Path)
                .Select(entry => (Path.GetFileName(entry), Directory.Exists(entry)))
                .OrderBy(item => item.Item1, StringComparer.Ordinal)];
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            return ToolErrors.ReadFailed($"列目录 {Display(path)} 失败：{ex.Message}");
        }

        if (items.Count == 0)
        {
            return $"目录 {Display(path)} 为空。";
        }

        var text = new StringBuilder();
        text.AppendLine($"{Display(path)} 的条目（{items.Count}）：");
        foreach ((string name, bool isDirectory) in items.Take(MaxEntries))
        {
            text.AppendLine($"  {name}{(isDirectory ? "/" : string.Empty)}");
        }

        if (items.Count > MaxEntries)
        {
            text.Append($"…仅列出前 {MaxEntries} 条");
        }

        return text.ToString().TrimEnd();
    }

    /// <summary>把相对路径解析为工作目录内的绝对路径，越界或不可解析时给出拒绝错误。不解析链接，工作目录内指向外部的链接不被拦截。</summary>
    private static ResolveResult Resolve(string baseDirectory, string path)
    {
        string full;
        try
        {
            full = Path.GetFullPath(path, baseDirectory);
        }
        catch (Exception ex) when (ex is ArgumentException or NotSupportedException or IOException)
        {
            return ResolveResult.Reject(ToolErrors.Path($"路径 {Display(path)} 无法解析：{ex.Message}"));
        }

        string relative = Path.GetRelativePath(baseDirectory, full);
        if (relative.StartsWith("..", StringComparison.Ordinal) || Path.IsPathRooted(relative))
        {
            return ResolveResult.Reject(ToolErrors.Path($"路径 {Display(path)} 不在工作目录内。"));
        }

        return new ResolveResult(full, null);
    }

    /// <summary>路径的展示。空路径即工作目录根。</summary>
    private static string Display(string path) => path.Length == 0 ? "工作目录根" : path;

    /// <summary>解析结果：工作目录内的绝对路径，或拒绝错误。</summary>
    private readonly record struct ResolveResult(string Path, Error? Rejected)
    {
        public static ResolveResult Reject(Error rejected) => new(string.Empty, rejected);
    }
}
