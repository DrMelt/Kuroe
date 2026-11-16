using Kuroe.Execution.Shared;
using Kuroe.Tasks.Shared;

namespace Kuroe.Runner.Approval;

/// <summary>审批执行参数：等待用户批准时的提示文本与等待步骤键。</summary>
public sealed record ApprovalExecutionParameters(
    string? Question,
    FlowStepPath Step) : IExecutionParameters;