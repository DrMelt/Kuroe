namespace Kuroe.Execution.Shared;

/// <summary>按执行目标建立运行器。执行运行器项目实现它，注册键取实现类型。</summary>
public interface IExecutionRunnerFactory
{
    /// <summary>为一次执行建立运行器。</summary>
    IExecutionRunner Create(IExecutionTarget target, ExecutionId execution);
}