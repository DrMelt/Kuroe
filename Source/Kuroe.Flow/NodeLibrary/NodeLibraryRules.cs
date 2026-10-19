using ErrorOr;
using Kuroe.Flow.Shared;
using Kuroe.Flow.Shared.StaticGraph;
using Kuroe.Flow.StaticGraph;
namespace Kuroe.Flow.NodeLibrary;

using Flow = Shared.StaticGraph;

/// <summary>节点库定义的校验：结构合法、端口声明、子树自包含。展开在 NodeExpander 里进行。</summary>
internal static class NodeLibraryRules
{
    /// <summary>校验节点库：定义结构与子树自包含。各流程随后按节点库展开。同名覆盖与重复节点名报告在注册处完成，
    /// 这里看到的是去重后的定义表。类型的形状由节点持有的类型描述给出，判定分派到哪一支。</summary>
    public static ErrorOr<Success> Validate(NodeLibrary library)
    {
        List<Error> errors = [];
        IReadOnlyList<NodeSpec> definitions = library.All();
        var byName = ValidateDefinitions(definitions, errors);

        foreach (NodeSpec node in definitions)
        {
            if (ShapeOf(node) is { Organizes: true })
            {
                if (node.Nodes is { Count: > 0 } members)
                {
                    CheckContainer(node, members, byName, errors, checkSlots: true);
                }
            }
            else
            {
                CheckLeaf(node, errors);
            }
        }

        return errors.Count > 0 ? errors : Result.Success;
    }

    /// <summary>节点持有的类型形状，引用节点为空。</summary>
    private static NodeTypeShape? ShapeOf(NodeSpec node) => node.Type?.Shape;

    /// <summary>节点类型名，未声明类型的节点为空。</summary>
    private static string KindName(NodeSpec node) =>
        node.Kind is { } kind ? kind.Value : string.Empty;

    private static Dictionary<NodeName, NodeSpec> ValidateDefinitions(
        IReadOnlyList<NodeSpec> definitions,
        List<Error> errors)
    {
        var byName = new Dictionary<NodeName, NodeSpec>();
        foreach (NodeSpec node in definitions)
        {
            if (node.Use is not null)
            {
                errors.Add(NodeScope.Library.Node(node.Label.Value, "节点库定义不能是引用。"));
                continue;
            }

            if (node.Name is not { } name || string.IsNullOrWhiteSpace(name.Value))
            {
                errors.Add(NodeScope.Library.Node("(未命名)", "节点名不能为空。"));
                continue;
            }

            if (name.Value.Contains('@'))
            {
                errors.Add(NodeScope.Library.Node(name.Value, "节点名不能含 @，@ 是端口引用的分隔符。"));
                continue;
            }

            if (name == StartNode.ReservedName)
            {
                errors.Add(NodeScope.Library.Node(name.Value, $"节点名 {StartNode.ReservedName} 是流程起点的保留名。"));
                continue;
            }

            byName[name] = node;

            if (ShapeOf(node) is { Organizes: true })
            {
                if (node.Nodes is not { Count: > 0 })
                {
                    errors.Add(NodeScope.Library.Node(node.Label.Value, "容器节点必须声明子节点。"));
                }

                if (node.Spec is not null)
                {
                    errors.Add(NodeScope.Library.Node(node.Label.Value, "容器不能同时声明执行配置。"));
                }

                if (node.From.Count > 0)
                {
                    errors.Add(NodeScope.Library.Node(node.Label.Value, "库容器定义不接线，不能声明 From。"));
                }

                if (node.In is not null)
                {
                    errors.Add(NodeScope.Library.Node(node.Label.Value, "库容器定义不接线，不能声明输入端口绑定。"));
                }

                if (node.Model is not null || node.Models is not null)
                {
                    errors.Add(NodeScope.Library.Node(node.Label.Value, "库容器定义不声明模型选择与模型绑定，绑定由引用处提供。"));
                }

                ValidateModelSlots(node, errors);

                if (node.Outputs.Count > 0)
                {
                    errors.Add(NodeScope.Library.Node(node.Label.Value, "库容器定义不声明执行节点输出端口，容器输出端口条目写 Name 与 Ref。"));
                }

                if (node.Validate is not null)
                {
                    errors.Add(NodeScope.Library.Node(node.Label.Value, "库容器定义不声明 Validate，输出校验只属于执行节点。"));
                }
            }
            else
            {
                if (node.Nodes is { Count: > 0 })
                {
                    errors.Add(NodeScope.Library.Node(node.Label.Value, "执行节点与输入节点不能声明子节点，子节点只属于容器。"));
                }

                if (node.ModelSlots is not null)
                {
                    errors.Add(NodeScope.Library.Node(node.Label.Value, "执行节点不声明模型槽位，槽位声明只属于容器。"));
                }

                if (node.Spec is null)
                {
                    errors.Add(NodeScope.Library.Node(node.Label.Value, "节点必须声明执行配置。"));
                }

                if (ShapeOf(node) is { DispatchesExecutions: false })
                {
                    if (node.Model is not null)
                    {
                        errors.Add(NodeScope.Library.Node(node.Label.Value,
                            $"{KindName(node)}成员不启动执行，不声明模型槽位。"));
                    }
                }
                else
                {
                    if (node.Model is not null)
                    {
                        errors.Add(NodeScope.Library.Node(node.Label.Value, "库执行定义不声明模型，模型由使用处输入。"));
                    }

                    if (node.Models is not null)
                    {
                        errors.Add(NodeScope.Library.Node(node.Label.Value, "库执行定义不写模型绑定，绑定只属于引用节点组。"));
                    }
                }
            }
        }

        return byName;
    }

