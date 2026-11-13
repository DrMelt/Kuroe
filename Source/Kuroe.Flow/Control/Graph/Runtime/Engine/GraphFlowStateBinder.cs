using Kuroe.Flow.Control;
using Kuroe.Flow.Control.Graph;
using Kuroe.Flow.Control.Graph.Nodes;
using Kuroe.Flow.Execution;
using Kuroe.Flow.Node.RuntimeGraph.Execution;
using Kuroe.Flow.Node.RuntimeGraph.Nodes;
using Kuroe.Flow.Tasks;

namespace Kuroe.Flow.Control.Graph.Engine;

/// <summary>图流程的状态绑定：建立图运行期，恢复节点与端口，处理中断执行并刷新恢复计划。</summary>
public sealed class GraphFlowStateBinder : IFlowStateBinder
{
    /// <inheritdoc/>
    public void Bind(ITaskFlowContext task)
    {
        if (task.FlowState is not GraphFlowState state)
        {
            throw new InvalidOperationException("图流程状态绑定收到非图流程状态。");
        }

        state.Attach(task);
        TaskRuntime runtime = state.Runtime;

        if (state.RestoreNodes.Count > 0 || state.RestorePorts.Count > 0)
        {
            runtime.Restore(state.RestoreNodes, state.RestorePorts);
        }

        if (task.Lifecycle == TaskLifecycle.Terminated)
        {
            runtime.Cancel();
            return;
        }

        foreach (GraphExecution execution in state.Executions
            .Where(execution => execution.State == ExecutionState.Interrupted))
        {
            GraphExecutionSnapshot snapshot = execution.Snapshot();
            RuntimeExecutable node = runtime.Executable(execution.Input.Node);
            if (snapshot.StartedAt is null)
            {
                if (node.Block?.Execution == execution.Id)
                {
                    node.ClearBlock();
                }

                node.RequestRerun();
                continue;
            }

            if (node.Block is null)
            {
                node.EnterBlocked(
                    [node.Path],
                    new NodeBlock("进程退出前未收口。", execution.Id, state.SavedAt));
            }
        }

        runtime.RefreshRestorePlans();
    }
}