using Kuroe.Execution.Shared;
using Kuroe.Tasks.Shared;

namespace Kuroe.Runner;

/// <summary>输入执行参数：等待用户回答时的提示文本与等待步骤键。</summary>
public sealed record InputExecutionParameters(
    string? Question,
    FlowStepPath Step) : IExecutionParameters;