    /// <summary>模型槽位声明的形状：名字非空且不重复。</summary>
    private static void ValidateModelSlots(NodeSpec node, List<Error> errors)
    {
        if (node.ModelSlots is not { } slots)
        {
            return;
        }

        foreach (ModelRef slot in slots.Where(slot => string.IsNullOrWhiteSpace(slot.Value)))
        {
            errors.Add(NodeScope.Library.Node(node.Label.Value, "模型槽位名不能为空。"));
        }

        foreach (ModelRef duplicated in slots.GroupBy(slot => slot).Where(group => group.Count() > 1).Select(group => group.Key))
        {
            errors.Add(NodeScope.Library.Node(node.Label.Value, $"模型槽位 {duplicated} 重复。"));
        }
    }

    /// <summary>执行节点库叶子：不能再带结构与接线，输入端口与输出端口在此声明。</summary>
    private static void CheckLeaf(NodeSpec node, List<Error> errors)
    {
        if (node.From.Count > 0)
        {
            errors.Add(NodeScope.Library.Node(node.Label.Value, "执行节点库定义的接线由引用处提供，不能写 From。"));
        }

        if (node.In is not null)
        {
            errors.Add(NodeScope.Library.Node(node.Label.Value, "执行节点库定义不绑定输入端口，接线从引用处提供。"));
        }

        if (node.ContainerOutputs is { Count: > 0 })
        {
            errors.Add(NodeScope.Library.Node(node.Label.Value, "库执行定义不声明容器输出端口，执行节点输出端口条目写 Name 与 Type。"));
        }

        if (node.Spec?.MaxExecutions is { } limit && limit < 1)
        {
            errors.Add(NodeScope.Library.Node(node.Label.Value, "MaxExecutions 必须是正整数。"));
        }

        ValidateInterface(node, errors);
    }

    /// <summary>节点接口的库规则：端口名与端口类型与流程校验共用一份，非执行节点的输出与指令另有库层限制。</summary>
    private static void ValidateInterface(NodeSpec node, List<Error> errors)
    {
        NodeInterfaceRules.Check(node, ShapeOf(node), message => errors.Add(NodeScope.Library.Node(node.Label.Value, message)));

        if ((node.Spec is null || ShapeOf(node) is { DispatchesExecutions: false }) && node.Outputs.Count > 1)
        {
            errors.Add(NodeScope.Library.Node(node.Label.Value, "非执行节点最多一个输出端口。"));
        }
    }

