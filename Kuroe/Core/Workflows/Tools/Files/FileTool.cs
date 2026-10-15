using System.Text;
using ErrorOr;
using Kuroe.Shared.Executions.Tools;

namespace Kuroe.Workflows.Tools.Files;

/// <summary>工作目录的文件访问工具：读写文本文件与列出目录条目。路径解析后必须落回工作目录内。</summary>
/// <remarks>容器装配用的载体。</remarks>
internal sealed class FileTool(string baseDirectory) : ITool
{
    /// <summary>ReadFile 缺省读取的行数，不传范围时只读文件开头这些行。</summary>
    private const int DefaultLines = 50;

    /// <summary>批量读取一次交回的总字符上限，超出后停止并提示。</summary>
    private const int TotalLimit = 40000;

    /// <summary>一次写入的字符上限，超出整段拒绝。</summary>
    private const int ContentLimit = 40000;

    /// <summary>目录列表的条目上限，超出截断并提示。</summary>
    private const int MaxEntries = 200;

    /// <summary>文件访问工具的分组。</summary>
    private static readonly ToolPath Group = new("files");

    /// <summary>读取单个文件指定行号范围。</summary>
    private static readonly ToolDefinition ReadFile = new(
        new ToolName("ReadFile"),
        "读取工作目录内文本文件指定行号范围的内容。from 与 to 是行号，缺省读前 50 行，to 超出文件行数时读到文件尾。",
        [
            new ToolParameter(new ToolName("path"), "相对工作目录的文件路径", Required: true),
            new ToolParameter(new ToolName("from"), "起始行号，数字，缺省 1"),
            new ToolParameter(new ToolName("to"), "结束行号，数字，缺省 50，超出文件行数时读到文件尾"),
        ],
        Group);

    /// <summary>列出目录的直接子项。</summary>
    private static readonly ToolDefinition ListDirectory = new(
        new ToolName("ListDirectory"),
        "列出工作目录内一个目录的直接子项。path 缺省时列出工作目录根的条目。",
        [new ToolParameter(new ToolName("path"), "相对工作目录的目录路径，缺省为工作目录根")],
        Group);

    /// <summary>一次读取多个文件。</summary>
    private static readonly ToolDefinition ReadFiles = new(
        new ToolName("ReadFiles"),
        "一次读取工作目录内的多个文本文件。paths 是相对工作目录的文件路径列表，结果按列表顺序分段交回。",
        [new ToolParameter(new ToolName("paths"), "相对工作目录的文件路径列表", Required: true, List: true)],
        Group);

    /// <summary>覆盖写入文件。</summary>
    private static readonly ToolDefinition WriteFile = new(
        new ToolName("WriteFile"),
        "把文本覆盖写入工作目录内的文件，文件不存在时创建，已存在时直接覆盖其内容。content 上限 40000 字符，父目录缺失时自动创建；encoding 指定写回编码，缺省 utf-8 不带 BOM。",
        [
            new ToolParameter(new ToolName("path"), "相对工作目录的文件路径", Required: true),
            new ToolParameter(new ToolName("content"), "要写入文件的文本", Required: true),
            new ToolParameter(new ToolName("encoding"), "写回编码，支持 utf-8、utf-8-bom、utf-16、utf-16be，缺省 utf-8"),
        ],
        Group);

    /// <summary>按行号范围替换文件内容。</summary>
    private static readonly ToolDefinition PatchFile = new(
        new ToolName("PatchFile"),
        "按行号范围替换工作目录内文件的内容。from 与 to 是行号，from 从 1 起；original 必须是该范围现在的原文，逐行不一致时调用被拒绝并在提示里回给实际内容；replacement 是替换后的新内容，行数可增减，为空时删除该范围；写回统一换行符，编码用 encoding 指定，缺省 utf-8 不带 BOM。",
        [
            new ToolParameter(new ToolName("path"), "相对工作目录的文件路径", Required: true),
            new ToolParameter(new ToolName("from"), "起始行号，数字，第 1 行起", Required: true),
            new ToolParameter(new ToolName("to"), "结束行号，数字", Required: true),
            new ToolParameter(new ToolName("original"), "该范围现在的原文，逐行与文件比对", Required: true),
            new ToolParameter(new ToolName("replacement"), "替换后的新内容，为空时删除该范围", Required: true),
            new ToolParameter(new ToolName("encoding"), "写回编码，支持 utf-8、utf-8-bom、utf-16、utf-16be，缺省 utf-8"),
        ],
        Group);

    /// <summary>本载体的函数声明。</summary>
    public IReadOnlyList<ToolFunction> Functions { get; } = Declare(new FileScope(baseDirectory, "工作目录"));

    /// <summary>把各定义绑上工作目录的调用体。</summary>
    private static IReadOnlyList<ToolFunction> Declare(FileScope scope) =>
    [
        ReadFile.Bind(arguments => Read(scope, arguments)),
        ListDirectory.Bind(arguments => List(scope, arguments)),
        ReadFiles.Bind(arguments => ReadMany(scope, arguments)),
        WriteFile.Bind(arguments => Write(scope, arguments)),
        PatchFile.Bind(arguments => Patch(scope, arguments)),
    ];

    /// <summary>读取指定文本文件指定行号范围的内容，按行截取并按字符上限截断；错误与拒绝以 ErrorOr 表达。行号从 1 起，结束行超出文件长度时读到文件尾。</summary>
    private static ErrorOr<string> Read(FileScope scope, ToolArguments arguments)
    {
        string? path = arguments.Text(new ToolName("path"));
        if (string.IsNullOrWhiteSpace(path))
        {
            return ToolErrors.Argument("缺少参数 path。");
        }

        ErrorOr<LineRange> range = LineRange.Parse(arguments, 1, DefaultLines);
        if (range.IsError)
        {
            return range.ErrorsOrEmptyList;
        }

        return scope.ReadRange(path, range.Value.From, range.Value.To, "ListDirectory");
    }

