using Kuroe.Executions.Runs;
using Kuroe.Executions.Turns;
using Kuroe.Shared.Executions;
using Kuroe.Shared.Executions.Runs;
using Kuroe.Shared.Executions.Turns;
using Kuroe.Shared.Workflows;
using Kuroe.Shared.Workflows.Flows;
using Kuroe.Shared.Workflows.Tasks;
using Kuroe.Workflows.Tasks;
using Microsoft.Agents.AI.Workflows;

namespace Kuroe.Workflows.Engine;

/// <summary>流程入口：承接宿主的启动、批准与返工意图，把它转成对具体执行节点的评估消息。
/// 状态改动在任务 Gate 内，消息投递在 Gate 外。</summary>
[SendsMessage(typeof(FlowMessage))]
internal sealed partial class FlowStartExecutor(TaskRegistry registry, TaskId taskId) : Executor("start")
{
    [MessageHandler]
    public async ValueTask HandleAsync(FlowMessage message, IWorkflowContext context, CancellationToken cancellationToken = default)
    {
        if (registry.Find(taskId) is not { IsError: false } found)
        {
            return;
        }

        WorkTask task = found.Value;
        switch (message.Intent)
        {
            case FlowIntent.Start:
            case FlowIntent.Approved:
                await RouteAsync(task, context, cancellationToken);
                break;

            case FlowIntent.Reworked:
                await ReworkAsync(task, message.Rerun ?? [], context, cancellationToken);
                break;
        }
    }

    /// <summary>启动：评估第一批根执行节点。放行：评估等待批准的执行节点。</summary>
    private static async ValueTask RouteAsync(WorkTask task, IWorkflowContext context, CancellationToken cancellationToken)
    {
        List<int> targets;
        lock (task.Gate)
        {
            IReadOnlyList<int> waiting = task.Runtime.AwaitingNodes;
            targets = [.. waiting.Count > 0 ? waiting : task.Runtime.Roots()];
        }

        foreach (int node in targets)
        {
            await context.SendMessageAsync(Activate(node), $"node:{node}", cancellationToken);
        }
    }

    /// <summary>返工：作废与放行已由宿主信号前的 Gate 内完成，这里只把重派消息投给目标执行节点。</summary>
    private static async ValueTask ReworkAsync(WorkTask task, IReadOnlyList<(int Blocked, int Node, int? Item)> targets, IWorkflowContext context, CancellationToken cancellationToken)
    {
        if (targets.Count == 0)
        {
            task.Journal.Append(new ErrorEntry("被阻塞的执行节点没有可作废重跑的目标，原地重试。"));
            return;
        }

        foreach ((int _, int node, int? item) in targets)
        {
            await context.SendMessageAsync(
                new FlowMessage { NodeIndex = node, ItemIndex = item },
                $"node:{node}",
                cancellationToken);
        }
    }

    /// <summary>激活某执行节点的评估消息，整节点不携带条目。</summary>
    private static FlowMessage Activate(int node) => new() { NodeIndex = node };
}