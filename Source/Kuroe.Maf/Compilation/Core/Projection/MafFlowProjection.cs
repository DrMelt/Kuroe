using ErrorOr;
using Kuroe.Execution.Shared.Runtime;
using Kuroe.Flow.Node.Shared.Model;
using Kuroe.Flow.Node.Shared.StaticGraph.Flow;
using Kuroe.Flow.Node.Shared.StaticGraph.Graph;
using Kuroe.Flow.Node.Shared.StaticGraph.Ports;
using Kuroe.Maf.Definition;
using Kuroe.Tasks.Shared;

namespace Kuroe.Maf.Compilation.Projection;

/// <summary>单条流程的 MAF 投影：开始端口、步骤、连接与装配诊断。</summary>
internal static class MafFlowProjection
{
    /// <summary>步骤标识的段分隔符，路径段按它连接成扁平标识。</summary>
    private const char Segment = '·';

    /// <summary>把编译完成的流程投影为 MAF 流程定义。</summary>
    internal static ErrorOr<MafFlowDefinition> Project(FlowGraph flow)
    {
        List<Error> errors = [];
        NodeGraph graph = flow.Graph;
        FlowName name = flow.Definition.Name;
        List<ExecutableNode> executables = [.. graph.ExecutableNodes];

        if (executables.Count == 0)
        {
            return [FlowErrors.Body(name.Value, "流程没有执行节点，无法装配成 MAF 步骤。")];
        }

        Dictionary<NodePath, MafStepId> ids = StepIds(executables, name, errors);
        Dictionary<NodePath, MafStepPlan> plans = Plans(graph, executables, ids, name, errors);
        IReadOnlyList<MafStartPort> startPorts = StartPorts(flow.Definition, errors);

        List<MafStep> steps = [];
        List<MafEdge> edges = [];
        foreach (ExecutableNode executable in executables)
        {
            if (!ids.TryGetValue(executable.Path, out MafStepId id)
                || !plans.TryGetValue(executable.Path, out MafStepPlan? plan))
            {
                continue;
            }

            steps.Add(new MafStep(
                id,
                executable.Label.Value,
                [.. executable.RequiredPorts.Select(port => port.Name)],
                new MafStepInvocation(executable),
                new MafStepInputAssembler(plan.Inputs)));

            if (plan.Upstream is { } upstream)
            {
                edges.Add(new MafEdge(upstream, id));
            }
        }

        if (errors.Count > 0)
        {
            return errors;
        }

        MafFlowDefinition definition = new(name, startPorts, steps, edges)
        {
            Description = flow.Definition.Description,
        };

        if (definition.Validate() is { Count: > 0 } invalid)
        {
            return [.. invalid];
        }

        return definition;
    }

    /// <summary>节点路径到步骤标识的映射：段按分隔符连接，重复时给出错误。</summary>
    private static Dictionary<NodePath, MafStepId> StepIds(
        IReadOnlyList<ExecutableNode> executables,
        FlowName name,
        List<Error> errors)
    {
        Dictionary<NodePath, MafStepId> ids = [];
        Dictionary<string, NodePath> used = [];
        IEnumerable<(NodePath Path, string Value)> entries = executables
            .Select(executable => (executable.Path, IdOf(executable.Path)));
        foreach ((NodePath path, string value) in entries)
        {
            if (!used.TryAdd(value, path))
            {
                errors.Add(FlowErrors.Body(name.Value,
                    $"节点 {path} 与 {used[value]} 的步骤标识 {value} 重复。"));
                continue;
            }

            ids[path] = new MafStepId(value);
        }

        return ids;
    }

    /// <summary>节点路径的扁平步骤标识。</summary>
    private static string IdOf(NodePath path) =>
        string.Join(Segment, path.Levels.Select(level => level.Value));

    /// <summary>逐执行节点建立输入计划与上游步骤，诊断不能表达为 MAF 步骤的结构。</summary>
    private static Dictionary<NodePath, MafStepPlan> Plans(
        NodeGraph graph,
        IReadOnlyList<ExecutableNode> executables,
        IReadOnlyDictionary<NodePath, MafStepId> ids,
        FlowName name,
        List<Error> errors)
    {
        Dictionary<NodePath, MafStepPlan> plans = [];
        foreach (ExecutableNode executable in executables)
        {
            CheckShape(executable, ids, name, errors);
            List<MafInputPlan> inputs = Inputs(graph, executable, ids, name, errors);
            HashSet<MafStepId> upstream =
                [.. inputs.Select(input => input.UpstreamStep).OfType<MafStepId>()];

            if (upstream.Count > 1)
            {
                errors.Add(FlowErrors.Body(name.Value,
                    $"步骤 {Label(executable, ids)} 有多个来源步骤，MAF 步骤只接受一个上游。"));
            }

            plans[executable.Path] = new MafStepPlan(
                inputs,
                upstream.Count == 1 ? upstream.First() : null);
        }

        return plans;
    }

