using Kuroe.Flow.Control.Runtime.Graph;
using Kuroe.Control.Shared;
using Kuroe.Flow.Node.Shared.RuntimeGraph.Execution;

namespace Kuroe.Flow.Control.Runtime.Engine;

/// <summary>为图流程任务建立控制实例。</summary>
public sealed class GraphFlowControlFactory(
    IExecutionNodeDeclarationResolver declarations,
    ITaskExecutionGateway gateway) : IFlowControlFactory
{
    /// <inheritdoc/>
    public IFlowControl CreateControl(ITaskFlowContext task)
    {
        if (task.FlowState is not GraphFlowState state)
        {
            throw new InvalidOperationException("图流程控制工厂收到非图流程状态。");
        }

        return new GraphFlowControl(task, state, declarations, gateway);
    }
}