using ErrorOr;
using Kuroe.Agent;
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
using FlowWorkflow = Kuroe.Shared.Workflows.Flows.Workflow;

namespace Kuroe.Workflows.Engine;

/// <summary>流程中一个步骤的执行器：收到激活消息后为该步骤的单元派生 agent，收口后按角色、门控与检查结论决定去向。
/// 单元状态改动在任务 Gate 内，agent 运行与消息投递在 Gate 外。</summary>
[SendsMessage(typeof(FlowMessage))]
internal sealed partial class FlowStepExecutor(
    TaskRegistry registry,
    RunDispatcher dispatcher,
    StepModelResolver models,
    SettingsProvider settings,
    TaskId taskId,
    int stepIndex,
    FlowWorkflow flow) : Executor($"step:{stepIndex}")
{
    /// <summary>激活消息只认本步骤；整步激活组装本步骤全部工作单元，按条目激活只处理一条。</summary>
    [MessageHandler]
    public async ValueTask HandleAsync(FlowMessage message, IWorkflowContext context, CancellationToken cancellationToken = default)
    {
        if (message.StepIndex != stepIndex || message.Intent is not null)
        {
            return;
        }

        if (registry.Find(taskId) is not { IsError: false } found)
        {
            return;
        }

        AgentTask task = found.Value;
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
            targets = [.. task.Units.Where(unit => unit.State == UnitState.Working && unit.StepCursor == stepIndex)];
        }

        await Task.WhenAll(targets.Select(unit => RunAndSettleAsync(task, unit, context, cancellationToken).AsTask()));
    }

    /// <summary>定位单元：批准恢复的单元前进到本步，非本步或已收口的单元忽略。</summary>
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
                unit.AdvanceTo(stepIndex);
                return unit;
            }

            return unit.State == UnitState.Working && unit.StepCursor == stepIndex ? unit : null;
        }
    }

    /// <summary>跑一个单元在本步骤的 agent：锁外派发并等待收口，锁内推进状态，锁外投递去向。</summary>
    private async ValueTask RunAndSettleAsync(AgentTask task, WorkUnit unit, IWorkflowContext context, CancellationToken cancellationToken)
    {
        StepSpec spec = flow.Steps[stepIndex];

        AgentRun? run;
        lock (task.Gate)
        {
            if (unit.State != UnitState.Working || unit.StepCursor != stepIndex || task.State == TaskState.Canceled)
            {
                return;
            }

            ErrorOr<string> model = models.For(spec);
            if (model.IsError)
            {
                unit.Block();
                task.Journal.Append(new ErrorEntry(model.FirstError.Description));
                registry.Report(ExecutionNotice.From(model.ErrorsOrEmptyList, $"{task.Id} 停在步骤 {spec.Name}"));
                return;
            }

            run = registry.NewRun(ContextComposer.ForStep(task, unit, stepIndex, model.Value));
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
/// <summary>要求持有任务 Gate 收口一个单元：按角色判定交回，按门控与检查结论决定去向。</summary>
    private FlowAction? Settle(AgentTask task, AgentRun run)
    {
        lock (task.Gate)
        {
            if (task.UnitFor(run.Context.ItemIndex) is not { } unit || unit.State == UnitState.Canceled)
            {
                return null;
            }

            unit.Release();
            StepSpec spec = flow.Steps[run.Context.StepIndex];
            if (run.State != RunState.Succeeded)
            {
                unit.Block();
                return FlowAction.Pause;
            }

            if (spec.Role == RunRole.Plan && task.Plan?.Origin != run.Id)
            {
                run.MarkUncollected("规划步骤没有交回条目拆分，本步未收口。");
                unit.Block();
                return FlowAction.Pause;
            }

            if (spec.Role == RunRole.Check)
            {
                return SettleCheck(task, unit, run, spec);
            }

            return spec.Gate == StepGate.Review ? AwaitApproval(unit) : Advance(task, unit);
        }
    }

    /// <summary>要求持有任务 Gate：检查结论定向，未交回或拒绝时决定返工目标还是停下。</summary>
    private FlowAction? SettleCheck(AgentTask task, WorkUnit unit, AgentRun run, StepSpec spec)
    {
        if (unit.Verdict == UnitVerdict.NotChecked)
        {
            run.MarkUncollected("检查步骤没有交回结论，本步未收口。");
            unit.Block();
            return FlowAction.Pause;
        }

        if (unit.Verdict == UnitVerdict.Rejected)
        {
            return Retry(unit, spec);
        }

        return spec.Gate == StepGate.Review ? AwaitApproval(unit) : Advance(task, unit);
    }

    /// <summary>要求持有任务 Gate：按检查步骤的处置决定返工一轮还是停下。</summary>
    private FlowAction? Retry(WorkUnit unit, StepSpec spec)
    {
        int limit = Math.Min(spec.AttemptLimit, settings.Current.Agent.MaxAttempts);
        int? implement = ImplementBefore(flow, stepIndex);
        if (spec.RejectAction == RejectAction.Retry && unit.Attempts < limit && implement is not null)
        {
            unit.Rework(implement);
            return FlowAction.Move(implement.Value, unit.ItemIndex);
        }

        unit.Block();
        return FlowAction.Pause;
    }

    private static FlowAction? AwaitApproval(WorkUnit unit)
    {
        unit.AwaitApproval();
        return FlowAction.Pause;
    }

    /// <summary>要求持有任务 Gate：本步收口后推进单元，按条目展开时建子单元并把下一步整步激活。</summary>
    private FlowAction? Advance(AgentTask task, WorkUnit unit)
    {
        int next = stepIndex + 1;
        if (next >= flow.Count)
        {
            unit.Finish(next);
            return null;
        }

        if (flow.Steps[next].Scope == StepScope.PerItem && unit.Item is null && task.Plan is { } plan)
        {
            unit.Finish(next);
            foreach (PlanItem item in plan.Items)
            {
                task.AddUnit(item.Index, item, next).Inherit(unit);
            }

            return FlowAction.Move(next);
        }

        unit.AdvanceTo(next);
        return FlowAction.Move(next, unit.ItemIndex);
    }

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

    /// <summary>锁外按收口结论投递激活消息或请求暂停。</summary>
    private static async ValueTask RouteAsync(IWorkflowContext context, FlowAction? action, CancellationToken cancellationToken)
    {
        switch (action)
        {
            case FlowAction.Route route:
                await context.SendMessageAsync(
                    new FlowMessage { StepIndex = route.Step, ItemIndex = route.Item },
                    $"step:{route.Step}",
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

    /// <summary>向另一步骤投递激活消息。</summary>
    public static FlowAction Move(int step, int? item = null) => new Route(step, item);

    /// <summary>请求暂停，等宿主批准或返工后恢复。</summary>
    public static FlowAction Pause { get; } = new Halt();

    /// <summary>向另一步骤投递激活消息。</summary>
    public sealed record Route(int Step, int? Item) : FlowAction;

    /// <summary>请求暂停。</summary>
    public sealed record Halt() : FlowAction;
}