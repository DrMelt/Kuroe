using ErrorOr;
using Kuroe.Agent.Runs;
using Kuroe.Agent.Turns;
using Kuroe.Configuration;
using Kuroe.Shared.Agent;
using Kuroe.Shared.Agent.Runs;
using Kuroe.Shared.Agent.Turns;
using Kuroe.Shared.Workflows;
using Kuroe.Shared.Workflows.Flows;
using Kuroe.Shared.Workflows.Tasks;

namespace Kuroe.Workflows.Tasks;

/// <summary>流程推进器：收口一步、开下一步、按检查结论返工、取消传播，把能派的 agent 派出去。
/// 单个任务的推进要求已持有该任务的 Gate，ShutdownAsync 例外。</summary>
sealed class WorkflowDriver(
    TaskRegistry registry,
    RunDispatcher dispatcher,
    StepModelResolver models,
    SettingsProvider settings)
{
    private bool _stopping;

    /// <summary>要求持有任务 Gate：建首个单元并派发。</summary>
    internal void Start(AgentTask task)
    {
        task.AddUnit(null, null, 0);
        Pump(task);
    }

    /// <summary>要求持有任务 Gate：批准等待放行的单元，开下一步，返回被批准的单元数。</summary>
    internal int Approve(AgentTask task)
    {
        List<WorkUnit> waiting = [.. task.Units.Where(unit => unit.State == UnitState.AwaitingApproval)];
        foreach (WorkUnit unit in waiting)
        {
            MoveForward(task, unit, unit.StepCursor);
        }

        if (waiting.Count > 0)
        {
            task.Touch();
            Pump(task);
        }

        return waiting.Count;
    }

    /// <summary>要求持有任务 Gate：对被阻塞的单元再开一轮实施，itemIndex 为空时处理全部，返回被处理的单元数。</summary>
    internal int Rework(AgentTask task, int? itemIndex)
    {
        List<WorkUnit> blocked = [.. task.Units.Where(unit =>
            unit.State == UnitState.Blocked && (itemIndex is null || unit.ItemIndex == itemIndex))];
        foreach (WorkUnit unit in blocked)
        {
            unit.Rework(ImplementBefore(task.Flow, unit.StepCursor));
        }

        if (blocked.Count > 0)
        {
            task.Touch();
            Pump(task);
        }

        return blocked.Count;
    }

    /// <summary>要求持有任务 Gate：取消任务，在跑的 agent 与未走完的单元一并终止。</summary>
    internal static void Cancel(AgentTask task) => task.Cancel();

    /// <summary>退出时取消全部任务与 agent 并等待收口。</summary>
    internal async Task ShutdownAsync()
    {
        _stopping = true;
        foreach (TaskSnapshot snapshot in registry.Snapshots())
        {
            if (registry.Find(snapshot.Id) is { IsError: false } found)
            {
                lock (found.Value.Gate)
                {
                    found.Value.Cancel();
                }

                registry.Report(new ExecutionNotice(NoticeLevel.Warning, $"{snapshot.Id} 已取消。"));
            }
        }

        await dispatcher.ShutdownAsync();
        registry.Forget();
    }

    /// <summary>收口回调：决定该单元的去向，然后把能跑的都派出去。</summary>
    private void OnSettled(AgentRun run)
    {
        ErrorOr<AgentTask> found = registry.Find(run.Context.Task);
        if (found.IsError)
        {
            return;
        }

        AgentTask task = found.Value;
        NoticeLevel level;
        string notice;
        lock (task.Gate)
        {
            level = run.State switch
            {
                RunState.Succeeded => NoticeLevel.Done,
                RunState.Canceled => NoticeLevel.Warning,
                _ => NoticeLevel.Error,
            };

            notice = $"{run.Id} · {run.Context.Label} {run.State.Label()}（{task.Id}）";
            if (task.UnitFor(run.Context.ItemIndex) is { } unit && unit.State != UnitState.Canceled)
            {
                unit.Release();
                Advance(task, unit, run);
                Pump(task);
            }

            task.Touch();
        }

        registry.Report(new ExecutionNotice(level, notice));
    }

    /// <summary>要求持有任务 Gate。一步收口后决定该单元的去向。</summary>
    private void Advance(AgentTask task, WorkUnit unit, AgentRun run)
    {
        StepSpec spec = task.Flow.Steps[run.Context.StepIndex];
        if (run.State != RunState.Succeeded)
        {
            unit.Block();
            return;
        }

        if (spec.Role == RunRole.Plan && task.Plan?.Origin != run.Id)
        {
            run.MarkUncollected("规划步骤没有交回条目拆分，本步未收口。");
            unit.Block();
            return;
        }

        if (spec.Role == RunRole.Check)
        {
            if (unit.Verdict == UnitVerdict.NotChecked)
            {
                run.MarkUncollected("检查步骤没有交回结论，本步未收口。");
                unit.Block();

                return;
            }

            if (unit.Verdict == UnitVerdict.Rejected)
            {
                Retry(task, unit, spec);

                return;
            }
        }

        if (spec.Gate == StepGate.Review)
        {
            unit.AwaitApproval();
            return;
        }

        MoveForward(task, unit, run.Context.StepIndex);
    }

    /// <summary>检查不通过时按检查步骤的处置决定返工一轮或停在此处。</summary>
    private void Retry(AgentTask task, WorkUnit unit, StepSpec spec)
    {
        int limit = Math.Min(spec.AttemptLimit, settings.Current.Agent.MaxAttempts);
        int? implement = ImplementBefore(task.Flow, unit.StepCursor);
        if (spec.RejectAction != RejectAction.Retry || unit.Attempts >= limit || implement is null)
        {
            unit.Block();

            return;
        }

        unit.Rework(implement);
    }

    /// <summary>要求持有任务 Gate。某步走完后把单元推向下一步，需要时按条目展开。</summary>
    private static void MoveForward(AgentTask task, WorkUnit unit, int finishedIndex)
    {
        int next = finishedIndex + 1;
        if (next >= task.Flow.Count)
        {
            unit.Finish(next);
            return;
        }

        if (task.Flow.Steps[next].Scope == StepScope.PerItem && unit.Item is null && task.Plan is { } plan)
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

    /// <summary>要求持有任务 Gate。把所有待推进的单元能派出去的都派出去。</summary>
    private void Pump(AgentTask task)
    {
        if (_stopping)
        {
            return;
        }

        foreach (WorkUnit unit in task.Units.Where(unit =>
                     unit.State == UnitState.Working && !unit.Pending).ToList())
        {
            Dispatch(task, unit);
        }
    }

    /// <summary>要求持有任务 Gate。</summary>
    private void Dispatch(AgentTask task, WorkUnit unit)
    {
        if (unit.StepCursor >= task.Flow.Count)
        {
            unit.Finish(unit.StepCursor);
            return;
        }

        StepSpec spec = task.Flow.Steps[unit.StepCursor];
        ErrorOr<string> model = models.For(spec);
        if (model.IsError)
        {
            unit.Block();
            task.Journal.Append(new ErrorEntry(model.FirstError.Description));
            registry.Report(ExecutionNotice.From(model.ErrorsOrEmptyList, $"{task.Id} 停在步骤 {spec.Name}"));

            return;
        }

        AgentRun run = registry.NewRun(ContextComposer.ForStep(task, unit, unit.StepCursor, model.Value));
        task.Attach(unit, run);
        dispatcher.Dispatch(run, OnSettled);
    }

    /// <summary>该步骤之前最近的实施步骤，返工时退回它。</summary>
    private static int? ImplementBefore(Workflow flow, int index)
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

