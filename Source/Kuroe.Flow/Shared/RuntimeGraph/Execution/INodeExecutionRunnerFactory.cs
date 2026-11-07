using Kuroe.Flow.Shared.StaticGraph.Graph;

namespace Kuroe.Flow.Shared.RuntimeGraph.Execution;

/// <summary>节点类型自带的执行器工厂：派发执行的类型实现它来提供自身运行器，不实现时使用默认执行器。</summary>
public interface INodeExecutionRunnerFactory
{
    /// <summary>为一次执行建立运行器，执行节点定义与执行输入一并给出。</summary>
    IExecutionRunner Create(ExecutableNode node, ExecutionId execution);
}