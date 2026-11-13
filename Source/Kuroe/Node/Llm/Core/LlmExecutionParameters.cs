using Kuroe.Execution.Shared;
using Kuroe.Tool;

namespace Kuroe.Node.Llm;

/// <summary>Llm 执行的类型专属参数：节点声明的能力、指令与本次执行使用的模型。不引用图节点类型。</summary>
public sealed record LlmExecutionParameters(
    LlmParameters? Parameters,
    string? Model,
    string Kind) : IExecutionParameters
{
    /// <summary>本轮要做的事：节点要求在请求最前，随后是轮次。</summary>
    public string Instruction(ExecutionRequest request)
    {
        List<string> instruction = [];
        if (Parameters?.Prompt is { Length: > 0 } prompt)
        {
            instruction.Add(prompt);
        }

        if (request.Attempt > 1)
        {
            instruction.Add($"这是第 {request.Attempt} 轮实施。");
        }

        return string.Join('\n', instruction);
    }

    /// <summary>本轮系统指令：节点声明的恒定块，都没有时为空。</summary>
    public string? SystemPrompt() =>
        Parameters?.SystemPrompt is { Count: > 0 } blocks ? string.Join('\n', blocks) : null;

    /// <summary>本轮可用的工具路径：节点声明的白名单，加按需交回输出端口附上的交回能力，以及所有执行节点都有的上报与查看通道。</summary>
    public IReadOnlyList<ToolPath> ToolPaths(IReadOnlyList<ExecutionOutputPort> outputs)
    {
        List<ToolPath> paths = [.. Parameters?.Tools ?? []];
        if (outputs.Any(port => port.Required))
        {
            paths.Add(new ToolPath(ContractTools.Port));
        }

        paths.Add(new ToolPath(ContractTools.CannotComplete));
        paths.Add(new ToolPath(ContractTools.Status));

        return paths;
    }
}