    /// <summary>校验容器子树：整棵子树标签扁平唯一，成员引用存在且结构合法，From 全部落在子树或声明端口内。
    /// checkSlots 只在节点库容器定义层为真，槽位声明与使用一致按定义整棵子树核对一次，内联子容器沿外层声明。</summary>
    private static void CheckContainer(
        NodeSpec container,
        IReadOnlyList<NodeSpec> members,
        Dictionary<NodeName, NodeSpec> byName,
        List<Error> errors,
        bool checkSlots)
    {
        if (container.MaxExecutions is { } limit && limit < 1)
        {
            errors.Add(NodeScope.Library.Node(container.Label.Value, "MaxExecutions 必须是正整数。"));
        }

        var subtreeLabels = new HashSet<NodeLabel>();
        CollectSubtreeLabels(members, byName, subtreeLabels);

        var subtreeOutputs = new Dictionary<NodeLabel, IReadOnlyList<Port>>();
        CollectSubtreeOutputs(members, byName, subtreeOutputs);

        var contextless = new HashSet<NodeLabel>();
        CollectContextless(members, byName, contextless);

        if (container.ContainerOutputs is { } outs)
        {
            CheckContainerOutputs(container, outs, subtreeLabels, subtreeOutputs, contextless, errors);
        }

        var literalLabels = new HashSet<NodeLabel>();
        CheckLiteralLabels(members, literalLabels, container, errors);

        var ports = new HashSet<PortName>([.. container.Inputs.Select(port => port.Name)]);
        if (checkSlots)
        {
            CheckModelSlots(container, members, container.ModelSlots ?? [], byName, errors);
        }

        foreach (NodeSpec member in members)
        {
            CheckMember(member, ports, subtreeLabels, byName, errors);
            CheckMemberModel(member, byName, errors);
            ValidateInterface(member, errors);
            if (member.Nodes is { Count: > 0 })
            {
                CheckContainer(member, member.Nodes, byName, errors, checkSlots: false);
            }
        }
    }

    /// <summary>容器槽位声明与使用一致：成员引用的槽位必须已声明，声明的槽位必须被引用，
    /// 引用子容器的绑定键覆盖被引用定义的声明。</summary>
    private static void CheckModelSlots(
        NodeSpec container,
        IReadOnlyList<NodeSpec> members,
        IReadOnlyList<ModelRef> declared,
        Dictionary<NodeName, NodeSpec> byName,
        List<Error> errors)
    {
        var used = new HashSet<ModelRef>();
        CollectSlotUsage(members, byName, used);

        foreach (ModelRef slot in used.Where(slot => !declared.Contains(slot)))
        {
            errors.Add(NodeScope.Library.Node(container.Label.Value, $"成员使用的模型槽位 {slot} 未在容器上声明。"));
        }

        foreach (ModelRef slot in declared.Where(slot => !used.Contains(slot)))
        {
            errors.Add(NodeScope.Library.Node(container.Label.Value, $"声明的模型槽位 {slot} 未被成员使用。"));
        }

        foreach (NodeSpec member in members)
        {
            if (member.Use is { } use && byName.TryGetValue(use, out NodeSpec? referenced) && referenced.Nodes is { Count: > 0 })
            {
                CheckGroupBinding(member, referenced, errors);
            }
        }
    }

