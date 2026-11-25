using Kuroe.Ability.Node.Llm;
using Kuroe.Platform;
using Kuroe.Kernel.Tasks.Shared;

namespace Kuroe.Assistant.Dialogue.Flow;

/// <summary>内置对话流程的代码定义：流程名、开始端口、回话步骤、存档键与回话参数。流程状态不进入节点图。</summary>
internal static class DialogueFlow
{
    /// <summary>流程名。</summary>
    internal static FlowName Name { get; } = new("对话");

    /// <summary>代码流程实现的种类名，存档按该种类名区分实现。</summary>
    internal const string Implementation = "dialogue";

    /// <summary>开始端口名：任务的初始输入由此进入流程。</summary>
    internal static PortName StartPort { get; } = new("目标");

    /// <summary>模式端口名：任务建立时由宿主给出初始模式。</summary>
    internal static PortName ModePort { get; } = new("模式");

    /// <summary>模型端口名：任务建立时固定本会话使用的模型。</summary>
    internal static PortName ModelPort { get; } = new("模型");

    /// <summary>语言端口名：任务建立时固定本会话的回复语言。</summary>
    internal static PortName LanguagePort { get; } = new("语言");

    /// <summary>回话步骤的路径与标签。</summary>
    internal const string ReplyLabel = "回话";

    /// <summary>回话执行接收用户输入的端口名。</summary>
    internal static PortName InputPort { get; } = new("回答");

    /// <summary>工作区根目录端口，由宿主在任务启动时给出。</summary>
    internal static PortName WorkspacePort { get; } = new("工作区");

    /// <summary>等待用户输入时的提问。</summary>
    internal const string Question = "请输入";

    /// <summary>存档值里工作区根目录的键。</summary>
    internal const string WorkspaceKey = "workspace";

    /// <summary>存档值里模型名的键。</summary>
    internal const string ModelKey = "model";

    /// <summary>存档值里回复语言的键。</summary>
    internal const string LanguageKey = "language";

    /// <summary>存档值里模式键的键。</summary>
    internal const string ModeKey = "mode";

    /// <summary>回话步骤的路径。</summary>
    internal static FlowStepPath ReplyStep { get; } = FlowStepPath.Of(ReplyLabel);

    /// <summary>停在回话步骤的恢复种子。</summary>
    internal static FlowSeed ReplySeed { get; } = new([ReplyStep]);

    /// <summary>回话节点的固定身份说明。</summary>
    private const string Identity = "你是Kuroe的内置助手，直接回应用户的输入。回复内容写在正常输出里。";

    /// <summary>流程定义记录里的定义值。</summary>
    internal static IReadOnlyDictionary<string, string> DefinitionValues(string language) =>
        new Dictionary<string, string> { [LanguageKey] = language };

    /// <summary>回话节点的恒定系统指令块：身份说明、模式指令与回复语言。</summary>
    internal static IReadOnlyList<string> SystemPrompt(DialogueMode mode, string language) =>
    [
        Identity,
        mode.Instruction,
        $"使用「{language}」语言母语者语言风格回答。",
    ];

    /// <summary>回话执行参数：模式工具白名单、工作区路径变量与系统指令。</summary>
    internal static LlmParameters Parameters(DialogueMode mode, string language) => new()
    {
        Tools = mode.Tools,
        ToolPorts = ToolPorts(),
        SystemPrompt = SystemPrompt(mode, language),
    };

    /// <summary>工具路径到路径变量映射的对应表，子树按上级路径给出。</summary>
    private static Dictionary<string, IReadOnlyDictionary<string, string>> ToolPorts() =>
        new()
        {
            ["file"] = RootPorts(),
            ["git"] = RootPorts(),
            ["dotnet"] = RootPorts(),
        };

    /// <summary>单个工具组的变量映射，路径变量 <see cref="PathVariables.RootName"/> 指向工作区端口。</summary>
    private static Dictionary<string, string> RootPorts() =>
        new()
        {
            [PathVariables.RootName] = WorkspacePort.Value,
        };
}