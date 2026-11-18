using Kuroe.Execution.Shared.Request;
using Kuroe.Execution.Shared.Runtime;

namespace Kuroe.Execution.Shared.Identity;

/// <summary>一次执行的目标描述：标识、执行运行器键与类型专属参数。由流程控制构造，执行单元只读取。</summary>
public sealed record ExecutionTarget(
    ExecutionTargetId Id,
    ExecutionRunnerKey RunnerKey,
    IExecutionParameters Parameters);