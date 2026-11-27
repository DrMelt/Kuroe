using ErrorOr;
using Kuroe.FlowDescription.Definition.Shared.Flow;
using Kuroe.FlowDescription.Definition.Shared.Model;
using Kuroe.FlowDescription.Definition.Shared.Nodes;
using Kuroe.FlowDescription.Definition.Shared.Ports;
using Kuroe.FlowDescription.Semantics.Shared;
using Kuroe.Kernel.Execution.Shared.Ports;
using Kuroe.Kernel.Execution.Shared.Runtime;
using Kuroe.Kernel.Tasks.Shared;
using Kuroe.Maf.Compilation.Shared;
using Kuroe.Maf.Definition;

namespace Kuroe.Maf.Compilation.Projection;

/// <summary>单条流程的 MAF 投影：从语言解析结果建立步骤、输入计划与连接，保留目标结构诊断。</summary>
internal static class MafFlowProjection
{
    private const char Segment = '·';

    /// <summary>把语言解析完成的流程投影为 MAF 流程定义。</summary>
    internal static ErrorOr<MafFlowDefinition> Project(
        FlowSemantics compilation,
        IMafNodeExecutions executions)
    {
        FlowDefinition flow = compilation.Definition;
        List<Error> errors = [];
        List<FlowSemanticNode> executables = [.. compilation.Executables];

        if (executables.Count == 0)
        {
            return [FlowErrors.Body(flow.Name.Value, "流程没有执行节点，无法装配成 MAF 步骤。")];
        }

        Dictionary<NodePath, MafStepId> ids = StepIds(executables, flow.Name, errors);
        Dictionary<NodePath, MafStepPlan> plans = Plans(executables, flow, ids, errors);
        bool requiresModel = executables.Any(executable => executable.Spec.Type?.ModelPort is not null);
        IReadOnlyList<MafStartPort> startPorts = StartPorts(flow, requiresModel, errors);

        List<MafStep> steps = [];
        List<MafEdge> edges = [];
        foreach (FlowSemanticNode executable in executables)
        {
            if (!ids.TryGetValue(executable.Path, out MafStepId id)
                || !plans.TryGetValue(executable.Path, out MafStepPlan? plan)
                || executable.Spec.Type is not { } type)
            {
                continue;
            }

            IExecutionBinder? binder = executions.Execution(type);
            if (binder is null)
            {
                errors.Add(FlowErrors.Body(flow.Name.Value,
                    $"节点 {executable.Path} 的类型 {type.Kind.Value} 没有执行器绑定。"));
                continue;
            }

            IReadOnlyList<Port> outputs = executable.Spec.Outputs;
            NodeEmission emission = executable.Spec.Spec?.Emission ?? NodeEmission.Repeated;
            steps.Add(new MafStep(
                id,
                executable.Spec.Label.Value,
                [.. PortRules.RequiringSubmission(outputs, emission).Select(port => port.Name)],
                new MafStepInvocation(binder),
                new MafStepInputAssembler(plan.Inputs))
            {
                ModelPort = type.ModelPort,
                OutputPlan =
                [
                    .. outputs.Select(port => new MafOutputPort(
                        port.Name,
                        KindOf(port.Type),
                        port.Trigger,
                        PortRules.RequiresSubmission(port, emission))),
                ],
            });

            foreach (MafStepId upstream in plan.Upstreams)
            {
                edges.Add(new MafEdge(upstream, id));
            }
        }

        if (HasCycle(steps, edges))
        {
            errors.Add(FlowErrors.Body(flow.Name.Value, "流程包含环，第一阶段不支持。"));
        }

        if (errors.Count > 0)
        {
            return errors;
        }

        MafFlowDefinition definition = new(flow.Name, startPorts, steps, edges)
        {
            Description = flow.Description,
        };

        if (definition.Validate() is { Count: > 0 } invalid)
        {
            return [.. invalid];
        }

        return definition;
    }

