using Kuroe.Flow.Shared.StaticGraph.Graph;

namespace Kuroe.Flow.Shared.RuntimeGraph.Execution;

/// <summary>按执行节点建立一次执行的运行器。节点类型自带执行器时取类型实现，其余取默认执行器。</summary>
public interface IExecutionRunnerSource
{
    /// <summary>为一次执行建立运行器，执行节点定义与执行输入一并给出。</summary>
    IExecutionRunner Create(ExecutableNode node, ExecutionId execution);
}