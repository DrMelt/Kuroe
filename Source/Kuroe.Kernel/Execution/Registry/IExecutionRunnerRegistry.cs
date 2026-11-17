using Kuroe.Execution.Shared.Identity;
using Kuroe.Execution.Shared.Runtime;

namespace Kuroe.Execution.Registry;

/// <summary>执行器工厂注册表契约：按运行器键查找，并按执行目标建立运行器。</summary>
public interface IExecutionRunnerRegistry
{
    /// <summary>按运行器键查找执行器工厂，没有注册时为空。</summary>
    IExecutionRunnerFactory? Find(ExecutionRunnerKey key);

    /// <summary>为一次执行建立运行器。</summary>
    IExecutionRunner Create(
        IExecutionTarget target,
        ExecutionId execution);
}