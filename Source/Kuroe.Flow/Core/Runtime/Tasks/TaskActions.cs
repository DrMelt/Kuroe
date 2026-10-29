using ErrorOr;
using Kuroe.Flow.Runtime.Engine;
using Kuroe.Flow.Shared.Model;
using Kuroe.Flow.Shared.RuntimeGraph.Execution;
using Kuroe.Flow.Shared.RuntimeGraph.Ports;
using Kuroe.Flow.Shared.RuntimeGraph.Tasks;
using Kuroe.Flow.Shared.StaticGraph.Flow;
using Kuroe.Flow.Shared.StaticGraph.Graph;
using ExecutableNode = Kuroe.Flow.Shared.StaticGraph.Graph.ExecutableNode;

namespace Kuroe.Flow.Runtime.Tasks;

/// <summary>一个任务域内的任务动作：提交、返工、取消与改名。前置条件与回执在这里判定，状态改动经推进器落地。</summary>
/// <remarks>装配任务动作所需的注册表、推进器、执行目标解析与宿主开始输入钩子。
/// 任务号与任务列表的可见范围由注册表所在的任务域决定。</remarks>
public sealed class TaskActions(
    TaskRegistry registry,
    IFlowRunner driver,
    IExecutionTargetResolver targets,
    IHostTaskStart host)
{
    private readonly TaskRegistry _registry = registry;
    private readonly IFlowRunner _driver = driver;
    private readonly IExecutionTargetResolver _targets = targets;
    private readonly IHostTaskStart _host = host;

    /// <summary>按编译结果提交：校验开始输入与开始直连下游的宿主声明、分配任务号、取注入的开始端口值，再建任务并启动推进。
    /// 任务号被宿主资源占用时换下一个任务号重来。onText 与 onEntry 收到该任务全部执行的过程事件。</summary>
    public ErrorOr<TaskSnapshot> SubmitCompiled(
        StartInputs inputs,
        CompiledFlow flow,
        string? title,
        Action<string>? onText = null,
        Action<JournalEntry>? onEntry = null)
    {
        INodeGraph graph = flow.Graph;
        IReadOnlyDictionary<PortName, string> hostInputs = flow.Model.HostStartInputs;

        if (CheckInputs(graph.Start, hostInputs, inputs) is { Count: > 0 } inputErrors)
        {
            return inputErrors;
        }

        string taskTitle = TaskTitle.Compose(inputs, flow.Model.Name, title);

        while (true)
        {
            TaskId id = _registry.Allocate();

            ErrorOr<IPortTexts> startTexts = _host.Prepare(id, hostInputs, flow.Model.Name, taskTitle);
            if (startTexts.IsError)
            {
                if (startTexts.FirstError.Type == ErrorType.Conflict)
                {
                    continue;
                }

                return startTexts.ErrorsOrEmptyList;
            }

            if (CheckStartTexts(hostInputs, startTexts.Value) is { Count: > 0 } startErrors)
            {
                return startErrors;
            }

            // 不启动执行的节点不需要宿主声明；开始直连的派发节点在开始端口值就绪后再要求声明可解析
            StaticPortValues staticPorts = new(graph, startTexts.Value);
            foreach (FlowEdge edge in graph.Outgoing(graph.Start.Path))
            {
                if (graph[edge.Target] is not ExecutableNode target
                    || graph.ShapeOf(target.Path) is { DispatchesExecutions: false })
                {
                    continue;
                }

                ErrorOr<INodeDeclaration> declaration = _targets.For(target, staticPorts.For(target));
                if (declaration.IsError)
                {
                    return declaration.ErrorsOrEmptyList;
                }
            }

            WorkTask task = _registry.Add(
                id,
                inputs,
                startTexts.Value,
                staticPorts,
                flow,
                taskTitle,
                onText,
                onEntry);

            lock (task.Gate)
            {
                _driver.Start(task);
            }

            return task.Snapshot();
        }
    }

    /// <summary>开始输入必须逐个覆盖不带固定值且未绑定宿主输入的开始端口，且只落在用户端口上。</summary>
    private static List<Error> CheckInputs(
        StartNode start,
        IReadOnlyDictionary<PortName, string> hostInputs,
        StartInputs inputs)
    {
        List<(PortName Name, string? Text)> required =
        [
            .. start.Outputs
                .Where(port => port.Value is null && !hostInputs.ContainsKey(port.Name))
                .Select(port => (port.Name, Text: inputs.Text(port.Name))),
        ];

        List<StartInput> supplied = [.. inputs.Values.DistinctBy(input => input.Port)];

        return
        [
            .. required.Where(entry => entry.Text is null).Select(entry => TaskErrors.MissingInput(entry.Name)),
            .. required
                .Where(entry => entry.Text is { } text && string.IsNullOrWhiteSpace(text))
                .Select(entry => TaskErrors.BlankInput(entry.Name)),
            .. inputs.Values
                .GroupBy(input => input.Port)
                .Where(group => group.Count() > 1)
                .Select(group => TaskErrors.DuplicateInput(group.Key)),
            .. supplied
                .Where(input => start.FindPort(input.Port) is null)
                .Select(input => TaskErrors.UnknownInput(input.Port)),
            .. supplied
                .Where(input => hostInputs.ContainsKey(input.Port))
                .Select(input => TaskErrors.HostInput(input.Port)),
            .. supplied
                .Where(input => start.FindPort(input.Port) is { Value: not null })
                .Select(input => TaskErrors.ConstantInput(input.Port)),
        ];
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

    /// <summary>改任务标题。</summary>
    public ErrorOr<Success> Rename(TaskId id, string title)
    {
        ErrorOr<WorkTask> found = _registry.Find(id);
        if (found.IsError)
        {
            return found.ErrorsOrEmptyList;
        }

        found.Value.Title = title;

        return Result.Success;
    }

    /// <summary>对被阻塞的执行节点再开一轮实施。</summary>
    public ErrorOr<Success> Rework(TaskId id)
    {
        ErrorOr<WorkTask> found = _registry.Find(id);
        if (found.IsError)
        {
            return found.ErrorsOrEmptyList;
        }

        WorkTask task = found.Value;
        int reworked;
        lock (task.Gate)
        {
            reworked = _driver.Rework(task);
        }

        if (reworked == 0)
        {
            return [TaskErrors.NotBlocked(id)];
        }

        return Result.Success;
    }

    /// <summary>取消一个执行。它所在的执行节点随后被阻塞，任务不再自动推进。</summary>
    public ErrorOr<Success> StopExecution(ExecutionId id)
    {
        ErrorOr<Execution> found = _registry.FindExecution(id);
        if (found.IsError)
        {
            return found.ErrorsOrEmptyList;
        }

        if (!found.Value.IsLive)
        {
            return [ExecutionErrors.ExecutionSettled(id, "取消")];
        }

        found.Value.Cancel();

        return Result.Success;
    }

    /// <summary>取消任务：在跑的执行全部取消，未走完的执行节点不再推进。</summary>
    public ErrorOr<Success> StopTask(TaskId id)
    {
        ErrorOr<WorkTask> found = _registry.Find(id);
        if (found.IsError)
        {
            return found.ErrorsOrEmptyList;
        }

        WorkTask task = found.Value;
        lock (task.Gate)
        {
            _driver.Cancel(task);
        }

        return Result.Success;
    }

    /// <summary>退出时取消全部任务与执行并等待收口。</summary>
    public Task ShutdownAsync() => _driver.ShutdownAsync();
}