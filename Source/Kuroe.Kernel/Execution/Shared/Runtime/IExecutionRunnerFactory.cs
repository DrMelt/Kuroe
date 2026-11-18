using Kuroe.Execution.Shared.Identity;
using Kuroe.Execution.Shared.Request;

namespace Kuroe.Execution.Shared.Runtime;

/// <summary>按执行参数建立运行器。执行运行器项目实现它，注册键由工厂给出。</summary>
public interface IExecutionRunnerFactory
{
    /// <summary>本工厂的注册键，装配按它选择执行器工厂。</summary>
    ExecutionRunnerKey Key { get; }

    /// <summary>为一次执行建立运行器。</summary>
    IExecutionRunner Create(IExecutionParameters parameters, ExecutionId execution);
}