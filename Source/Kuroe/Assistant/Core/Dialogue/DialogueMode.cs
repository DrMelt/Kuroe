using Kuroe.Tool;

namespace Kuroe.Assistant.Dialogue;

/// <summary>内置对话的能力模式：决定回话执行可用的工具与恒定指令。</summary>
public sealed class DialogueMode
{
    private DialogueMode(
        string key,
        string displayName,
        IReadOnlyList<ToolPath> tools,
        string instruction)
    {
        Key = key;
        DisplayName = displayName;
        Tools = tools;
        Instruction = instruction;
    }

    /// <summary>只读取工作区与状态，不修改文件。</summary>
    public static DialogueMode Read { get; } = new(
        "read",
        "只读",
        [
            new ToolPath("GetLocalTime"),
            new ToolPath("KuroeInfo"),
            new ToolPath("file/read"),
            new ToolPath("source"),
            new ToolPath("git/read"),
            new ToolPath("dotnet/read"),
        ],
        "当前为只读模式：只读取工作区与查询状态，不修改文件。");

    /// <summary>可以修改工作区文件，不改变仓库与构建产物。</summary>
    public static DialogueMode Edit { get; } = new(
        "edit",
        "更改",
        [
            new ToolPath("GetLocalTime"),
            new ToolPath("KuroeInfo"),
            new ToolPath("file/read"),
            new ToolPath("file/write"),
            new ToolPath("source"),
            new ToolPath("git/read"),
            new ToolPath("dotnet/read"),
        ],
        "当前为更改模式：可以修改工作区文件。仓库改动与构建、测试、格式化命令不在当前模式的能力范围。");

    /// <summary>全部模式，按只读与更改排列。</summary>
    public static IReadOnlyList<DialogueMode> All { get; } = [Read, Edit];

    /// <summary>默认模式。</summary>
    public static DialogueMode Default => Read;

    /// <summary>模式键，写入流程状态。</summary>
    public string Key { get; }

    /// <summary>显示名。</summary>
    public string DisplayName { get; }

    /// <summary>回话执行的工具白名单。</summary>
    internal IReadOnlyList<ToolPath> Tools { get; }

    /// <summary>回话执行的模式指令。</summary>
    internal string Instruction { get; }

    /// <summary>按键取模式，缺失或未知时取默认模式。</summary>
    public static DialogueMode Parse(string? key) =>
        All.FirstOrDefault(mode => string.Equals(mode.Key, key, StringComparison.OrdinalIgnoreCase)) ?? Default;
}