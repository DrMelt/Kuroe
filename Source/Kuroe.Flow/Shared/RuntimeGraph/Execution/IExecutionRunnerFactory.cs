using Kuroe.Flow.Shared.StaticGraph.Graph;

namespace Kuroe.Flow.Shared.RuntimeGraph.Execution;

/// <summary>默认执行器工厂：为没有自带执行器工厂的派发节点建立运行器。替换实现即可替换这些节点的执行器。</summary>
public interface IExecutionRunnerFactory
{
    /// <summary>为一次执行建立运行器，过程记录随运行器一同建立。</summary>
    IExecutionRunner Create(ExecutableNode node, ExecutionId execution);
}