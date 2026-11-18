using Kuroe.Execution.Shared.Identity;

namespace Kuroe.Execution.Shared.Runtime;

/// <summary>装配后的执行：目标描述与已解析的执行器工厂。</summary>
public sealed record ExecutionInvocation(
    ExecutionTarget Target,
    IExecutionRunnerFactory Factory);