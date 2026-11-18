namespace Kuroe.Execution.Shared.Runtime;

/// <summary>节点类型给出的执行运行器工厂，执行装配携带它建立运行器。</summary>
public interface IExecutionRunnerFactoryProvider
{
    /// <summary>本类型使用的执行运行器工厂。</summary>
    IExecutionRunnerFactory RunnerFactory { get; }
}