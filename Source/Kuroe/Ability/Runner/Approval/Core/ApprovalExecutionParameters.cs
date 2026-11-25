using Kuroe.Kernel.Tasks.Shared;
using Kuroe.Kernel.Execution.Shared.Request;

namespace Kuroe.Ability.Runner.Approval;

/// <summary>审批执行参数：等待用户批准时的提示文本与等待步骤键。</summary>
public sealed record ApprovalExecutionParameters(
    string? Question,
    FlowStepPath Step) : IExecutionParameters;