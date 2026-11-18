using Kuroe.Execution.Shared.Request;

namespace Kuroe.Execution.Shared.Runtime;

/// <summary>按执行参数建立运行器。执行运行器项目实现它。</summary>
public interface IExecutionRunnerFactory
{
    /// <summary>为一次执行建立运行器。</summary>
    IExecutionRunner Create(
        IExecutionParameters parameters,
        IExecutionRequestEntry requests);
}