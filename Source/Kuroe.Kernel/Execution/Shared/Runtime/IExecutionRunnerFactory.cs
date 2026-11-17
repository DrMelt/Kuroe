using Kuroe.Execution.Shared.Identity;

namespace Kuroe.Execution.Shared.Runtime;

/// <summary>按执行目标建立运行器。执行运行器项目实现它，注册键由工厂给出。</summary>
public interface IExecutionRunnerFactory
{
    /// <summary>本工厂的注册键，执行目标按它选择运行器。</summary>
    ExecutionRunnerKey Key { get; }

    /// <summary>为一次执行建立运行器。</summary>
    IExecutionRunner Create(IExecutionTarget target, ExecutionId execution);
}