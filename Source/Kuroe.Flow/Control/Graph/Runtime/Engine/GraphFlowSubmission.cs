using ErrorOr;
using Kuroe.Flow.Control;
using Kuroe.Flow.Execution;
using Kuroe.Flow.Execution.Records;
using Kuroe.Flow.Graph.Records;
using Kuroe.Flow.Control.Graph;
using Kuroe.Flow.Shared.Model;
using Kuroe.Flow.Node.RuntimeGraph.Execution;
using Kuroe.Flow.Node.RuntimeGraph.Ports;
using Kuroe.Flow.Node.RuntimeGraph.Tasks;
using Kuroe.Flow.Node.StaticGraph.Flow;
using Kuroe.Flow.Node.StaticGraph.Graph;
using Kuroe.Flow.Node.StaticGraph.Ports;
using Kuroe.Flow.Tasks.Records;
using ExecutableNode = Kuroe.Flow.Node.StaticGraph.Graph.ExecutableNode;

namespace Kuroe.Flow.Control.Graph.Engine;

/// <summary>图流程的提交与恢复：校验开始输入，建立图状态，从存档重建执行上下文。</summary>
public sealed class GraphFlowSubmission(
    IFlowRuntimeCatalog flows,
    IFlowSnapshotCompiler compiler,
    IHostStartInputs hostStartInputs,
    IHostTaskStart host,
    IExecutionTargetResolver targets,
    IExecutionRuntime executions,
    IFlowSnapshotSource? snapshots = null) : IFlowSubmission
{
    /// <inheritdoc/>
    public ErrorOr<FlowSubmission> Prepare(TaskId id, FlowName name, StartInputs inputs, string? title)
    {
        ErrorOr<CompiledFlow> compiled = flows.FindCompiled(name);
        if (compiled.IsError)
        {
            return compiled.ErrorsOrEmptyList;
        }

        return PrepareCompiled(id, compiled.Value, inputs, title);
    }

    /// <summary>按已编译流程准备提交。</summary>
    public ErrorOr<FlowSubmission> PrepareCompiled(
        TaskId id,
        CompiledFlow flow,
        StartInputs inputs,
        string? title)
    {
        NodeGraph graph = flow.Graph;

        // 宿主输入表在提交时解析，流程编译不涉及这份配置
        ErrorOr<IReadOnlyDictionary<PortName, string>> resolved = hostStartInputs.Resolve(flow.Model);
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

        GraphFlowDefinitionRecord? definition = null;
        if (snapshots is { } snapshotSource)
        {
            ErrorOr<GraphFlowDefinitionRecord> captured = snapshotSource.Capture(flow.Model.Name);
            if (captured.IsError)
            {
                return captured.ErrorsOrEmptyList;
            }

            definition = captured.Value;
        }

        ErrorOr<IPortTexts> start = host.Prepare(id, hostInputs);
        if (start.IsError)
        {
            return start.ErrorsOrEmptyList;
        }

        if (CheckStartTexts(hostInputs, start.Value) is { Count: > 0 } startErrors)
        {
            return startErrors;
        }

        StartPortValues startPorts = new(graph.StartPorts, start.Value, effectiveInputs);

        // 等待型节点不进入派发路径；派发节点在静态值全部就绪后再要求执行目标可解析
        StaticPortValues staticPorts = new(graph, startPorts);
        foreach (InputBinding binding in graph.StartOutgoing)
        {
            if (graph[binding.Target] is not ExecutableNode target
                || graph.ShapeOf(target.Path) is { DispatchesExecutions: false }
                || !staticPorts.FullyResolved(target))
            {
                continue;
            }

            ErrorOr<INodeDeclaration> declaration = targets.For(target, staticPorts.For(target));
            if (declaration.IsError)
            {
                return declaration.ErrorsOrEmptyList;
            }
        }

        GraphFlowState state = new(flow, effectiveInputs, start.Value, startPorts, definition, executions);

        return new FlowSubmission(
            state,
            taskTitle,
            new FlowTexts(start.Value.Texts),
            definition);
    }
/// <inheritdoc/>
    public ErrorOr<FlowRestore> Restore(TaskRecord record)
    {
        if (record.Flow.Definition is not GraphFlowDefinitionRecord definition
            || record.Flow.State is not GraphFlowStateRecord saved)
        {
            return [Error.Failure(FlowErrorCodes.RecordFormat, "存档的流程记录不是图流程。")];
        }

        ErrorOr<CompiledFlow> compiled = compiler.Compile(definition);
        if (compiled.IsError)
        {
            return compiled.ErrorsOrEmptyList;
        }

        CompiledFlow flow = compiled.Value;
        PortTexts startTexts = new(saved.Start.StartTexts);
        StartPortValues startPorts = new(flow.Graph.StartPorts, startTexts, saved.Start.Inputs);
        GraphFlowState state = new(flow, saved.Start.Inputs, startTexts, startPorts, definition, executions)
        {
            RestoreNodes = saved.Nodes,
            RestorePorts = saved.Ports,
            Seed = saved.Pending,
            SavedAt = record.SavedAt,
        };

        List<IExecution> units = [];
        try
        {
            foreach (ExecutionRecord executionRecord in record.Executions)
            {
                NodePath path = GraphRecordTargets.Parse(executionRecord.Target);
                if (flow.Graph[path] is not ExecutableNode node)
                {
                    return [Error.Failure(
                        FlowErrorCodes.RecordGraphMismatch,
                        $"存档执行节点 {path} 不在流程图中。")];
                }

                GraphExecutionInput input = BuildInput(record.Identity.Id, node, executionRecord);
                GraphExecution execution = GraphExecution.Restore(executionRecord, input, executions);
                state.Bind(execution.Unit.Id, input, execution.ControlVersion);
                units.Add(execution.Unit);
            }
        }
        catch (Exception ex) when (ex is ArgumentException or FormatException)
        {
            return [Error.Failure(FlowErrorCodes.RecordNodeMismatch, ex.Message)];
        }

        return new FlowRestore(
            state,
            new FlowTexts(saved.Start.StartTexts),
            units,
            saved.Pending);
    }

    private GraphExecutionInput BuildInput(TaskId task, ExecutableNode node, ExecutionRecord saved)
    {
        GraphExecutionInput input = new()
        {
            Task = task,
            Node = node.Path,
            Type = node.Type,
            ExecutionCount = saved.Attempt,
            MaxAttempts = node.MaxExecutions,
            Inputs =
            [
                .. saved.Inputs.Select(value => new InputValue(
                    new PortName(value.Port.Value),
                    new ContextValue(GraphRecordSources.Read(value.Source), value.Text))),
            ],
            Texts = saved.Texts.ToDictionary(pair => new PortName(pair.Key.Value), pair => pair.Value),
            OutputPorts = node.SubmittablePorts,
        };

        if (targets.For(node, input) is { IsError: false } declaration)
        {
            input = input with { Declaration = declaration.Value };
        }

        return input;
    }
/// <summary>开始输入必须覆盖承载内容、不带固定值、不带默认值且未绑定宿主输入的开始端口，且只落在用户端口上。</summary>
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
            .. required.Where(port => inputs.Text(port) is null).Select(MissingInput),
            .. ports
                .Where(port => StartInputRules.AcceptsInput(port, hostInputs.ContainsKey(port.Name))
                    && inputs.Text(port.Name) is { } text
                    && string.IsNullOrWhiteSpace(text))
                .Select(port => BlankInput(port.Name)),
            .. inputs.Values
                .GroupBy(input => input.Port)
                .Where(group => group.Count() > 1)
                .Select(group => DuplicateInput(group.Key)),
            .. supplied
                .Where(input => ports.Find(input.Port) is null)
                .Select(input => UnknownInput(input.Port)),
            .. supplied
                .Where(input => KindOf(input.Port) == StartInputKind.Host)
                .Select(input => HostInput(input.Port)),
            .. supplied
                .Where(input => KindOf(input.Port) == StartInputKind.Fixed)
                .Select(input => ConstantInput(input.Port)),
            .. supplied
                .Where(input => KindOf(input.Port) == StartInputKind.Automatic)
                .Select(input => NonContentInput(input.Port)),
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
                errors.Add(MissingHostValue(port));
            }
            else if (string.IsNullOrWhiteSpace(text))
            {
                errors.Add(BlankHostValue(port));
            }
        }

        return errors;
    }
