using Kuroe.Execution.Shared;

namespace Kuroe.Runner;

/// <summary>Llm 执行参数：指令、能力路径、系统指令、工具变量映射与本次执行使用的模型。</summary>
public sealed record LlmExecutionParameters(
    string? Prompt,
    IReadOnlyList<string> Tools,
    IReadOnlyList<string> SystemPrompt,
    IReadOnlyDictionary<string, IReadOnlyDictionary<string, string>> ToolPorts,
    string? Model) : IExecutionParameters
{
    /// <summary>本轮的会话历史种子，来自此前执行留下的 Llm 会话快照。不写入执行记录。</summary>
    public ExecutionRestoreState? ConversationSeed { get; init; }

    /// <summary>本轮要做的事：执行要求在请求最前，随后是轮次。没有内容时为空。</summary>
    public string? Instruction(ExecutionRequest request)
    {
        List<string> instruction = [];
        if (Prompt is { Length: > 0 } prompt)
        {
            instruction.Add(prompt);
        }

        if (request.Attempt > 1)
        {
            instruction.Add($"这是第 {request.Attempt} 轮实施。");
        }

        return instruction.Count == 0 ? null : string.Join('\n', instruction);
    }

    /// <summary>本轮系统指令：声明的恒定块，都没有时为空。</summary>
    public string? SystemPromptText() =>
        SystemPrompt is { Count: > 0 } blocks ? string.Join('\n', blocks) : null;
}