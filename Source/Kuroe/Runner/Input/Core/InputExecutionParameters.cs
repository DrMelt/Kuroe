using Kuroe.Tasks.Shared;
using Kuroe.Execution.Shared.Request;

namespace Kuroe.Runner.Input;

/// <summary>输入执行参数：等待用户回答时的提示文本与等待步骤键。</summary>
public sealed record InputExecutionParameters(
    string? Question,
    FlowStepPath Step) : IExecutionParameters;