    /// <summary>一次读取多个文件，结果按传入顺序分段交回；整体超长截断。单个条目的错误不阻断其它条目。</summary>
    private static ErrorOr<string> ReadMany(FileScope scope, ToolArguments arguments)
    {
        IReadOnlyList<string>? paths = arguments.List(new ToolName("paths"));
        if (paths is null || paths.Count == 0)
        {
            return ToolErrors.Argument("至少给一个文件路径。");
        }

        var text = new StringBuilder();
        foreach (string path in paths)
        {
            string section = $"=== {path} ===\n{ToolResult.Render(scope.ReadWhole(path, "ListDirectory"))}\n";
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
    private static ErrorOr<string> Write(FileScope scope, ToolArguments arguments)
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

        ErrorOr<string> resolved = scope.Resolve(path);
        if (resolved.IsError)
        {
            return resolved.ErrorsOrEmptyList;
        }

        if (Directory.Exists(resolved.Value))
        {
            return ToolErrors.Path($"{path} 是目录，不能写入。");
        }

        try
        {
            string? directory = Path.GetDirectoryName(resolved.Value);
            if (!string.IsNullOrEmpty(directory))
            {
                Directory.CreateDirectory(directory);
            }

            File.WriteAllText(resolved.Value, content, encoding.Value);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            return ToolErrors.WriteFailed($"写入 {path} 失败：{ex.Message}");
        }

        return $"已写入 {content.Length} 字符到 {path}。";
    }

    /// <summary>按行号范围替换文件内容，替换区间的原文须与 original 逐行一致；错误与拒绝以 ErrorOr 表达。行号从 1 起，replacement 行数可增减，为空时删除该范围，写回统一换行符并按 encoding 参数编码。</summary>
    private static ErrorOr<string> Patch(FileScope scope, ToolArguments arguments)
    {
        string? path = arguments.Text(new ToolName("path"));
        if (string.IsNullOrWhiteSpace(path))
        {
            return ToolErrors.Argument("缺少参数 path。");
        }

        ErrorOr<LineRange> range = LineRange.Parse(arguments, 0, 0);
        if (range.IsError)
        {
            return range.ErrorsOrEmptyList;
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

        ErrorOr<string> resolved = scope.Resolve(path);
        if (resolved.IsError)
        {
            return resolved.ErrorsOrEmptyList;
        }

        if (Directory.Exists(resolved.Value))
        {
            return ToolErrors.Path($"{path} 是目录，不能替换。");
        }

        if (!File.Exists(resolved.Value))
        {
            return ToolErrors.Path($"{path} 不存在。");
        }

        string[] lines;
        try
        {
            lines = [.. File.ReadLines(resolved.Value)];
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            return ToolErrors.ReadFailed($"读取 {path} 失败：{ex.Message}");
        }

        if (range.Value.From > lines.Length)
        {
            return ToolErrors.Argument($"{path} 共 {lines.Length} 行，起始行 {range.Value.From} 超出。");
        }

        if (range.Value.To > lines.Length)
        {
            return ToolErrors.Argument($"{path} 共 {lines.Length} 行，结束行 {range.Value.To} 超出。");
        }

        string[] current = lines[(range.Value.From - 1)..range.Value.To];
        if (!current.SequenceEqual(SplitLines(original)))
        {
            return ToolErrors.Argument($"第 {range.Value.From} 到 {range.Value.To} 行的原文与 original 不一致，实际内容是：\n{FileScope.Truncate(string.Join('\n', current))}");
        }

        string[] replacementLines = SplitLines(replacement);
        string[] newLines = [.. lines[..(range.Value.From - 1)], .. replacementLines, .. lines[range.Value.To..]];
        try
        {
            File.WriteAllText(resolved.Value, string.Join("\n", newLines), encoding.Value);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            return ToolErrors.WriteFailed($"写入 {path} 失败：{ex.Message}");
        }

        return $"已替换 {path} 的第 {range.Value.From} 到 {range.Value.To} 行（{current.Length} 行 → {replacementLines.Length} 行）。";
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

    /// <summary>列出目录的直接子项，按名字排序并标注目录；错误与拒绝以 ErrorOr 表达。</summary>
    private static ErrorOr<string> List(FileScope scope, ToolArguments arguments)
    {
        string? path = arguments.Text(new ToolName("path"));
        path ??= string.Empty;

        ErrorOr<string> resolved = scope.Resolve(path);
        if (resolved.IsError)
        {
            return resolved.ErrorsOrEmptyList;
        }

        if (File.Exists(resolved.Value))
        {
            return ToolErrors.Path($"{scope.Display(path)} 是文件，用 ReadFile 读取它的内容。");
        }

        if (!Directory.Exists(resolved.Value))
        {
            return ToolErrors.Path($"{scope.Display(path)} 不存在。");
        }

        IReadOnlyList<(string Name, bool IsDirectory)> items;
        try
        {
            items = [.. Directory.GetFileSystemEntries(resolved.Value)
                .Select(entry => (Path.GetFileName(entry), Directory.Exists(entry)))
                .OrderBy(item => item.Item1, StringComparer.Ordinal)];
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            return ToolErrors.ReadFailed($"列目录 {scope.Display(path)} 失败：{ex.Message}");
        }

        if (items.Count == 0)
        {
            return $"目录 {scope.Display(path)} 为空。";
        }

        var text = new StringBuilder();
        text.AppendLine($"{scope.Display(path)} 的条目（{items.Count}）：");
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
}
