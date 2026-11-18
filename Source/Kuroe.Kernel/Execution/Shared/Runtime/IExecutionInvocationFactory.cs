using ErrorOr;
using Kuroe.Execution.Shared.Identity;
using Kuroe.Execution.Shared.Restore;

namespace Kuroe.Execution.Shared.Runtime;

/// <summary>执行装配：按目标描述解析执行器工厂，并准备恢复。</summary>
public interface IExecutionInvocationFactory
{
    /// <summary>按执行运行器键解析工厂，组合为可运行的执行。</summary>
    ErrorOr<ExecutionInvocation> Create(ExecutionTarget target);

    /// <summary>校验恢复负载并装配一次恢复。</summary>
    ErrorOr<PreparedExecutionRestore> PrepareRestore(ExecutionRestoreContext context);
}