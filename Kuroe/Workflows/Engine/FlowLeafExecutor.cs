using ErrorOr;
using Kuroe.Agent.Runs;
using Kuroe.Agent.Turns;
using Kuroe.Configuration;
using Kuroe.Shared.Agent;
using Kuroe.Shared.Agent.Runs;
using Kuroe.Shared.Agent.Turns;
using Kuroe.Shared.Workflows;
using Kuroe.Shared.Workflows.Tasks;
using Kuroe.Shared.Workflows.Flows;
using Kuroe.Workflows.Tasks;
using Microsoft.Agents.AI.Workflows;

namespace Kuroe.Workflows.Engine;

/// <summary>流程中一片叶子的执行器：收到激活消息后为该叶子的单元派生 agent，收口后按契约、门控与检查结论决定去向。
/// 收拢叶的激活一律进入就绪判定，全部条目单元齐备后合并启动一次整体检查。
/// 单元状态改动在任务 Gate 内，agent 运行与消息投递在 Gate 外。</summary>
[SendsMessage(typeof(FlowMessage))]
internal sealed partial class FlowLeafExecutor(
    TaskRegistry registry,
    RunDispatcher dispatcher,
    NodeModelResolver models,
    SettingsProvider settings,
    TaskId taskId,
    int nodeIndex) : Executor($"node:{nodeIndex}")
{
    /// <summary>激活消息只认本叶子；整叶激活组装本叶全部工作单元，按条目激活只处理一条。</summary>
    [MessageHandler]
    public async ValueTask HandleAsync(FlowMessage message, IWorkflowContext context, CancellationToken cancellationToken = default)
    {
        if (message.NodeIndex != nodeIndex || message.Intent is not null)
        {
            return;
        }

        if (registry.Find(taskId) is not { IsError: false } found)
        {
            return;
        }

        AgentTask task = found.Value;
        if (task.Graph.IsFunnel(nodeIndex))
        {
            await FunnelActivateAsync(task, context, cancellationToken);
            return;
        }

        if (message.ItemIndex is not null)
        {
            if (UnitAt(task, message.ItemIndex) is { } unit)
            {
                await RunAndSettleAsync(task, unit, context, cancellationToken);
            }

            return;
        }

        List<WorkUnit> targets;
        lock (task.Gate)
        {
            targets = [.. task.Units.Where(unit => unit.State == UnitState.Working && unit.NodeCursor == nodeIndex)];
        }

        await Task.WhenAll(targets.Select(unit => RunAndSettleAsync(task, unit, context, cancellationToken).AsTask()));
    }

    /// <summary>定位单元：批准恢复的单元推进到本叶，非本叶或已收口的单元忽略。</summary>
    private WorkUnit? UnitAt(AgentTask task, int? itemIndex)
    {
        lock (task.Gate)
        {
            WorkUnit? unit = task.UnitFor(itemIndex);
            if (unit is null)
            {
                return null;
            }

            if (unit.State == UnitState.AwaitingApproval)
            {
                unit.AdvanceTo(nodeIndex);
                return unit;
            }

            return unit.State == UnitState.Working && unit.NodeCursor == nodeIndex ? unit : null;
        }
    }

    /// <summary>跑一个单元在本叶子的 agent：锁外派发并等待收口，锁内推进状态，锁外投递去向。</summary>
    private async ValueTask RunAndSettleAsync(AgentTask task, WorkUnit unit, IWorkflowContext context, CancellationToken cancellationToken)
    {
        LeafNode leaf = task.Graph[nodeIndex];
        AgentRun? run;
        lock (task.Gate)
        {
            if (unit.State != UnitState.Working || unit.NodeCursor != nodeIndex || task.State == TaskState.Canceled)
            {
                return;
            }

            ErrorOr<string> model = models.For(leaf);
            if (model.IsError)
            {
                unit.Block();
                task.Journal.Append(new ErrorEntry(model.FirstError.Description));
                registry.Report(ExecutionNotice.From(model.ErrorsOrEmptyList, $"{task.Id} 停在节点 {leaf.Name}"));
                return;
            }

            run = registry.NewRun(ContextComposer.ForLeaf(task, unit, leaf, model.Value));
            task.Attach(unit, run);
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
        }

        FlowAction? action = Settle(task, run);
        if (action is not null)
        {
            await RouteAsync(context, action, cancellationToken);
        }
    }

    /// <summary>要求持有任务 Gate 收口一个单元：按契约判定交回，按门控与检查结论决定去向。</summary>
    private FlowAction? Settle(AgentTask task, AgentRun run)
    {
        lock (task.Gate)
        {
            if (task.UnitFor(run.Context.ItemIndex) is not { } unit || unit.State == UnitState.Canceled)
            {
                return null;
            }

            unit.Release();
            LeafNode leaf = task.Graph[run.Context.NodeIndex];
            if (run.State != RunState.Succeeded)
            {
                unit.Block();
                return FlowAction.Pause;
            }

            if (leaf.Output == NodeOutput.Plan && task.Plan?.Origin != run.Id)
            {
                run.MarkUncollected("规划叶子没有交回条目拆分，本步未收口。");
                unit.Block();
                return FlowAction.Pause;
            }

            if (leaf.Output == NodeOutput.Review)
            {
                return SettleCheck(task, unit, run, leaf);
            }

            return leaf.Gate == NodeGate.Review ? AwaitApproval(unit) : Advance(task, unit);
        }
    }

    /// <summary>要求持有任务 Gate：逐条检查结论定向，未交回或拒绝时决定返工目标还是停下。</summary>
    private FlowAction? SettleCheck(AgentTask task, WorkUnit unit, AgentRun run, LeafNode leaf)
    {
        if (unit.LastCheck is not { } check || check.Origin != run.Id)
        {
            run.MarkUncollected("检查叶子没有交回结论，本步未收口。");
            unit.Block();
            return FlowAction.Pause;
        }

        if (!check.Passed)
        {
            return Retry(task, unit, leaf);
        }

        return leaf.Gate == NodeGate.Review ? AwaitApproval(unit) : Advance(task, unit);
    }
    /// <summary>要求持有任务 Gate：逐条检查拒绝后按处置决定返工一轮还是停下。轮次由检查结论给出，退回不再计数。</summary>
    private FlowAction? Retry(AgentTask task, WorkUnit unit, LeafNode leaf)
    {
        int limit = Math.Min(leaf.AttemptLimit, settings.Current.Agent.MaxAttempts);
        int? implement = task.Graph.ImplementBefore(nodeIndex);
        if (leaf.RejectAction == RejectAction.Retry
            && unit.LastCheck is { } check
            && check.Round < limit
            && implement is not null)
        {
            unit.Rework(implement);
            return FlowAction.Move(implement.Value, unit.ItemIndex);
        }

        unit.Block();
        return FlowAction.Pause;
    }

    /// <summary>要求持有任务 Gate：本叶收口后推进单元，按条目展开时建子单元并把下一叶整叶激活。</summary>
    private FlowAction? Advance(AgentTask task, WorkUnit unit)
    {
        int next = nodeIndex + 1;
        AdvanceUnit(task, unit, next);
        if (next >= task.Graph.Count)
        {
            return null;
        }

        return FlowAction.Move(next, unit.ItemIndex);
    }

    /// <summary>要求持有任务 Gate：推进一个单元到下一叶子。下一叶展开且本单元是整叶单元时按条目建子单元。</summary>
    internal static void AdvanceUnit(AgentTask task, WorkUnit unit, int next)
    {
        if (next >= task.Graph.Count)
        {
            unit.Finish(next);
            return;
        }

        if (task.Graph[next].Mode == NodeMode.PerItem && unit.Item is null && task.Plan is { } plan)
        {
            unit.Finish(next);
            foreach (PlanItem item in plan.Items)
            {
                task.AddUnit(item.Index, item, next).Inherit(unit);
            }

            return;
        }

        unit.AdvanceTo(next);
    }

    private static FlowAction? AwaitApproval(WorkUnit unit)
    {
        unit.AwaitApproval();
        return FlowAction.Pause;
    }

    /// <summary>收拢叶的激活：全部条目单元齐备后合并派一个整体检查 agent。幂等由抢占标志保证。</summary>
    private async ValueTask FunnelActivateAsync(AgentTask task, IWorkflowContext context, CancellationToken cancellationToken)
    {
        LeafNode leaf = task.Graph[nodeIndex];
        AgentRun? run;
        lock (task.Gate)
        {
            List<WorkUnit> members = [.. task.Units.Where(unit =>
                unit.ItemIndex is not null && unit.State == UnitState.Working
                && unit.NodeCursor == nodeIndex && !unit.Pending)];

            bool ready = task.Plan is { } plan
                && plan.Items.All(item => members.Any(member => member.ItemIndex == item.Index));
            if (!ready || !task.TryBeginFunnel(nodeIndex, leaf.Name, out int round))
            {
                return;
            }

            ErrorOr<string> model = models.For(leaf);
            if (model.IsError)
            {
                task.EndFunnel(nodeIndex);
                task.Journal.Append(new ErrorEntry(model.FirstError.Description));
                registry.Report(ExecutionNotice.From(model.ErrorsOrEmptyList, $"{task.Id} 停在节点 {leaf.Name}"));
                foreach (WorkUnit member in members)
                {
                    member.Block();
                }

                return;
            }

            run = registry.NewRun(ContextComposer.ForFunnel(task, members, leaf, model.Value, round));
            task.AttachRun(run);
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
        }

        await SettleFunnelAsync(task, run, context, cancellationToken);
    }
    /// <summary>收拢检查的收口：结论统一铺到全部条目单元，通过则整体推进，拒绝则整批退回实施叶重跑。</summary>
    private async ValueTask SettleFunnelAsync(AgentTask task, AgentRun run, IWorkflowContext context, CancellationToken cancellationToken)
    {
        FlowAction? action;
        lock (task.Gate)
        {
            task.EndFunnel(nodeIndex);
            LeafNode leaf = task.Graph[nodeIndex];
            CheckResult? check = task.FunnelCheck(leaf.Name);
            List<WorkUnit> targets = [.. task.Units.Where(unit =>
                unit.ItemIndex is not null && unit.State == UnitState.Working && unit.NodeCursor == nodeIndex)];

            if (run.State != RunState.Succeeded || check is null || check.Origin != run.Id)
            {
                foreach (WorkUnit target in targets)
                {
                    target.Block();
                }

                action = FlowAction.Pause;
            }
            else if (!check.Passed)
            {
                foreach (WorkUnit target in targets)
                {
                    target.RecordCheck(run, false, check.Findings);
                }

                int limit = Math.Min(leaf.AttemptLimit, settings.Current.Agent.MaxAttempts);
                int? implement = task.Graph.FunnelReturn(nodeIndex);
                if (leaf.RejectAction == RejectAction.Retry && check.Round < limit && implement is not null)
                {
                    foreach (WorkUnit target in targets)
                    {
                        target.Rework(implement);
                    }

                    action = FlowAction.Move(implement.Value);
                }
                else
                {
                    foreach (WorkUnit target in targets)
                    {
                        target.Block();
                    }

                    action = FlowAction.Pause;
                }
            }
            else
            {
                foreach (WorkUnit target in targets)
                {
                    target.RecordCheck(run, true, string.Empty);
                }

                int next = nodeIndex + 1;
                if (next >= task.Graph.Count)
                {
                    foreach (WorkUnit target in targets)
                    {
                        target.Finish(next);
                    }

                    action = null;
                }
                else
                {
                    foreach (WorkUnit target in targets)
                    {
                        target.AdvanceTo(next);
                    }

                    action = FlowAction.Move(next);
                }
            }
        }

        if (action is not null)
        {
            await RouteAsync(context, action, cancellationToken);
        }
    }

    /// <summary>锁外按收口结论投递激活消息或请求暂停。</summary>
    private static async ValueTask RouteAsync(IWorkflowContext context, FlowAction? action, CancellationToken cancellationToken)
    {
        switch (action)
        {
            case FlowAction.Route route:
                await context.SendMessageAsync(
                    new FlowMessage { NodeIndex = route.Node, ItemIndex = route.Item },
                    $"node:{route.Node}",
                    cancellationToken);
                break;

            case FlowAction.Halt:
                await context.RequestHaltAsync();
                break;
        }
    }
}

/// <summary>收口的决策结果，锁内计算后由执行器锁外按它投递或暂停。</summary>
internal abstract record FlowAction
{
    private FlowAction()
    {
    }

    /// <summary>向另一片叶子投递激活消息。</summary>
    public static FlowAction Move(int node, int? item = null) => new Route(node, item);

    /// <summary>请求暂停，等宿主批准或返工后恢复。</summary>
    public static FlowAction Pause { get; } = new Halt();

    /// <summary>向另一片叶子投递激活消息。</summary>
    public sealed record Route(int Node, int? Item) : FlowAction;

    /// <summary>请求暂停。</summary>
    public sealed record Halt() : FlowAction;
}