using Kuroe.Flow.Shared.RuntimeGraph.Execution;
using Kuroe.Flow.Shared.StaticGraph.Graph;

namespace Kuroe.Flow.Runtime;

/// <summary>按节点类型选择执行器：类型自带执行器时用类型实现，否则用默认执行器。</summary>
public sealed class ExecutionRunnerSource(IExecutionRunnerFactory fallback) : IExecutionRunnerSource
{
    /// <summary>为一次执行建立运行器。</summary>
    public IExecutionRunner Create(ExecutableNode node, ExecutionId execution, ExecutionInput input)
    {
        if (node.Type is INodeExecutionRunnerFactory factory)
        {
            return factory.Create(node, execution, input);
        }

        if (node.Type.ModelPort is not null)
        {
            return fallback.Create(node, execution, input);
        }

        throw new InvalidOperationException($"节点类型 {node.Type.Kind.Value} 既没有自带执行器，也没有模型端口。");
    }
}