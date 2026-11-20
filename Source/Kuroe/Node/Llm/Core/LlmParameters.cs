using Kuroe.Runner.Llm;
using Kuroe.Tool;
using Kuroe.Kernel.Execution.Shared.Restore;

namespace Kuroe.Node.Llm;

/// <summary>Llm 类型的声明：对本次执行的额外要求、能力白名单与恒定系统指令块，由 Llm 类型实例承载。</summary>
public sealed record LlmParameters
{
    /// <summary>对本次执行的额外要求，与目标一起构成指令。</summary>
    public string? Prompt { get; init; }

    /// <summary>能力白名单，按工具路径前缀放行，写上级路径即放行整棵子树。未写或空时不给出任何能力。</summary>
    public IReadOnlyList<ToolPath> Tools { get; init; } = [];

    /// <summary>恒定系统指令块：作为系统指令置于请求最前，内容必须恒定以命中服务商前缀缓存。</summary>
    public IReadOnlyList<string> SystemPrompt { get; init; } = [];

    /// <summary>工具路径到路径变量映射的对应表，键是工具路径，值是变量名到输入端口名。</summary>
    public IReadOnlyDictionary<string, IReadOnlyDictionary<string, string>> ToolPorts { get; init; } =
        new Dictionary<string, IReadOnlyDictionary<string, string>>();

    /// <summary>按本次执行使用的模型、轮次与会话种子建立执行参数。</summary>
    public LlmExecutionParameters ToExecutionParameters(
        string? model,
        ExecutionRestoreState? seed = null,
        int attempt = 1,
        int maxAttempts = 1) =>
        new(Prompt, [.. Tools.Select(tool => tool.Value)], SystemPrompt, ToolPorts, model)
        {
            ConversationSeed = seed,
            Attempt = attempt,
            MaxAttempts = maxAttempts,
        };
}