    /// <summary>执行节点的端口与形状必须能由 MAF 步骤表达。</summary>
    private static void CheckShape(
        ExecutableNode executable,
        IReadOnlyDictionary<NodePath, MafStepId> ids,
        FlowName name,
        List<Error> errors)
    {
        string label = Label(executable, ids);

        if (executable.Type.Shape.AwaitsExternalInput)
        {
            errors.Add(FlowErrors.Body(name.Value,
                $"步骤 {label} 的节点类型等待外部输入，MAF 步骤不支持。"));
        }

        if (executable.Type is not IExecutionRunnerFactoryProvider)
        {
            errors.Add(FlowErrors.Body(name.Value,
                $"步骤 {label} 的节点类型没有执行运行器工厂，不能作为 MAF 步骤。"));
        }

        foreach (InputPort input in executable.Inputs)
        {
            if (input.Type != PortTypes.Text)
            {
                errors.Add(FlowErrors.Body(name.Value,
                    $"步骤 {label} 的输入端口 {input.Name} 类型 {input.Type} 不支持，MAF 只传递文本。"));
            }

            if (input.Trigger)
            {
                errors.Add(FlowErrors.Body(name.Value,
                    $"步骤 {label} 的输入端口 {input.Name} 使用触发，MAF 步骤不支持触发门控。"));
            }
        }

        foreach (Port output in executable.Outputs)
        {
            if (output.Type != PortTypes.Text)
            {
                errors.Add(FlowErrors.Body(name.Value,
                    $"步骤 {label} 的输出端口 {output.Name} 类型 {output.Type} 不支持，MAF 只传递文本。"));
            }

            if (output.Trigger)
            {
                errors.Add(FlowErrors.Body(name.Value,
                    $"步骤 {label} 的输出端口 {output.Name} 使用触发，MAF 步骤不支持触发位。"));
            }
        }
    }

    /// <summary>建立执行节点的输入计划：动态接线与静态绑定都落成步骤输入。</summary>
    private static List<MafInputPlan> Inputs(
        NodeGraph graph,
        ExecutableNode executable,
        IReadOnlyDictionary<NodePath, MafStepId> ids,
        FlowName name,
        List<Error> errors)
    {
        List<MafInputPlan> plans = [];
        IReadOnlyList<InputBinding> incoming = graph.Incoming(executable.Path);
        if (incoming.Select(binding => binding.Group).Distinct().Count() > 1)
        {
            errors.Add(FlowErrors.Body(name.Value,
                $"步骤 {Label(executable, ids)} 有多个 From 启动组，MAF 步骤只有一个输入来源。"));
        }

        foreach (InputPort port in executable.Inputs)
        {
            if (port.Role == InputRole.Control)
            {
                continue;
            }

            if (port.IsStatic)
            {
                StaticInput(graph, executable, port, ids, name, errors, plans);
                continue;
            }

            DynamicInput(graph, executable, port, incoming, ids, name, errors, plans);
        }

        return plans;
    }

    /// <summary>静态输入按绑定链解析成常量、开始输入或上游端口。</summary>
    private static void StaticInput(
        NodeGraph graph,
        ExecutableNode executable,
        InputPort port,
        IReadOnlyDictionary<NodePath, MafStepId> ids,
        FlowName name,
        List<Error> errors,
        List<MafInputPlan> plans)
    {
        if (graph.StaticInputResolution(executable.Path, port.Name) is not { } resolved)
        {
            errors.Add(FlowErrors.Body(name.Value,
                $"步骤 {Label(executable, ids)} 的静态输入 {port.Name} 没有解析结果。"));
            return;
        }

        switch (resolved.Kind)
        {
            case StaticSourceKind.Literal:
            case StaticSourceKind.Default:
            case StaticSourceKind.StartFixed:
                plans.Add(MafInputPlan.Constant(port, resolved.Text ?? string.Empty));
                return;

            case StaticSourceKind.StartInjected:
                plans.Add(MafInputPlan.Start(port, resolved.Port));
                return;

            case StaticSourceKind.RuntimeConstant:
                if (Upstream(resolved, ids) is { } upstream)
                {
                    plans.Add(MafInputPlan.Upstream(port, upstream.Step, upstream.Port));
                }
                else
                {
                    errors.Add(FlowErrors.Body(name.Value,
                        $"步骤 {Label(executable, ids)} 的静态输入 {port.Name} 来源 {resolved.Source.Display} 不是执行步骤端口。"));
                }

                return;

            default:
                errors.Add(FlowErrors.Body(name.Value,
                    $"步骤 {Label(executable, ids)} 的静态输入 {port.Name} 无法解析：{resolved.Error}"));
                return;
        }
    }

