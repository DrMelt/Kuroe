using ErrorOr;
using Kuroe.Flow.Compilation.Shared.Validation;
using Kuroe.Flow.Shared.Model;
using Kuroe.Flow.Shared.StaticGraph.Nodes;
using Kuroe.Flow.Shared.StaticGraph.Ports;

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

            node.Check(NodeRuleContext.LibraryDefinition, message =>
                errors.Add(NodeScope.Library.Node(node.Label.Value, message)));
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

        if (container.Outputs.Count > 0)
        {
            CheckContainerOutputs(container, subtreeLabels, subtreeOutputs, errors);
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

    /// <summary>收集子树内全部节点的输出端口表，供容器输出端口的绑定目标校验。被引用节点取定义上的端口表。</summary>
    private static void CollectSubtreeOutputs(
        IReadOnlyList<NodeSpec> nodes,
        Dictionary<NodeName, NodeSpec> byName,
        Dictionary<NodeLabel, IReadOnlyList<Port>> outputs)
    {
        foreach (NodeSpec node in nodes)
        {
            NodeSpec declared = node.Use is { } use && byName.TryGetValue(use, out NodeSpec? definition)
                ? definition
                : node;
            if (declared.Outputs.Count > 0)
            {
                outputs[node.Label] = declared.Outputs;
            }

            if (node.Nodes is { Count: > 0 })
            {
                CollectSubtreeOutputs(node.Nodes, byName, outputs);
            }
        }
    }

    /// <summary>容器输出端口：有 Ref 时目标必须落在子树内，端口必须存在且常量性与类型一致；固定值输出不需要目标。</summary>
    private static void CheckContainerOutputs(
        NodeSpec container,
        HashSet<NodeLabel> subtreeLabels,
        Dictionary<NodeLabel, IReadOnlyList<Port>> subtreeOutputs,
        List<Error> errors)
    {
        foreach (Port output in container.Outputs)
        {
            if (output.Ref is not { } target)
            {
                continue;
            }

            if (target.Source is not { } sourceLabel)
            {
                errors.Add(NodeScope.Library.Node(container.Label.Value,
                    $"容器输出端口 {output.Name.Value} 的 Ref 必须写 成员@端口。"));
                continue;
            }

            if (!subtreeLabels.Contains(sourceLabel))
            {
                errors.Add(NodeScope.Library.Node(container.Label.Value,
                    $"容器输出端口 {output.Name.Value} 的绑定目标 {target.Display} 不在容器 {container.Label} 的子树里。"));
                continue;
            }

            if (!subtreeOutputs.TryGetValue(sourceLabel, out IReadOnlyList<Port>? memberPorts)
                || memberPorts.Find(target.Port) is not { } memberPort)
            {
                errors.Add(NodeScope.Library.Node(container.Label.Value,
                    $"容器输出端口 {output.Name.Value} 的绑定目标 {target.Display} 没有输出端口 {target.Port.Value}。"));
                continue;
            }

            if (memberPort.IsConstant != output.IsConstant)
            {
                errors.Add(NodeScope.Library.Node(container.Label.Value,
                    $"容器输出端口 {output.Name.Value} 的常量性必须与绑定目标 {target.Display} 一致。"));
            }

            if (memberPort.Type != output.Type)
            {
                errors.Add(NodeScope.Library.Node(container.Label.Value,
                    $"容器输出端口 {output.Name.Value} 的类型必须与绑定目标 {target.Display} 的类型一致。"));
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