    /// <summary>收集子树引用的模型槽位：执行成员写自己的槽位，内联子容器递归，引用子容器取绑定值。
    /// 被引用定义内部的槽位属于它自己的声明，不在此收集。</summary>
    private static void CollectSlotUsage(
        IReadOnlyList<NodeSpec> nodes,
        Dictionary<NodeName, NodeSpec> byName,
        HashSet<ModelRef> used)
    {
        foreach (NodeSpec node in nodes)
        {
            if (node.Nodes is { Count: > 0 })
            {
                CollectSlotUsage(node.Nodes, byName, used);
                continue;
            }

            if (node.Use is { } use && byName.TryGetValue(use, out NodeSpec? referenced) && referenced.Nodes is { Count: > 0 })
            {
                foreach (ModelRef bound in node.Models?.Values ?? [])
                {
                    used.Add(bound);
                }

                continue;
            }

            if (node.Model is { } slot)
            {
                used.Add(slot);
            }
        }
    }

    /// <summary>引用子容器的模型绑定：键必须覆盖被引用定义声明的槽位，不能绑定声明以外的槽位。</summary>
    private static void CheckGroupBinding(
        NodeSpec member,
        NodeSpec definition,
        List<Error> errors)
    {
        IReadOnlyList<ModelRef> slots = definition.ModelSlots ?? [];
        var bound = new HashSet<ModelRef>(member.Models?.Keys ?? []);
        foreach (ModelRef slot in slots.Where(slot => !bound.Contains(slot)))
        {
            errors.Add(NodeScope.Library.Node(member.Label.Value, $"引用节点组未绑定模型槽位 {slot}。"));
        }

        foreach (ModelRef slot in bound.Where(slot => !slots.Contains(slot)))
        {
            errors.Add(NodeScope.Library.Node(member.Label.Value, $"模型槽位 {slot} 不在容器 {definition.Label} 上。"));
        }
    }

    /// <summary>收集子树内全部执行节点的输出端口表，供容器输出端口的绑定目标校验。被引用执行节点取定义上的端口表。</summary>
    private static void CollectSubtreeOutputs(
        IReadOnlyList<NodeSpec> nodes,
        Dictionary<NodeName, NodeSpec> byName,
        Dictionary<NodeLabel, IReadOnlyList<Port>> outputs)
    {
        foreach (NodeSpec node in nodes)
        {
            if (node.Spec is not null)
            {
                outputs[node.Label] = node.Outputs;
            }
            else if (node.Use is { } use && byName.TryGetValue(use, out NodeSpec? definition) && definition.Spec is not null)
            {
                outputs[node.Label] = definition.Outputs;
            }

            if (node.Nodes is { Count: > 0 })
            {
                CollectSubtreeOutputs(node.Nodes, byName, outputs);
            }
        }
    }

    /// <summary>收集子树内不产出上下文端口的节点标签，上下文端口绑定不允许落在这些节点上。
    /// 判断按类型的输出端口值域，组织子节点的类型不在此列。</summary>
    private static void CollectContextless(
        IReadOnlyList<NodeSpec> nodes,
        Dictionary<NodeName, NodeSpec> byName,
        HashSet<NodeLabel> contextless)
    {
        foreach (NodeSpec node in nodes)
        {
            NodeSpec declared = node.Use is { } use && byName.TryGetValue(use, out NodeSpec? definition) ? definition : node;
            if (ShapeOf(declared) is { Organizes: false } shape
                && !shape.OutputTypes.Contains(Flow.PortType.Context))
            {
                contextless.Add(node.Label);
            }

            if (node.Nodes is { Count: > 0 })
            {
                CollectContextless(node.Nodes, byName, contextless);
            }
        }
    }

