using ErrorOr;
using Kuroe.Flow.Shared;
using Kuroe.Flow.Shared.StaticGraph.Node;
using Kuroe.Flow.Shared.StaticGraph.PortModel;

namespace Kuroe.Flow.NodeLibrary;

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
            node.Type?.Validate(node, NodeScope.Library, errors.Add);

            if (ShapeOf(node) is { Organizes: true })
            {
                if (node.Nodes is { Count: > 0 } members)
                {
                    CheckContainer(node, members, byName, errors);
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

            string identityLabel = string.IsNullOrWhiteSpace(node.Name?.Value) ? "(未命名)" : node.Label.Value;
            List<Error> identityErrors = [];
            node.CheckIdentity(NodeRuleContext.LibraryDefinition, message =>
                identityErrors.Add(NodeScope.Library.Node(identityLabel, message)));
            if (identityErrors.Count > 0)
            {
                errors.AddRange(identityErrors);
                continue;
            }

            if (node.Name is not { } name)
            {
                continue;
            }

            byName[name] = node;

            if (ShapeOf(node) is { Organizes: true })
            {
                if (node.Outputs.Count > 0)
                {
                    errors.Add(NodeScope.Library.Node(node.Label.Value, "库容器定义不声明执行节点输出端口，容器输出端口条目写 Name 与 Ref。"));
                }

                node.Check(NodeRuleContext.LibraryDefinition, message =>
                    errors.Add(NodeScope.Library.Node(node.Label.Value, message)));
            }
            else
            {
                node.Check(NodeRuleContext.LibraryDefinition, message =>
                    errors.Add(NodeScope.Library.Node(node.Label.Value, message)));
            }
        }

        return byName;
    }

    /// <summary>执行节点库叶子的库层限制：端口与类型规则由 NodeSpec 承担，这里只拒绝非执行节点声明多个输出端口。</summary>
    private static void CheckLeaf(NodeSpec node, List<Error> errors) => ValidateInterface(node, errors);

    /// <summary>非执行节点的输出端口上限，属于库层对接口的额外限制。</summary>
    private static void ValidateInterface(NodeSpec node, List<Error> errors)
    {
        if ((node.Spec is null || ShapeOf(node) is { DispatchesExecutions: false }) && node.Outputs.Count > 1)
        {
            errors.Add(NodeScope.Library.Node(node.Label.Value, "非执行节点最多一个输出端口。"));
        }
    }

    /// <summary>校验容器子树：整棵子树标签扁平唯一，成员引用存在且结构合法，From 全部落在子树或声明端口内。</summary>
    private static void CheckContainer(
        NodeSpec container,
        IReadOnlyList<NodeSpec> members,
        Dictionary<NodeName, NodeSpec> byName,
        List<Error> errors)
    {
        NodeSpec? find(NodeName name) => byName.GetValueOrDefault(name);
        HashSet<NodeLabel> subtreeLabels = ContainerScope.SubtreeLabels(members, find);

        var subtreeOutputs = new Dictionary<NodeLabel, IReadOnlyList<Port>>();
        CollectSubtreeOutputs(members, byName, subtreeOutputs);

        var subtreeStatics = new Dictionary<NodeLabel, IReadOnlySet<PortName>>();
        CollectSubtreeStaticPorts(members, byName, subtreeStatics);

        var contextless = new HashSet<NodeLabel>();
        CollectContextless(members, byName, contextless);

        if (container.ContainerOutputs is { } outs)
        {
            CheckContainerOutputs(container, outs, subtreeLabels, subtreeOutputs, subtreeStatics, contextless, errors);
        }

        var literalLabels = new HashSet<NodeLabel>();
        CheckLiteralLabels(members, literalLabels, container, errors);

        ContainerScope.Check(container, members, find, (member, message) =>
            errors.Add(NodeScope.Library.Node(member.Label.Value, message)));

        foreach (NodeSpec member in members)
        {
            member.Type?.Validate(member, NodeScope.Library, errors.Add);
            CheckMember(member, byName, errors);
            member.CheckStatic(message =>
                errors.Add(NodeScope.Library.Node(member.Label.Value, message)));
            if (member.Spec is not null)
            {
                member.CheckStaticCoverage(message =>
                    errors.Add(NodeScope.Library.Node(member.Label.Value, message)));
            }

            member.CheckInterface(ShapeOf(member), message =>
                errors.Add(NodeScope.Library.Node(member.Label.Value, message)));
            ValidateInterface(member, errors);
            if (member.Nodes is { Count: > 0 })
            {
                CheckContainer(member, member.Nodes, byName, errors);
            }
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

    /// <summary>收集子树内成员声明的静态端口：执行节点取静态输入，容器取静态输出。被引用节点取定义上的声明。</summary>
    private static void CollectSubtreeStaticPorts(
        IReadOnlyList<NodeSpec> nodes,
        Dictionary<NodeName, NodeSpec> byName,
        Dictionary<NodeLabel, IReadOnlySet<PortName>> statics)
    {
        foreach (NodeSpec node in nodes)
        {
            NodeSpec declared = node.Use is { } use && byName.TryGetValue(use, out NodeSpec? definition)
                ? definition
                : node;
            HashSet<PortName> ports = declared.Nodes is { Count: > 0 }
                ? [.. (declared.ContainerOutputs ?? new Dictionary<PortName, ContainerOutput>())
                    .Where(pair => pair.Value.IsStatic)
                    .Select(pair => pair.Key)]
                : [.. declared.EffectiveInputs.Where(port => port.IsStatic).Select(port => port.Name)];
            if (ports.Count > 0)
            {
                statics[node.Label] = ports;
            }

            if (node.Nodes is { Count: > 0 } children)
            {
                CollectSubtreeStaticPorts(children, byName, statics);
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
                && !shape.OutputTypes.Any(type => type.Semantics == PortSemantics.Context))
            {
                contextless.Add(node.Label);
            }

            if (node.Nodes is { Count: > 0 })
            {
                CollectContextless(node.Nodes, byName, contextless);
            }
        }
    }

    /// <summary>容器输出端口：绑定目标必须落在子树内。动态输出取执行节点的输出端口，静态输出取成员的静态端口，
    /// 上下文端口不落在不产出上下文端口的节点上。</summary>
    private static void CheckContainerOutputs(
        NodeSpec container,
        IReadOnlyDictionary<PortName, ContainerOutput> outs,
        HashSet<NodeLabel> subtreeLabels,
        Dictionary<NodeLabel, IReadOnlyList<Port>> subtreeOutputs,
        Dictionary<NodeLabel, IReadOnlySet<PortName>> subtreeStatics,
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

            // 绑定目标必须落在子树内且是执行节点：上下文端口透出装配上下文，Text 与 Signal 端口透出交回段
            if (!subtreeLabels.Contains(sourceLabel))
            {
                errors.Add(NodeScope.Library.Node(container.Label.Value,
                    $"容器输出端口 {port.Value} 的绑定目标 {target.Display} 不在容器 {container.Label} 的子树里。"));
                continue;
            }

            if (output.IsStatic)
            {
                if (!subtreeStatics.TryGetValue(sourceLabel, out IReadOnlySet<PortName>? staticPorts)
                    || !staticPorts.Contains(target.Port))
                {
                    errors.Add(NodeScope.Library.Node(container.Label.Value,
                        $"静态输出端口 {port.Value} 的绑定目标 {target.Display} 无法取用，目标成员必须声明了静态端口 {target.Port.Value}。"));
                }

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

            if (memberPort.Type.Semantics == PortSemantics.Context && contextless.Contains(sourceLabel))
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

    /// <summary>校验容器的一个成员：结构不混用、引用存在。作用域来源由 ContainerScope 判定。</summary>
    private static void CheckMember(
        NodeSpec member,
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
    }

}