    /// <summary>动态输入按接线解析成开始输入或上游端口，没有来源时取默认值。</summary>
    private static void DynamicInput(
        NodeGraph graph,
        ExecutableNode executable,
        InputPort port,
        IReadOnlyList<InputBinding> incoming,
        IReadOnlyDictionary<NodePath, MafStepId> ids,
        FlowName name,
        List<Error> errors,
        List<MafInputPlan> plans)
    {
        string label = Label(executable, ids);
        List<InputBinding> matches = [.. incoming.Where(binding => binding.TargetInput == port.Name)];
        if (matches.Count == 0)
        {
            if (port.Default is { Length: > 0 } text)
            {
                plans.Add(MafInputPlan.Constant(port, text));
                return;
            }

            errors.Add(FlowErrors.Body(name.Value, $"步骤 {label} 的输入 {port.Name} 没有来源。"));
            return;
        }

        if (matches.Count > 1)
        {
            errors.Add(FlowErrors.Body(name.Value,
                $"步骤 {label} 的输入 {port.Name} 有多个连接，MAF 步骤只接受一个来源。"));
            return;
        }

        InputBinding binding = matches[0];
        if (binding.Condition != TriggerCondition.Positive)
        {
            errors.Add(FlowErrors.Body(name.Value,
                $"步骤 {label} 的输入 {port.Name} 使用触发条件，MAF 步骤不支持门控。"));
        }

        ErrorOr<MafSource> resolved = MafSourceResolution.Resolve(graph, binding.Source);
        if (resolved.IsError)
        {
            errors.AddRange(resolved.ErrorsOrEmptyList);
            return;
        }

        MafSource source = resolved.Value;
        if (source.IsStart)
        {
            if (source.Port is { } startPort)
            {
                plans.Add(MafInputPlan.Start(port, startPort));
            }

            return;
        }

        if (source.Node is { } node
            && ids.TryGetValue(node, out MafStepId step)
            && source.Port is { } sourcePort)
        {
            plans.Add(MafInputPlan.Upstream(port, step, sourcePort));
            return;
        }

        errors.Add(FlowErrors.Body(name.Value,
            $"步骤 {label} 的输入 {port.Name} 来源 {binding.Source.Display} 不是执行步骤端口。"));
    }

    /// <summary>把运行期常量的来源节点解析成上游步骤端口，来源不是执行步骤时为空。</summary>
    private static (MafStepId Step, PortName Port)? Upstream(
        StaticResolution resolution,
        IReadOnlyDictionary<NodePath, MafStepId> ids) =>
        resolution.Source.Node is { } node && ids.TryGetValue(node, out MafStepId step)
            ? (step, resolution.Port)
            : null;

    /// <summary>开始端口：文本端口进入提交输入，固定值只作为装配期常量。</summary>
    private static List<MafStartPort> StartPorts(FlowDefinition flow, List<Error> errors)
    {
        List<MafStartPort> ports = [];
        HashSet<PortName> names = [];
        foreach (Port port in flow.StartPorts)
        {
            if (!names.Add(port.Name))
            {
                errors.Add(FlowErrors.Body(flow.Name.Value, $"开始端口 {port.Name} 重复。"));
                continue;
            }

            if (port.Type != PortTypes.Text)
            {
                errors.Add(FlowErrors.Body(flow.Name.Value,
                    $"开始端口 {port.Name} 类型 {port.Type} 不支持，MAF 只传递文本。"));
                continue;
            }

            if (port.Trigger)
            {
                errors.Add(FlowErrors.Body(flow.Name.Value,
                    $"开始端口 {port.Name} 使用触发，MAF 步骤不支持触发门控。"));
                continue;
            }

            if (port.Name == MafFlowDefinition.ModelPort)
            {
                if (port.Value is { Length: > 0 })
                {
                    errors.Add(FlowErrors.Body(flow.Name.Value,
                        $"开始端口 {port.Name} 不能是固定值，MAF 由提交给出模型。"));
                    continue;
                }

                ports.Add(new MafStartPort(port.Name, port.Default));
                continue;
            }

            if (port.Mode == PortMode.Constant && port.Value is { Length: > 0 })
            {
                continue;
            }

            ports.Add(new MafStartPort(port.Name, port.Default));
        }

        if (ports.All(port => port.Name != MafFlowDefinition.ModelPort))
        {
            ports.Add(new MafStartPort(MafFlowDefinition.ModelPort));
        }

        return ports;
    }

    /// <summary>步骤诊断使用的标识：已生成时取步骤标识，否则取节点路径。</summary>
    private static string Label(ExecutableNode executable, IReadOnlyDictionary<NodePath, MafStepId> ids) =>
        ids.TryGetValue(executable.Path, out MafStepId id) ? id.Value : executable.Path.Value;
}