    /// <summary>容器输出端口：绑定目标必须落在子树内且是执行节点，绑定的端口必须是目标声明的端口，
    /// 上下文端口不落在不产出上下文端口的节点上。</summary>
    private static void CheckContainerOutputs(
        NodeSpec container,
        IReadOnlyDictionary<PortName, ContainerOutput> outs,
        HashSet<NodeLabel> subtreeLabels,
        Dictionary<NodeLabel, IReadOnlyList<Port>> subtreeOutputs,
        HashSet<NodeLabel> contextless,
        List<Error> errors)
    {
        foreach ((PortName port, ContainerOutput output) in outs)
        {
            PortRef target = output.Ref;
            if (target.Source is not { } sourceLabel)
            {
                errors.Add(NodeScope.Library.Node(container.Label.Value,
                    $"容器输出端口 {port.Value} 的绑定目标不能是对绑定端口的引用。"));
                continue;
            }

            // 绑定目标必须落在子树内且是执行节点：上下文端口透出装配上下文，Text 端口透出命名段
            if (!subtreeLabels.Contains(sourceLabel))
            {
                errors.Add(NodeScope.Library.Node(container.Label.Value,
                    $"容器输出端口 {port.Value} 的绑定目标 {target.Display} 不在容器 {container.Label} 的子树里。"));
                continue;
            }

            if (!subtreeOutputs.TryGetValue(sourceLabel, out IReadOnlyList<Port>? memberPorts))
            {
                errors.Add(NodeScope.Library.Node(container.Label.Value,
                    $"容器输出端口 {port.Value} 的绑定目标 {target.Display} 不是执行节点，无法取产出。"));
                continue;
            }

            if (memberPorts.Find(target.Port) is not { } memberPort)
            {
                errors.Add(NodeScope.Library.Node(container.Label.Value,
                    $"容器输出端口 {port.Value} 的绑定目标 {target.Display} 无法取用，目标必须是执行节点且声明了端口 {target.Port.Value}。"));
                continue;
            }

            if (memberPort.Type == Flow.PortType.Context && contextless.Contains(sourceLabel))
            {
                errors.Add(NodeScope.Library.Node(container.Label.Value,
                    $"容器输出端口 {port.Value} 的绑定目标 {target.Display} 不产出上下文端口，不能作为上下文端口来源。"));
            }
        }
    }

    /// <summary>字面子树全部成员标签唯一。实例化时成员标签映射进同一个作用域，重复会折叠歧义。</summary>
    private static void CheckLiteralLabels(
        IReadOnlyList<NodeSpec> nodes,
        HashSet<NodeLabel> seen,
        NodeSpec container,
        List<Error> errors)
    {
        foreach (NodeSpec node in nodes)
        {
            if (node.Label.Value.Contains('@'))
            {
                errors.Add(NodeScope.Library.Node(container.Label.Value, $"成员标签 {node.Label} 不能含 @，@ 是端口引用的分隔符。"));
            }
            else if (!seen.Add(node.Label))
            {
                errors.Add(NodeScope.Library.Node(container.Label.Value, $"成员标签 {node.Label} 在容器子树内重复。"));
            }

            if (node.Nodes is { Count: > 0 })
            {
                CheckLiteralLabels(node.Nodes, seen, container, errors);
            }
        }
    }