private static Error MissingInput(PortName port) =>
        Error.Validation(FlowErrorCodes.TaskInput, $"开始端口 {port} 没有输入。");

    private static Error BlankInput(PortName port) =>
        Error.Validation(FlowErrorCodes.TaskInput, $"开始端口 {port} 的输入不能为空。");

    private static Error UnknownInput(PortName port) =>
        Error.Validation(FlowErrorCodes.TaskInput, $"流程没有开始端口 {port}。");

    private static Error ConstantInput(PortName port) =>
        Error.Validation(FlowErrorCodes.TaskInput, $"开始端口 {port} 带固定值，不接受提交输入。");

    private static Error HostInput(PortName port) =>
        Error.Validation(FlowErrorCodes.TaskInput, $"开始端口 {port} 由宿主提供，不接受提交输入。");

    private static Error NonContentInput(PortName port) =>
        Error.Validation(FlowErrorCodes.TaskInput, $"开始端口 {port} 不承载内容，不接受提交输入。");

    private static Error MissingHostValue(PortName port) =>
        Error.Validation(FlowErrorCodes.TaskInput, $"开始端口 {port} 的宿主输入没有给出值。");

    private static Error BlankHostValue(PortName port) =>
        Error.Validation(FlowErrorCodes.TaskInput, $"开始端口 {port} 的宿主输入不能为空。");

    private static Error DuplicateInput(PortName port) =>
        Error.Validation(FlowErrorCodes.TaskInput, $"开始端口 {port} 的输入重复。");
}