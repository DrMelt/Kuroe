namespace Kuroe.Execution.Shared;

/// <summary>节点类型给出的执行器工厂类型，执行目标据此选择运行器。</summary>
public interface IExecutionRunnerFactoryProvider
{
    /// <summary>本类型使用的执行器工厂类型。</summary>
    Type RunnerFactoryType { get; }
}