    /// <summary>收集容器整棵子树（含被引用容器定义）的全部成员标签。</summary>
    private static void CollectSubtreeLabels(
        IReadOnlyList<NodeSpec> nodes,
        Dictionary<NodeName, NodeSpec> byName,
        HashSet<NodeLabel> labels)
    {
        foreach (NodeSpec node in nodes)
        {
            labels.Add(node.Label);

            if (node.Nodes is { Count: > 0 })
            {
                CollectSubtreeLabels(node.Nodes, byName, labels);
            }
            else if (node.Use is { } use && byName.TryGetValue(use, out NodeSpec? referenced) && referenced.Nodes is { Count: > 0 })
            {
                CollectSubtreeLabels(referenced.Nodes, byName, labels);
            }
        }
    }
    /// <summary>校验容器的一个成员：结构不混用、引用存在、From 引用自包含。</summary>
    private static void CheckMember(
        NodeSpec member,
        HashSet<PortName> ports,
        HashSet<NodeLabel> subtreeLabels,
        Dictionary<NodeName, NodeSpec> byName,
        List<Error> errors)
    {
        if (member.Use is { } use)
        {
            if (!byName.ContainsKey(use))
            {
                errors.Add(NodeScope.Library.Node(member.Label.Value, $"引用的节点 {use} 不在节点库。"));
            }

            if (member.Spec is not null || member.Nodes is not null)
            {
                errors.Add(NodeScope.Library.Node(member.Label.Value, "引用成员不能同时声明执行配置或子节点。"));
            }
        }
        else if (member.Spec is not null && member.Nodes is { Count: > 0 })
        {
            errors.Add(NodeScope.Library.Node(member.Label.Value, "成员不能同时声明执行配置与子节点。"));
        }
        else if (member.Spec is null && member.Nodes is not { Count: > 0 })
        {
            errors.Add(NodeScope.Library.Node(member.Label.Value, "成员必须声明执行配置或子节点或引用。"));
        }

        if (member.Use is not null && member.MaxExecutions is { } referenceLimit && referenceLimit < 1)
        {
            errors.Add(NodeScope.Library.Node(member.Label.Value, "MaxExecutions 必须是正整数。"));
        }

        if (member.Use is null && member.Spec is { MaxExecutions: { } executionLimit } && executionLimit < 1)
        {
            errors.Add(NodeScope.Library.Node(member.Label.Value, "MaxExecutions 必须是正整数。"));
        }

        foreach (PortRef reference in member.From.SelectMany(group => group.Links).Select(link => link.Ref))
        {
            bool inScope = reference.Source is { } source
                ? subtreeLabels.Contains(source)
                : ports.Contains(reference.Port);
            if (!inScope)
            {
                string message = reference.Source is null
                    ? $"端口 @{reference.Port.Value} 没有在此容器上声明。"
                    : $"From 引用的节点 {reference.Source.Value} 不在容器 {member.Label} 的作用域里。";
                errors.Add(NodeScope.Library.Node(member.Label.Value, message));
            }
        }
    }

    /// <summary>节点组内成员的模型来源：执行成员写模型槽位名，组内节点的模型从引用处绑定输入，不设全局模型引用。
    /// 引用不存在的定义只由 CheckMember 报错，这里不叠加槽位消息。</summary>
    private static void CheckMemberModel(
        NodeSpec member,
        Dictionary<NodeName, NodeSpec> byName,
        List<Error> errors)
    {
        if (member.Nodes is { Count: > 0 })
        {
            if (member.Model is not null || member.Models is not null || member.ModelSlots is not null)
            {
                errors.Add(NodeScope.Library.Node(member.Label.Value, "内联容器成员不声明模型、模型槽位与模型绑定。"));
            }

            return;
        }

        if (member.Use is { } use)
        {
            if (!byName.TryGetValue(use, out NodeSpec? referenced))
            {
                return;
            }

            if (referenced.Nodes is { Count: > 0 })
            {
                if (member.Model is not null)
                {
                    errors.Add(NodeScope.Library.Node(member.Label.Value, "引用节点组的成员不声明模型，节点组内节点的模型用 Models 绑定。"));
                }

                return;
            }

            if (ShapeOf(referenced) is { DispatchesExecutions: false })
            {
                if (member.Model is not null)
                {
                    errors.Add(NodeScope.Library.Node(member.Label.Value,
                        $"引用{KindName(referenced)}成员的节点不声明模型槽位。"));
                }

                return;
            }
        }
        else if (ShapeOf(member) is { DispatchesExecutions: false })
        {
            if (member.Model is not null)
            {
                errors.Add(NodeScope.Library.Node(member.Label.Value,
                    $"{KindName(member)}成员不启动执行，不声明模型槽位。"));
            }

            return;
        }

        if (member.Model is null)
        {
            errors.Add(NodeScope.Library.Node(member.Label.Value, "节点组内执行成员必须声明模型槽位，模型从引用处绑定输入。"));
        }

        if (member.Models is not null)
        {
            errors.Add(NodeScope.Library.Node(member.Label.Value, "执行成员不写模型绑定，绑定只属于引用节点组。"));
        }
    }
}
