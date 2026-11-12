using ErrorOr;
using Kuroe.Flow.Persistence.Files.Shared;
using Kuroe.Flow.Persistence.Saving.Shared;
using Kuroe.Flow.Graph.Records;
using Kuroe.Flow.Runtime.Engine;
using Kuroe.Flow.Shared.Model;
using Kuroe.Flow.Shared.RuntimeGraph.Execution;
using Kuroe.Flow.Shared.RuntimeGraph.Ports;
using Kuroe.Flow.Shared.RuntimeGraph.Tasks;
using Kuroe.Flow.Shared.StaticGraph.Flow;
using Kuroe.Flow.Shared.StaticGraph.Graph;
using Kuroe.Flow.Shared.StaticGraph.Ports;
using ExecutableNode = Kuroe.Flow.Shared.StaticGraph.Graph.ExecutableNode;

namespace Kuroe.Flow.Runtime.Tasks;

/// <summary>一个任务域内的任务动作：提交、重新执行、终止与改名。前置条件与回执在这里判定，状态改动经推进器落地。</summary>
/// <remarks>装配任务动作所需的注册表、推进器、执行目标解析、宿主开始输入表与宿主开始输入钩子。
/// 任务号与任务列表的可见范围由注册表所在的任务域决定。</remarks>
public sealed class TaskActions(
    TaskRegistry registry,
    IFlowRuntime driver,
    IExecutionTargetResolver targets,
    IHostStartInputs hostStartInputs,
    IHostTaskStart host,
    ITaskIdSource ids,
    Kuroe.Flow.Execution.ExecutionRestorerRegistry? restorers = null,
    IFlowSnapshotSource? snapshots = null,
    ITaskRecordCoordinator? records = null,
    ITaskLeaseStore? leases = null)
{
    private readonly TaskRegistry _registry = registry;
    private readonly IFlowRuntime _driver = driver;
    private readonly IExecutionTargetResolver _targets = targets;
    private readonly Kuroe.Flow.Execution.ExecutionRestorerRegistry? _restorers = restorers;
    private readonly IHostStartInputs _hostStartInputs = hostStartInputs;
    private readonly IHostTaskStart _host = host;
    private readonly ITaskIdSource _ids = ids;
    private readonly IFlowSnapshotSource? _snapshots = snapshots;
    private readonly ITaskRecordCoordinator? _records = records;
    private readonly ITaskLeaseStore? _leases = leases;

    /// <summary>按编译结果提交：解析宿主开始输入表、校验开始输入与开始直连下游的宿主声明、分配任务号、占用任务键、取注入的开始端口值，再建任务并启动推进。
    /// observer 收到该任务全部执行的过程事件。</summary>
    public ErrorOr<TaskSnapshot> SubmitCompiled(
        StartInputs inputs,
        CompiledFlow flow,
        string? title,
        ProcessObserver? observer = null)
    {
        NodeGraph graph = flow.Graph;

        // 宿主输入表在提交时解析，流程编译不涉及这份配置
        ErrorOr<IReadOnlyDictionary<PortName, string>> resolved = _hostStartInputs.Resolve(flow.Model);
        if (resolved.IsError)
        {
            return resolved.ErrorsOrEmptyList;
        }

        IReadOnlyDictionary<PortName, string> hostInputs = resolved.Value;

        if (CheckInputs(graph.StartPorts, hostInputs, inputs) is { Count: > 0 } inputErrors)
        {
            return inputErrors;
        }

        StartInputs effectiveInputs = FillDefaults(inputs, graph.StartPorts, hostInputs);
        string taskTitle = TaskTitle.Compose(effectiveInputs, flow.Model.Name, title);

        TaskId id = _ids.Next();

        GraphFlowDefinitionRecord? flowSnapshot = null;
        if (_snapshots is { } snapshotSource)
        {
            ErrorOr<GraphFlowDefinitionRecord> captured = snapshotSource.Capture(flow.Model.Name);
            if (captured.IsError)
            {
                return captured.ErrorsOrEmptyList;
            }

            flowSnapshot = captured.Value;
        }

        ErrorOr<IPortTexts> startTexts = _host.Prepare(id, hostInputs);
        if (startTexts.IsError)
        {
            return startTexts.ErrorsOrEmptyList;
        }

        if (CheckStartTexts(hostInputs, startTexts.Value) is { Count: > 0 } startErrors)
        {
            return startErrors;
        }

        // 等待型节点不进入派发路径；派发节点在静态值全部就绪后再要求执行目标可解析，无模型端口的类型由解析器给空声明
        StaticPortValues staticPorts = new(graph, new StartPortValues(graph.StartPorts, startTexts.Value, effectiveInputs));
        foreach (InputBinding binding in graph.StartOutgoing)
        {
            if (graph[binding.Target] is not ExecutableNode target
                || graph.ShapeOf(target.Path) is { DispatchesExecutions: false }
                || !staticPorts.FullyResolved(target))
            {
                continue;
            }

            ErrorOr<INodeDeclaration> declaration = _targets.For(target, staticPorts.For(target));
            if (declaration.IsError)
            {
                return declaration.ErrorsOrEmptyList;
            }
        }

        if (_leases is { } leaseStore)
        {
            ErrorOr<Success> lease = leaseStore.TryAcquire(id);
            if (lease.IsError)
            {
                return lease.ErrorsOrEmptyList;
            }
        }

        WorkTask task = _registry.Add(
            id,
            effectiveInputs,
            startTexts.Value,
            flow,
            _restorers,
            taskTitle,
            observer);
        task.FlowSnapshot = flowSnapshot;

        lock (task.Gate)
        {
            _driver.Start(task);
        }

        // 提交是任务身份首次跨会话可见的点，等待这一份存档落盘；其余动作只标记待写，由后台合并写盘
        PersistImmediately(task.Id);

        return task.Snapshot();
    }

    /// <summary>开始输入必须覆盖承载内容、不带固定值、不带默认值且未绑定宿主输入的开始端口，且只落在用户端口上。
    /// 带默认值的端口可以省略，提交了空白值仍报错。</summary>
    private static List<Error> CheckInputs(
        IReadOnlyList<Port> ports,
        IReadOnlyDictionary<PortName, string> hostInputs,
        StartInputs inputs)
    {
        List<PortName> required =
        [
            .. ports
                .Where(port => StartInputRules.RequiresInput(port, hostInputs.ContainsKey(port.Name)))
                .Select(port => port.Name),
        ];

        List<StartInput> supplied = [.. inputs.Values.DistinctBy(input => input.Port)];

        return
        [
            .. required.Where(port => inputs.Text(port) is null).Select(TaskErrors.MissingInput),
            .. ports
                .Where(port => StartInputRules.AcceptsInput(port, hostInputs.ContainsKey(port.Name))
                    && inputs.Text(port.Name) is { } text
                    && string.IsNullOrWhiteSpace(text))
                .Select(port => TaskErrors.BlankInput(port.Name)),
            .. inputs.Values
                .GroupBy(input => input.Port)
                .Where(group => group.Count() > 1)
                .Select(group => TaskErrors.DuplicateInput(group.Key)),
            .. supplied
                .Where(input => ports.Find(input.Port) is null)
                .Select(input => TaskErrors.UnknownInput(input.Port)),
            .. supplied
                .Where(input => KindOf(input.Port) == StartInputKind.Host)
                .Select(input => TaskErrors.HostInput(input.Port)),
            .. supplied
                .Where(input => KindOf(input.Port) == StartInputKind.Fixed)
                .Select(input => TaskErrors.ConstantInput(input.Port)),
            .. supplied
                .Where(input => KindOf(input.Port) == StartInputKind.Automatic)
                .Select(input => TaskErrors.NonContentInput(input.Port)),
        ];

        StartInputKind? KindOf(PortName port) => ports.Find(port) is { } declared
            ? StartInputRules.Kind(declared, hostInputs.ContainsKey(port))
            : null;
    }

    /// <summary>把省略的默认值端口补进开始输入，已提交的值保持不变。</summary>
    private static StartInputs FillDefaults(
        StartInputs inputs,
        IReadOnlyList<Port> ports,
        IReadOnlyDictionary<PortName, string> hostInputs)
    {
        List<StartInput> values = [.. inputs.Values];
        foreach (Port port in ports)
        {
            if (StartInputRules.AcceptsInput(port, hostInputs.ContainsKey(port.Name))
                && port.Default is { Length: > 0 } defaultValue
                && inputs.Text(port.Name) is null)
            {
                values.Add(new StartInput(port.Name, defaultValue));
            }
        }

        return values.Count == inputs.Values.Count ? inputs : new StartInputs(values);
    }

    /// <summary>宿主必须为每个绑定端口给出非空值。</summary>
    private static List<Error> CheckStartTexts(
        IReadOnlyDictionary<PortName, string> hostInputs,
        IPortTexts values)
    {
        List<Error> errors = [];
        foreach (PortName port in hostInputs.Keys)
        {
            string? text = values.Text(port);
            if (text is null)
            {
                errors.Add(TaskErrors.MissingHostValue(port));
            }
            else if (string.IsNullOrWhiteSpace(text))
            {
                errors.Add(TaskErrors.BlankHostValue(port));
            }
        }

        return errors;
    }

    /// <summary>标记任务状态已变，由存档协调器在后台写盘，交互路径不等写盘。</summary>
    private void Persist(TaskId id) => _records?.MarkDirty(id);

    /// <summary>写入该任务并等待落盘，用于任务身份变更与终止等需要立即可见的动作。</summary>
    private void PersistImmediately(TaskId id) => _records?.FlushAsync(id).GetAwaiter().GetResult();

    /// <summary>暂停任务：停止新的推进与派发，在跑的执行就地中断。要求任务未完成且未终止。</summary>
    public ErrorOr<Success> Pause(TaskId id)
    {
        ErrorOr<WorkTask> found = _registry.Find(id);
        if (found.IsError)
        {
            return found.ErrorsOrEmptyList;
        }

        WorkTask task = found.Value;
        lock (task.Gate)
        {
            ErrorOr<Success> paused = task.Pause();
            if (paused.IsError)
            {
                return paused;
            }

            _driver.Pause(task);
        }

        Persist(task.Id);

        return Result.Success;
    }

    /// <summary>恢复任务：暂停时恢复控制状态，存在恢复计划时保留控制状态，两者都派发恢复计划并唤醒推进。</summary>
    public ErrorOr<Success> Resume(TaskId id)
    {
        ErrorOr<WorkTask> found = _registry.Find(id);
        if (found.IsError)
        {
            return found.ErrorsOrEmptyList;
        }

        WorkTask task = found.Value;
        lock (task.Gate)
        {
            if (task.Lifecycle == TaskLifecycle.Terminated)
            {
                return [TaskErrors.Terminated(id)];
            }

            if (task.Lifecycle == TaskLifecycle.Paused)
            {
                ErrorOr<Success> resumed = task.Resume();
                if (resumed.IsError)
                {
                    return resumed;
                }
            }
            else if (task.Lifecycle != TaskLifecycle.Active
                || task.Completed
                || !task.Runtime.HasRestorePlans)
            {
                return [TaskErrors.NotPaused(id)];
            }

            _driver.ResumeTask(task);
        }

        Persist(task.Id);

        return Result.Success;
    }

    /// <summary>改任务标题。</summary>
    public ErrorOr<Success> Rename(TaskId id, string title)
    {
        ErrorOr<WorkTask> found = _registry.Find(id);
        if (found.IsError)
        {
            return found.ErrorsOrEmptyList;
        }

        found.Value.Title = title;
        Persist(found.Value.Id);

        return Result.Success;
    }

    /// <summary>对阻塞的执行节点重新执行，放弃恢复，保留恢复数据。暂停任务先重新执行再恢复推进。</summary>
    public ErrorOr<Success> Rerun(TaskId id)
    {
        ErrorOr<WorkTask> found = _registry.Find(id);
        if (found.IsError)
        {
            return found.ErrorsOrEmptyList;
        }

        WorkTask task = found.Value;
        int rerun;
        lock (task.Gate)
        {
            if (task.Lifecycle == TaskLifecycle.Terminated)
            {
                return [TaskErrors.Terminated(id)];
            }

            rerun = _driver.Rerun(task);
            if (rerun == 0)
            {
                return [TaskErrors.NotBlocked(id)];
            }

            if (task.Lifecycle == TaskLifecycle.Paused)
            {
                ErrorOr<Success> resumed = task.Resume();
                if (resumed.IsError)
                {
                    return resumed;
                }

                _driver.ResumeTask(task, continueRestores: false);
            }
        }

        Persist(id);

        return Result.Success;
    }

    /// <summary>停止一个已启动的执行。它所在的执行节点随后被阻塞，Llm 节点可随任务恢复。</summary>
    public ErrorOr<Success> StopExecution(ExecutionId id)
    {
        ErrorOr<GraphExecution> found = _registry.FindExecution(id);
        if (found.IsError)
        {
            return found.ErrorsOrEmptyList;
        }

        GraphExecution execution = found.Value;
        ErrorOr<WorkTask> owner = _registry.Find(execution.Input.Task);
        if (owner.IsError)
        {
            return owner.ErrorsOrEmptyList;
        }

        lock (owner.Value.Gate)
        {
            return _driver.StopExecution(owner.Value, execution.Id);
        }
    }

    /// <summary>终止任务：在跑的执行随之中断，未走完的执行节点不再推进，之后不可恢复。</summary>
    public ErrorOr<Success> StopTask(TaskId id)
    {
        ErrorOr<WorkTask> found = _registry.Find(id);
        if (found.IsError)
        {
            return found.ErrorsOrEmptyList;
        }

        ErrorOr<Success> stopped = _driver.Terminate(found.Value);
        if (!stopped.IsError)
        {
            PersistImmediately(found.Value.Id);
            _leases?.Release(found.Value.Id);
        }

        return stopped;
    }

    /// <summary>丢掉已完成或已终止的任务，删除它们的存档并放开占用。</summary>
    public IReadOnlyList<TaskId> ClearSettled()
    {
        IReadOnlyList<TaskId> cleared = _registry.ClearSettled();
        if (_records is { } coordinator)
        {
            foreach (TaskId id in cleared)
            {
                coordinator.Remove(id);
            }
        }

        if (_leases is { } leaseStore)
        {
            foreach (TaskId id in cleared)
            {
                leaseStore.Release(id);
            }
        }

        return cleared;
    }

    /// <summary>等待当前待写状态落盘，不改任务状态。</summary>
    public Task FlushAsync() => _records?.FlushAsync() ?? Task.CompletedTask;

    /// <summary>退出时先中断任务与执行并等待收口，再写全部任务存档，放开占用，最后丢弃任务域。</summary>
    public async Task ShutdownAsync()
    {
        await _driver.ShutdownAsync().ConfigureAwait(false);

        if (_records is { } coordinator)
        {
            foreach (TaskId id in _registry.Ids())
            {
                coordinator.MarkDirty(id);
            }

            await coordinator.FlushAsync().ConfigureAwait(false);
        }

        _leases?.ReleaseAll();
        _registry.Forget();
    }
}