using ErrorOr;
using Kuroe.Execution.Shared.Request;

namespace Kuroe.Execution.Shared.Runtime;

/// <summary>执行器绑定：按节点声明给出执行运行器工厂与本次执行的参数构造。
/// 绑定只描述能力，不执行，也不包含步骤标识或流程语义。</summary>
public interface IExecutionBinder
{
    /// <summary>本次执行使用的运行器工厂。</summary>
    IExecutionRunnerFactory RunnerFactory { get; }

    /// <summary>按本次执行上下文建立类型专属参数。</summary>
    ErrorOr<IExecutionParameters> Parameters(ExecutionParameterContext context);
}
