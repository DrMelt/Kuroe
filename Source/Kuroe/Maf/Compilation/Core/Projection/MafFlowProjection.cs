using ErrorOr;
using Kuroe.FlowDescription.Definition.Shared.Flow;
using Kuroe.FlowDescription.Definition.Shared.Model;
using Kuroe.FlowDescription.Definition.Shared.Nodes;
using Kuroe.FlowDescription.Definition.Shared.Ports;
using Kuroe.FlowDescription.Semantics.Shared;
using Kuroe.Kernel.Execution.Shared.Runtime;
using Kuroe.Kernel.Tasks.Shared;
using Kuroe.Maf.Compilation.Shared;
using Kuroe.Maf.Definition;

namespace Kuroe.Maf.Compilation.Projection;

/// <summary>单条流程的 MAF 投影：从语言解析结果建立步骤、连接与目标可表达性诊断。</summary>
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
        IReadOnlyList<MafStartPort> startPorts = StartPorts(flow, errors);

        int entries = plans.Values.Count(plan => plan.Upstream is null);
        if (entries > 1)
        {
            errors.Add(FlowErrors.Body(flow.Name.Value, "流程有多个入口步骤，MAF 步骤只支持一个入口。"));
        }

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
            steps.Add(new MafStep(
                id,
                executable.Spec.Label.Value,
                [.. PortRules.RequiringSubmission(outputs, executable.Spec.Spec?.Emission ?? NodeEmission.Repeated)
                    .Select(port => port.Name)],
                new MafStepInvocation(binder),
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
            HashSet<MafStepId> upstream = [.. inputs.Select(input => input.UpstreamStep).OfType<MafStepId>()];

            if (upstream.Count > 1)
            {
                errors.Add(FlowErrors.Body(flow.Name.Value,
                    $"步骤 {Label(executable, ids)} 有多个来源步骤，MAF 步骤只接受一个上游。"));
            }

            plans[executable.Path] = new MafStepPlan(inputs, upstream.Count == 1 ? upstream.First() : null);
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
            errors.Add(FlowErrors.Body(name.Value, $"步骤 {label} 的节点类型等待外部输入，MAF 步骤不支持。"));
        }

        foreach (InputPort input in executable.Spec.EffectiveInputs)
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

        foreach (Port output in executable.Spec.Outputs)
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

    private static List<MafInputPlan> Inputs(
        FlowSemanticNode executable,
        FlowDefinition flow,
        IReadOnlyDictionary<NodePath, MafStepId> ids,
        List<Error> errors)
    {
        List<MafInputPlan> plans = [];
        string label = Label(executable, ids);

        foreach (IGrouping<PortName, FlowSemanticInput> group in executable.Inputs.GroupBy(input => input.Port.Name))
        {
            InputPort port = group.First().Port;

            if (port.Role == InputRole.Control)
            {
                errors.Add(FlowErrors.Body(flow.Name.Value,
                    $"步骤 {label} 的输入 {port.Name} 使用控制角色，MAF 步骤不支持控制输入。"));
                continue;
            }

            if (port.IsStatic)
            {
                AddPlan(port, group.First().Source, flow, plans, errors, label, ids);
                continue;
            }

            List<FlowSemanticInput> linked = [.. group.Where(input => input.IsLinked)];
            if (linked.Count == 0)
            {
                AddPlan(port, group.First().Source, flow, plans, errors, label, ids);
                continue;
            }

            if (linked.Select(input => input.Group).Distinct().Count() > 1)
            {
                errors.Add(FlowErrors.Body(flow.Name.Value,
                    $"步骤 {label} 的输入 {port.Name} 来自多个启动组，MAF 步骤不支持多组启动条件。"));
                continue;
            }

            if (linked.Any(input => input.Condition != TriggerCondition.Positive))
            {
                errors.Add(FlowErrors.Body(flow.Name.Value,
                    $"步骤 {label} 的输入 {port.Name} 使用触发条件，MAF 步骤不支持。"));
                continue;
            }

            if (linked.Count > 1)
            {
                errors.Add(FlowErrors.Body(flow.Name.Value,
                    $"步骤 {label} 的输入 {port.Name} 有多条来源，MAF 步骤不支持。"));
                continue;
            }

            AddPlan(port, linked[0].Source, flow, plans, errors, label, ids);
        }

        return plans;
    }
    private static void AddPlan(
            InputPort port,
            FlowSource source,
            FlowDefinition flow,
            List<MafInputPlan> plans,
            List<Error> errors,
            string label,
            IReadOnlyDictionary<NodePath, MafStepId> ids)
    {
        switch (source.Kind)
        {
            case FlowSourceKind.StartFixed:
            case FlowSourceKind.NodeConstant:
            case FlowSourceKind.Default:
                plans.Add(MafInputPlan.Constant(port, source.Text ?? string.Empty));
                return;

            case FlowSourceKind.StartInput:
                plans.Add(MafInputPlan.Start(port, source.Port ?? port.Name));
                return;

            case FlowSourceKind.NodeOutput:
            case FlowSourceKind.RuntimeConstant:
                if (source.Node is { } node
                    && source.Port is { } sourcePort
                    && ids.TryGetValue(node, out MafStepId step))
                {
                    plans.Add(MafInputPlan.Upstream(port, step, sourcePort));
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

    private static List<MafStartPort> StartPorts(FlowDefinition flow, List<Error> errors)
    {
        List<MafStartPort> ports = [];
        foreach (Port port in flow.StartPorts)
        {
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

    private static string Label(FlowSemanticNode executable, IReadOnlyDictionary<NodePath, MafStepId> ids) =>
        ids.TryGetValue(executable.Path, out MafStepId id) ? id.Value : executable.Path.Value;
}