    private static Dictionary<NodePath, MafStepId> StepIds(
        IReadOnlyList<FlowSemanticNode> executables,
        FlowName name,
        List<Error> errors)
    {
        Dictionary<NodePath, MafStepId> ids = [];
        Dictionary<string, NodePath> used = [];
        IEnumerable<(NodePath Path, string Value)> entries = executables.Select(executable =>
            (executable.Path, string.Join(Segment, executable.Path.Levels.Select(level => level.Value))));
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
    private static Dictionary<NodePath, MafStepPlan> Plans(
            IReadOnlyList<FlowSemanticNode> executables,
            FlowDefinition flow,
            IReadOnlyDictionary<NodePath, MafStepId> ids,
            List<Error> errors)
    {
        Dictionary<NodePath, MafStepPlan> plans = [];
        foreach (FlowSemanticNode executable in executables)
        {
            CheckShape(executable, ids, flow.Name, errors);
            List<MafInputPlan> inputs = Inputs(executable, flow, ids, errors);
            MafStepId[] upstreams = [.. inputs
                .Select(input => input.UpstreamStep)
                .OfType<MafStepId>()
                .Distinct()];

            plans[executable.Path] = new MafStepPlan(inputs, upstreams);
        }

        return plans;
    }

    private static void CheckShape(
        FlowSemanticNode executable,
        IReadOnlyDictionary<NodePath, MafStepId> ids,
        FlowName name,
        List<Error> errors)
    {
        string label = Label(executable, ids);
        NodeTypeShape? shape = executable.Spec.Type?.Shape;

        if (shape is { AwaitsExternalInput: true })
        {
            errors.Add(FlowErrors.Body(name.Value, $"步骤 {label} 的节点类型等待外部输入，第一阶段不支持。"));
        }

        if (shape is { StartsUnaided: true })
        {
            errors.Add(FlowErrors.Body(name.Value, $"步骤 {label} 的节点类型不依赖环外来源启动，第一阶段不支持。"));
        }

        if (shape is { DispatchesExecutions: false })
        {
            errors.Add(FlowErrors.Body(name.Value, $"步骤 {label} 的节点类型不派发执行，第一阶段不支持。"));
        }

        if (shape?.Outputs is OutputDeclaration.DerivedFromInputs)
        {
            errors.Add(FlowErrors.Body(name.Value, $"步骤 {label} 的输出从输入派生，第一阶段不支持。"));
        }
    }

    private static List<MafInputPlan> Inputs(
        FlowSemanticNode executable,
        FlowDefinition flow,
        IReadOnlyDictionary<NodePath, MafStepId> ids,
        List<Error> errors)
    {
        List<MafInputPlan> plans = [];
        string label = Label(executable, ids);

        foreach (FlowSemanticInput input in executable.Inputs)
        {
            AddPlan(input, flow, plans, errors, label, ids);
        }

        return plans;
    }

    private static void AddPlan(
            FlowSemanticInput input,
            FlowDefinition flow,
            List<MafInputPlan> plans,
            List<Error> errors,
            string label,
            IReadOnlyDictionary<NodePath, MafStepId> ids)
    {
        InputPort port = input.Port;
        FlowSource source = input.Source;
        switch (source.Kind)
        {
            case FlowSourceKind.StartFixed:
            case FlowSourceKind.NodeConstant:
            case FlowSourceKind.Default:
                plans.Add(MafInputPlan.Constant(port, source.Text ?? string.Empty));
                return;

            case FlowSourceKind.StartInput:
                plans.Add(MafInputPlan.Start(
                    port,
                    source.Port ?? port.Name,
                    input.Group,
                    ConditionOf(input.Condition)));
                return;

            case FlowSourceKind.NodeOutput:
                if (source.Node is { } node
                    && source.Port is { } sourcePort
                    && ids.TryGetValue(node, out MafStepId step))
                {
                    plans.Add(MafInputPlan.Upstream(
                        port,
                        step,
                        sourcePort,
                        input.Group,
                        ConditionOf(input.Condition)));
                    return;
                }

                errors.Add(FlowErrors.Body(flow.Name.Value,
                    $"步骤 {label} 的输入 {port.Name} 来源 {source.Display} 不是执行步骤端口。"));
                return;

            case FlowSourceKind.RuntimeConstant:
                if (source.Node is { } constantNode
                    && source.Port is { } constantPort
                    && ids.TryGetValue(constantNode, out MafStepId constantStep))
                {
                    plans.Add(MafInputPlan.RuntimeConstant(
                        port,
                        constantStep,
                        constantPort,
                        input.Group,
                        ConditionOf(input.Condition)));
                    return;
                }

                errors.Add(FlowErrors.Body(flow.Name.Value,
                    $"步骤 {label} 的输入 {port.Name} 来源 {source.Display} 不是执行步骤端口。"));
                return;

            default:
                errors.Add(FlowErrors.Body(flow.Name.Value,
                    $"步骤 {label} 的输入 {port.Name} 缺少来源终点，编译产物不完整。"));
                return;
        }
    }

    private static List<MafStartPort> StartPorts(
        FlowDefinition flow,
        bool requiresModel,
        List<Error> errors)
    {
        List<MafStartPort> ports = [];
        foreach (Port port in flow.StartPorts)
        {
            if (port.Name == MafFlowDefinition.ModelPort)
            {
                if (!requiresModel)
                {
                    continue;
                }

                if (port.Value is { Length: > 0 })
                {
                    errors.Add(FlowErrors.Body(flow.Name.Value,
                        $"开始端口 {port.Name} 不能是固定值，MAF 由提交给出模型。"));
                    continue;
                }

                ports.Add(new MafStartPort(port.Name, port.Default)
                {
                    Kind = KindOf(port.Type),
                    Mode = ModeOf(port.Mode),
                    RequiresInput = true,
                });
                continue;
            }

            if (port.Mode == PortMode.Constant && port.Value is { Length: > 0 })
            {
                continue;
            }

            ports.Add(new MafStartPort(port.Name, port.Default)
            {
                Kind = KindOf(port.Type),
                Mode = ModeOf(port.Mode),
                RequiresInput = PortRules.CarriesContent(port.Type),
            });
        }

        if (requiresModel && ports.All(port => port.Name != MafFlowDefinition.ModelPort))
        {
            ports.Add(new MafStartPort(MafFlowDefinition.ModelPort));
        }

        return ports;
    }

    private static string Label(FlowSemanticNode executable, IReadOnlyDictionary<NodePath, MafStepId> ids) =>
        ids.TryGetValue(executable.Path, out MafStepId id) ? id.Value : executable.Path.Value;

    private static ExecutionPortKind KindOf(PortType type) => type.Semantics switch
    {
        PortSemantics.Context => ExecutionPortKind.Context,
        PortSemantics.Signal => ExecutionPortKind.Signal,
        _ => ExecutionPortKind.Text,
    };

    private static MafPortMode ModeOf(PortMode mode) => mode switch
    {
        PortMode.Trigger => MafPortMode.Trigger,
        PortMode.Constant => MafPortMode.Constant,
        _ => MafPortMode.Plain,
    };

    private static MafTriggerCondition ConditionOf(TriggerCondition condition) =>
        condition == TriggerCondition.Negative ? MafTriggerCondition.Negative : MafTriggerCondition.Positive;

    private static bool HasCycle(List<MafStep> steps, IReadOnlyList<MafEdge> edges)
    {
        Dictionary<MafStepId, int> incoming = steps.ToDictionary(step => step.Id, _ => 0);
        foreach (MafEdge edge in edges.Where(edge => incoming.ContainsKey(edge.From) && incoming.ContainsKey(edge.To)))
        {
            incoming[edge.To]++;
        }

        Queue<MafStepId> ready = new(incoming.Where(pair => pair.Value == 0).Select(pair => pair.Key));
        int visited = 0;
        while (ready.TryDequeue(out MafStepId current))
        {
            visited++;
            foreach (MafStepId target in edges
                .Where(edge => edge.From == current)
                .Select(edge => edge.To)
                .Where(incoming.ContainsKey))
            {
                incoming[target]--;
                if (incoming[target] == 0)
                {
                    ready.Enqueue(target);
                }
            }
        }

        return visited < steps.Count;
    }
}