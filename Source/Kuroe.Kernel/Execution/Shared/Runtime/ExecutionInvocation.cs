using Kuroe.Execution.Shared.Identity;
using Kuroe.Execution.Shared.Request;

namespace Kuroe.Execution.Shared.Runtime;

/// <summary>一次执行的装配：步骤标识、运行器工厂与类型专属参数。</summary>
public sealed record ExecutionInvocation(
    ExecutionStepId Step,
    IExecutionRunnerFactory Factory,
    IExecutionParameters Parameters);