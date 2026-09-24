using Kuroe.Agent.Runs;
using Kuroe.Agent.Turns;
using Kuroe.Shared.Agent;
using Kuroe.Shared.Agent.Runs;
using Kuroe.Shared.Workflows;
using Kuroe.Shared.Workflows.Tasks;
using Kuroe.Shared.Workflows.Flows;
using Kuroe.Workflows.Tasks;
using Microsoft.Agents.AI.Workflows;
using FlowWorkflow = Kuroe.Shared.Workflows.Flows.Workflow;

namespace Kuroe.Workflows.Engine;

/// <summary>流程入口：承接宿主的启动、批准与返工意图，把它转成对具体步骤的激活消息。
/// 单元状态改动在任务 Gate 内，消息投递在 Gate 外。</summary>
[SendsMessage(typeof(FlowMessage))]
internal sealed partial class FlowStartExecutor(
    TaskRegistry registry,
    TaskId taskId,
    FlowWorkflow flow) : Executor("start")
{
    [MessageHandler]
    public async ValueTask HandleAsync(FlowMessage message, IWorkflowContext context, CancellationToken cancellationToken = default)
    {
        if (registry.Find(taskId) is not { IsError: false } found)
        {
            return;
        }

        AgentTask task = found.Value;
        switch (message.Intent)
        {
            case FlowIntent.Start:
                await context.SendMessageAsync(Activate(0), "step:0", cancellationToken);
                break;

            case FlowIntent.Approved:
                await RouteReadyAsync(task, context, cancellationToken);
                break;

            case FlowIntent.Reworked:
                await ReworkAsync(task, message.ItemIndex, context, cancellationToken);
                break;
        }
    }

    /// <summary>批准后激活所有待跑的单元：引擎已把它们推进到下一步，这里按步骤去重投递激活消息。</summary>
    private static async ValueTask RouteReadyAsync(AgentTask task, IWorkflowContext context, CancellationToken cancellationToken)
    {
        List<(int Step, int? Item)> ready;
        lock (task.Gate)
        {
            ready = [.. task.Units
                .Where(unit => unit.State == UnitState.Working && !unit.Pending)
                .Select(unit => (unit.StepCursor, unit.ItemIndex))
                .Distinct()];
        }

        foreach ((int step, int? item) in ready)
        {
            await context.SendMessageAsync(Activate(step, item), $"step:{step}", cancellationToken);
        }
    }

    /// <summary>对被阻塞的单元再开一轮实施，itemIndex 为空时处理全部。</summary>
    private async ValueTask ReworkAsync(AgentTask task, int? itemIndex, IWorkflowContext context, CancellationToken cancellationToken)
    {
        List<WorkUnit> blocked;
        lock (task.Gate)
        {
            blocked = [.. task.Units.Where(unit =>
                unit.State == UnitState.Blocked && (itemIndex is null || unit.ItemIndex == itemIndex))];
        }

        foreach (WorkUnit unit in blocked)
        {
            int? implement;
            lock (task.Gate)
            {
                implement = ImplementBefore(flow, unit.StepCursor);
                unit.Rework(implement);
                task.Touch();
            }

            int target = implement ?? unit.StepCursor;
            await context.SendMessageAsync(Activate(target, unit.ItemIndex), $"step:{target}", cancellationToken);
        }
    }

    /// <summary>激活某步骤的单元，整步激活不携带条目。</summary>
    private static FlowMessage Activate(int step, int? item = null) =>
        new() { StepIndex = step, ItemIndex = item };

    /// <summary>该步骤之前最近的实施步骤，返工时退回它。</summary>
    private static int? ImplementBefore(FlowWorkflow flow, int index)
    {
        for (int candidate = index - 1; candidate >= 0; candidate--)
        {
            if (flow.Steps[candidate].Role == RunRole.Implement)
            {
                return candidate;
            }
        }

        return null;
    }
}