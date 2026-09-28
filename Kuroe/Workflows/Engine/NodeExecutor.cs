using ErrorOr;
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
using Run = Kuroe.Executions.Runs.Run;

namespace Kuroe.Workflows.Engine;

/// <summary>流程里一个执行节点的执行器：收到评估消息后按图就绪判定启动实例，run 收口后发布产出并沿出边通知下游。
/// 批准放行与返工重派也走同一个评估入口。状态改动在任务 Gate 内，run 运行与消息投递在 Gate 外。</summary>
[SendsMessage(typeof(FlowMessage))]
internal sealed partial class NodeExecutor(
    TaskRegistry registry,
    RunDispatcher dispatcher,
    NodeModelResolver models,
    TaskId taskId,
    RuntimeExecutable node) : Executor($"node:{node.Index}")
{
    /// <summary>评估消息只认本执行节点：纯静态拆分就地产出，等待批准的放行，其余按输入就绪启动实例。</summary>
    [MessageHandler]
    public async ValueTask HandleAsync(FlowMessage message, IWorkflowContext context, CancellationToken cancellationToken = default)
    {
        if (message.NodeIndex != node.Index || message.Intent is not null)
        {
            return;
        }

        if (registry.Find(taskId) is not { IsError: false } found)
        {
            return;
        }

        WorkTask task = found.Value;
        List<int> notify = [];
        List<TaskFlow.RunStarter> starters = [];
        lock (task.Gate)
        {
            TaskFlow.EvaluateResult result = task.Runtime.Evaluate(node);
            notify.AddRange(result.Notify);
            starters.AddRange(result.Starters);
        }

        foreach (int next in notify)
        {
            await context.SendMessageAsync(Activate(next), $"node:{next}", cancellationToken);
        }

        if (starters.Count != 0)
        {
            await Task.WhenAll(starters.Select(starter =>
                DispatchAndSettleAsync(task, starter, context, cancellationToken).AsTask()));
        }

        await MaybeHaltAsync(task, context);
    }

    /// <summary>任务停在待批准或阻塞时请求暂停，让宿主循环停在信号上；其余情况运行自行收口。</summary>
    private static async ValueTask MaybeHaltAsync(WorkTask task, IWorkflowContext context)
    {
        bool park;
        lock (task.Gate)
        {
            park = task.Runtime.HasAwaiting || task.Runtime.HasBlocked;
        }

        if (park)
        {
            await context.RequestHaltAsync();
        }
    }

    /// <summary>为一个实例装配、派发并等待收口，再按收口计划通知下游或重派返工实例。</summary>
    private async ValueTask DispatchAndSettleAsync(WorkTask task, TaskFlow.RunStarter starter, IWorkflowContext context, CancellationToken cancellationToken)
    {
        ExecutableNode executable = node.Executable;
        Run? run;
        lock (task.Gate)
        {
            if (task.State == TaskState.Canceled)
            {
                node.ReleaseActive();
                return;
            }

            ErrorOr<string> model = models.For(executable);
            if (model.IsError)
            {
                node.ReleaseActive();
                node.EnterBlocked([(node.Index, null)]);
                task.Journal.Append(new ErrorEntry(model.FirstError.Description));
                registry.Report(ExecutionNotice.From(model.ErrorsOrEmptyList, $"{task.Id} 停在节点 {executable.Name}"));
                return;
            }

            run = registry.NewRun(ContextComposer.ForExecutable(task, node, starter.Item, model.Value));
            task.Attach(run);
        }
        if (run is null)
        {
            return;
        }

        try
        {
            await dispatcher.DispatchAsync(run, cancellationToken);
        }
        catch (OperationCanceledException)
        {
            // 取消已由调度侧收口为取消状态，这里退出后按状态处理
        }

        TaskFlow.SettlePlan plan;
        lock (task.Gate)
        {
            node.ReleaseActive();
            plan = task.Runtime.Settle(run);
        }

        foreach (int next in plan.Notify)
        {
            await context.SendMessageAsync(Activate(next), $"node:{next}", cancellationToken);
        }

        foreach ((int nodeIndex, int? item) in plan.Rerun)
        {
            await context.SendMessageAsync(
                new FlowMessage { NodeIndex = nodeIndex, ItemIndex = item },
                $"node:{nodeIndex}",
                cancellationToken);
        }
    }

    private static FlowMessage Activate(int nodeIndex) => new() { NodeIndex = nodeIndex };
}
