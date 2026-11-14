using Kuroe.Tasks.Shared;
using Kuroe.Node.Llm;
using Kuroe.Shared;
using Kuroe.Tool;

namespace Kuroe.Assistant.Flows;

/// <summary>内置对话流程的代码定义：流程名、开始端口、回话步骤与回话参数。流程状态由对话框架维护，不经过节点图。</summary>
internal static class DialogueFlow
{
    /// <summary>流程名。</summary>
    internal static FlowName Name { get; } = new("对话");

    /// <summary>代码流程实现的种类名，存档按它区分实现。</summary>
    internal const string Implementation = "dialogue";

    /// <summary>开始端口名：任务的初始输入由此进入流程。</summary>
    internal static PortName StartPort { get; } = new("目标");

    /// <summary>回话步骤的路径与标签。</summary>
    internal const string ReplyLabel = "回话";

    /// <summary>回话执行接收用户输入的端口名。</summary>
    internal static PortName InputPort { get; } = new("回答");

    /// <summary>工作区根目录端口，由宿主在任务启动时给出。</summary>
    internal static PortName WorkspacePort { get; } = new("工作区");

    /// <summary>等待用户输入时的提问。</summary>
    internal const string Question = "请输入";

    /// <summary>回话节点可用的能力白名单，写上级路径即放行整棵子树。文件只读工具可用，路径变量绑定工作区。</summary>
    internal static IReadOnlyList<ToolPath> Tools { get; } =
    [
        new ToolPath("GetLocalTime"),
        new ToolPath("KuroeInfo"),
        new ToolPath("file/read"),
        new ToolPath("source"),
        new ToolPath("git/read"),
        new ToolPath("dotnet/read"),
    ];

    /// <summary>回话节点的固定身份说明。</summary>
    private const string Identity = "你是Kuroe的内置助手，直接回应用户的输入。回复内容写在正常输出里。";

    /// <summary>回话节点的恒定系统指令块：身份说明与回复语言。</summary>
    internal static IReadOnlyList<string> SystemPrompt(string language) =>
    [
        Identity,
        $"使用「{language}」语言母语者语言风格回答。",
    ];

    /// <summary>回话执行参数：工具白名单、工作区路径变量与系统指令。</summary>
    internal static LlmParameters Parameters(string language) => new()
    {
        Tools = Tools,
        ToolPorts = ToolPorts(),
        SystemPrompt = SystemPrompt(language),
    };

    /// <summary>工具路径到路径变量映射的对应表，子树按上级路径给出。</summary>
    private static Dictionary<string, IReadOnlyDictionary<string, string>> ToolPorts() =>
        new()
        {
            ["file"] = RootPorts(),
            ["git"] = RootPorts(),
            ["dotnet"] = RootPorts(),
        };

    /// <summary>单个工具组的变量映射，root 指向工作区端口。</summary>
    private static Dictionary<string, string> RootPorts() =>
        new()
        {
            [PathVariables.RootName] = WorkspacePort.Value,
        };
}