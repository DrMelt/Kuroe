using ErrorOr;
using Kuroe.Agent;
using Kuroe.Catalogs;
using Kuroe.Configuration;

namespace Kuroe.Workflows;

/// <summary>流程推进器：提交任务、收口一步、开下一步、批准与返工、取消传播。
/// 任务内的状态改动一律在持有该任务 Gate 时进行。</summary>
public sealed class WorkflowDriver(
    TaskRegistry registry,
    RunDispatcher dispatcher,
    WorkflowService flows,
    SettingsProvider settings,
    CatalogService catalog,
    AgentSessionFactory sessions)
{
    private bool _stopping;

    /// <summary>提交任务：按流程建任务并开第一步。</summary>
    public ErrorOr<TaskSnapshot> Submit(string goal, string? flowName, string? title)
    {
        if (string.IsNullOrWhiteSpace(goal))
        {
            return [TaskErrors.EmptyGoal()];
        }

        ErrorOr<Workflow> flow = string.IsNullOrWhiteSpace(flowName) ? flows.Default() : flows.Find(flowName);
        if (flow.IsError)
        {
            return flow.ErrorsOrEmptyList;
        }

        ErrorOr<string> model = ResolveModel(flow.Value.Steps[0]);
        if (model.IsError)
        {
            return model.ErrorsOrEmptyList;
        }

        AgentTask task = registry.Create(goal.Trim(), flow.Value, sessions.NewDialogue(), title);
        lock (task.Gate)
        {
            task.AddUnit(null, null, 0);
            Pump(task);
        }

        registry.Report(new ExecutionNotice(NoticeLevel.Info, $"{task.Id} 已提交，流程 {flow.Value.Name}。"));

        return task.Snapshot();
    }

    /// <summary>改任务标题。</summary>
    public ErrorOr<Success> Rename(TaskId id, string title)
    {
        ErrorOr<AgentTask> found = registry.Find(id);
        if (found.IsError)
        {
            return found.ErrorsOrEmptyList;
        }

        found.Value.Title = title;

        return Result.Success;
    }

    /// <summary>把前台对话切到该任务。</summary>
    public ErrorOr<Success> Use(TaskId id)
    {
        ErrorOr<AgentTask> found = registry.Find(id);
        if (found.IsError)
        {
            return found.ErrorsOrEmptyList;
        }

        registry.Use(id);

        return Result.Success;
    }

    /// <summary>批准等待放行的步骤，开下一步。</summary>
    public ErrorOr<Success> Approve(TaskId id)
    {
        ErrorOr<AgentTask> found = registry.Find(id);
        if (found.IsError)
        {
            return found.ErrorsOrEmptyList;
        }

        AgentTask task = found.Value;
        lock (task.Gate)
        {
            List<WorkUnit> waiting = [.. task.Units.Where(unit => unit.State == UnitState.AwaitingApproval)];
            if (waiting.Count == 0)
            {
                return [TaskErrors.NotAwaiting(id)];
            }

            foreach (WorkUnit unit in waiting)
            {
                MoveForward(task, unit, unit.StepCursor);
            }

            task.Touch();
            Pump(task);
        }

        registry.Report(new ExecutionNotice(NoticeLevel.Info, $"{id} 已批准，继续下一步。"));

        return Result.Success;
    }

    /// <summary>对被阻塞的单元再开一轮实施。itemIndex 为空时处理该任务全部被阻塞的单元。</summary>
    public ErrorOr<Success> Rework(TaskId id, int? itemIndex)
    {
        ErrorOr<AgentTask> found = registry.Find(id);
        if (found.IsError)
        {
            return found.ErrorsOrEmptyList;
        }

        AgentTask task = found.Value;
        lock (task.Gate)
        {
            List<WorkUnit> blocked = [.. task.Units.Where(unit =>
                unit.State == UnitState.Blocked && (itemIndex is null || unit.ItemIndex == itemIndex))];
            if (blocked.Count == 0)
            {
                return [TaskErrors.NotBlocked(id)];
            }

            foreach (WorkUnit unit in blocked)
            {
                unit.Rework(ImplementBefore(task.Flow, unit.StepCursor));
            }

            task.Touch();
            Pump(task);
        }

        registry.Report(new ExecutionNotice(NoticeLevel.Info, $"{id} 已返工。"));

        return Result.Success;
    }

    /// <summary>取消一个 agent。它所在的单元随后被阻塞，任务不再自动推进。</summary>
    public ErrorOr<Success> StopRun(RunId id)
    {
        ErrorOr<AgentRun> found = registry.FindRun(id);
        if (found.IsError)
        {
            return found.ErrorsOrEmptyList;
        }

        if (!found.Value.IsLive)
        {
            return [AgentErrors.RunSettled(id, "取消")];
        }

        found.Value.Cancel();

        return Result.Success;
    }

    /// <summary>取消任务：在跑的 agent 全部取消，未走完的单元不再推进。</summary>
    public ErrorOr<Success> StopTask(TaskId id)
    {
        ErrorOr<AgentTask> found = registry.Find(id);
        if (found.IsError)
        {
            return found.ErrorsOrEmptyList;
        }

        AgentTask task = found.Value;
        lock (task.Gate)
        {
            task.Cancel();
        }

        registry.Report(new ExecutionNotice(NoticeLevel.Warning, $"{id} 已取消。"));

        return Result.Success;
    }

    /// <summary>把 agent 的结论作为一条消息写进任务历史，后续对话才用得上它。</summary>
    public ErrorOr<Success> Adopt(RunId id)
    {
        ErrorOr<AgentRun> found = registry.FindRun(id);
        if (found.IsError)
        {
            return found.ErrorsOrEmptyList;
        }

        RunSnapshot snapshot = found.Value.Snapshot();
        if (snapshot.State != RunState.Succeeded || snapshot.Result is not { Length: > 0 } result)
        {
            return [AgentErrors.NothingToAdopt(id)];
        }

        ErrorOr<AgentTask> task = registry.Find(snapshot.Context.Task);
        if (task.IsError)
        {
            return task.ErrorsOrEmptyList;
        }

        task.Value.Adopt($"{id} 的结论（{snapshot.Context.Label}）：\n{result}");

        return Result.Success;
    }

    /// <summary>退出时取消全部任务与 agent 并等待收口。</summary>
    public async Task ShutdownAsync()
    {
        _stopping = true;
        foreach (TaskSnapshot snapshot in registry.Snapshots())
        {
            StopTask(snapshot.Id);
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
        ErrorOr<string> model = ResolveModel(spec);
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

    private ErrorOr<string> ResolveModel(StepSpec spec)
    {
        string? model = spec.Model ?? settings.Current.Agent.Model;
        if (string.IsNullOrWhiteSpace(model))
        {
            return [AgentErrors.ModelNotSelected()];
        }

        ErrorOr<Success> reachable = catalog.Check(model);

        return reachable.IsError ? reachable.ErrorsOrEmptyList : model;
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

