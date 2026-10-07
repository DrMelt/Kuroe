using ErrorOr;
using Kuroe.Shared.Workflows.Graph;
using Flow = Kuroe.Shared.Workflows.Flows;

namespace Kuroe.Workflows.Flows;

/// <summary>节点库定义的校验：结构合法、端口声明、子树自包含。展开在 NodeExpander 里进行。</summary>
internal static class NodeLibraryRules
{
    private const char PortPrefix = '@';
    private static bool IsPort(Flow.NodeName name) => name.Value.StartsWith(PortPrefix);
    private static string PortOf(Flow.NodeName name) => name.Value[1..];

    /// <summary>校验节点库：定义结构与子树自包含。各流程随后按节点库展开。</summary>
    public static ErrorOr<Success> Validate(IReadOnlyList<Flow.NodeSpec> library)
    {
        List<Error> errors = [];
        var byName = ValidateDefinitions(library, errors);

        foreach (Flow.NodeSpec node in library)
        {
            if (node.Nodes is { Count: > 0 })
            {
                CheckContainer(node, node.Nodes, byName, errors);
            }
            else
            {
                CheckLeaf(node, errors);
            }
        }

        return errors.Count > 0 ? errors : Result.Success;
    }

    private static Dictionary<Flow.NodeName, Flow.NodeSpec> ValidateDefinitions(IReadOnlyList<Flow.NodeSpec> library, List<Error> errors)
    {
        var byName = new Dictionary<Flow.NodeName, Flow.NodeSpec>();
        foreach (Flow.NodeSpec node in library)
        {
            if (node.Use is not null)
            {
                errors.Add(FlowErrors.Node("节点库", node.Name.Value, "节点库定义不能是引用。"));
                continue;
            }

            if (string.IsNullOrWhiteSpace(node.Name.Value))
            {
                errors.Add(FlowErrors.Node("节点库", "(未命名)", "节点名不能为空。"));
                continue;
            }

            if (node.Name.Value.Contains('@'))
            {
                errors.Add(FlowErrors.Node("节点库", node.Name.Value, "节点名不能含 @，@ 是端口引用的分隔符。"));
                continue;
            }

            if (!byName.TryAdd(node.Name, node))
            {
                errors.Add(FlowErrors.Node("节点库", node.Name.Value, "节点名重复。"));
                continue;
            }

            if (node.Nodes is { Count: > 0 })
            {
                if (node.Execution is not null)
                {
                    errors.Add(FlowErrors.Node("节点库", node.Name.Value, "容器不能同时声明执行配置。"));
                }

                if (node.From.Count > 0)
                {
                    errors.Add(FlowErrors.Node("节点库", node.Name.Value, "库容器定义不接线，不能声明 From。"));
                }

                if (node.In is not null)
                {
                    errors.Add(FlowErrors.Node("节点库", node.Name.Value, "库容器定义不接线，不能声明输入端口绑定。"));
                }

                if (node.Model is not null || node.Models is not null)
                {
                    errors.Add(FlowErrors.Node("节点库", node.Name.Value, "库容器定义不声明模型与模型绑定，绑定由引用处提供。"));
                }
            }
            else
            {
                if (node.Execution is null)
                {
                    errors.Add(FlowErrors.Node("节点库", node.Name.Value, "节点必须声明执行配置或子节点。"));
                }

                if (node.Execution?.Output == Flow.NodeOutput.Input)
                {
                    if (node.Model is not null)
                    {
                        errors.Add(FlowErrors.Node("节点库", node.Name.Value, "输入成员不启动 run，不声明模型槽位。"));
                    }
                }
                else
                {
                    if (node.Model is not null)
                    {
                        errors.Add(FlowErrors.Node("节点库", node.Name.Value, "库执行定义不声明模型，模型由使用处输入。"));
                    }

                    if (node.Models is not null)
                    {
                        errors.Add(FlowErrors.Node("节点库", node.Name.Value, "库执行定义不写模型绑定，绑定只属于引用节点组。"));
                    }
                }
            }
        }

        return byName;
    }
    /// <summary>执行节点库叶子：不能再带结构与接线。</summary>
    private static void CheckLeaf(Flow.NodeSpec node, List<Error> errors)
    {
        if (node.Inputs.Count > 0)
        {
            errors.Add(FlowErrors.Node("节点库", node.Name.Value, "执行节点不能声明输入端口，接线从引用处提供。"));
        }

        if (node.From.Count > 0)
        {
            errors.Add(FlowErrors.Node("节点库", node.Name.Value, "执行节点库定义的接线由引用处提供，不能写 From。"));
        }

        if (node.In is not null)
        {
            errors.Add(FlowErrors.Node("节点库", node.Name.Value, "执行节点库定义不绑定输入端口，接线从引用处提供。"));
        }

        if (node.Execution?.MaxRuns is { } limit && limit < 1)
        {
            errors.Add(FlowErrors.Node("节点库", node.Name.Value, "MaxRuns 必须是正整数。"));
        }

        ValidateInterface(node, errors);
    }

    /// <summary>节点接口的库规则：输出端口只声明在整节点文本产出上，输入成员免端口与前置。</summary>
    private static void ValidateInterface(Flow.NodeSpec node, List<Error> errors)
    {
        if (node.Execution is not { } executable || executable.Output == Flow.NodeOutput.Input)
        {
            if (node.Outputs.Count > 0 || node.SystemPrompt.Count > 0)
            {
                errors.Add(FlowErrors.Node("节点库", node.Name.Value, "输入成员不启动 run，不能声明输出端口与系统指令。"));
            }

            return;
        }

        if (executable.Question is { Length: > 0 })
        {
            errors.Add(FlowErrors.Node("节点库", node.Name.Value, "Question 只属于输入节点。"));
        }

        if (node.Outputs.Count > 0 && (executable.Output != Flow.NodeOutput.Text || executable.Mode != Flow.NodeMode.Single))
        {
            errors.Add(FlowErrors.Node("节点库", node.Name.Value, "输出端口只能声明在整节点文本产出上。"));
        }

        foreach (Flow.PortName port in node.Outputs.Where(port => port == ExecutableNode.ContextOutputPort || port == ExecutableNode.ContextInputPort))
        {
            errors.Add(FlowErrors.Node("节点库", node.Name.Value, $"输出端口名 {port.Value} 是保留名，隐式端口无需声明。"));
        }

        foreach (Flow.PortName port in node.Outputs
            .GroupBy(port => port)
            .Where(group => group.Count() > 1)
            .Select(group => group.Key))
        {
            errors.Add(FlowErrors.Node("节点库", node.Name.Value, $"输出端口 {port.Value} 重复。"));
        }

        foreach (string block in node.SystemPrompt.Where(block => string.IsNullOrWhiteSpace(block)))
        {
            errors.Add(FlowErrors.Node("节点库", node.Name.Value, "系统指令块不能为空。"));
        }
    }

    /// <summary>校验容器子树：整棵子树名字扁平唯一，成员引用存在且结构合法，From 全部落在子树或声明端口内。</summary>
    private static void CheckContainer(
        Flow.NodeSpec container,
        IReadOnlyList<Flow.NodeSpec> members,
        Dictionary<Flow.NodeName, Flow.NodeSpec> byName,
        List<Error> errors)
    {
        if (container.MaxRuns is { } limit && limit < 1)
        {
            errors.Add(FlowErrors.Node("节点库", container.Name.Value, "MaxRuns 必须是正整数。"));
        }

        var subtreeNames = new HashSet<Flow.NodeName>();
        CollectSubtreeNames(members, byName, subtreeNames);

        var literalNames = new HashSet<Flow.NodeName>();
        CheckLiteralNames(members, literalNames, container, errors);

        var ports = new HashSet<Flow.PortName>([.. container.Inputs]);
        foreach (Flow.NodeSpec member in members)
        {
            CheckMember(member, ports, subtreeNames, byName, errors);
            CheckMemberModel(member, byName, errors);
            ValidateInterface(member, errors);
            if (member.Nodes is { Count: > 0 })
            {
                CheckContainer(member, member.Nodes, byName, errors);
            }
        }
    }

    /// <summary>字面子树全部成员名唯一。实例化时成员名映射进同一个作用域，重名会折叠歧义。</summary>
    private static void CheckLiteralNames(
        IReadOnlyList<Flow.NodeSpec> nodes,
        HashSet<Flow.NodeName> seen,
        Flow.NodeSpec container,
        List<Error> errors)
    {
        foreach (Flow.NodeSpec node in nodes)
        {
            Flow.NodeName name = node.Name.Value.Length > 0
                ? node.Name
                : new Flow.NodeName(node.Use?.Value ?? string.Empty);
            if (name.Value.Contains('@'))
            {
                errors.Add(FlowErrors.Node("节点库", container.Name.Value, $"成员名 {name} 不能含 @，@ 是端口引用的分隔符。"));
            }
            else if (!seen.Add(name))
            {
                errors.Add(FlowErrors.Node("节点库", container.Name.Value, $"成员名 {name} 在容器子树内重复。"));
            }

            if (node.Nodes is { Count: > 0 })
            {
                CheckLiteralNames(node.Nodes, seen, container, errors);
            }
        }
    }

    /// <summary>收集容器整棵子树（含被引用容器定义）的全部成员名。</summary>
    private static void CollectSubtreeNames(
        IReadOnlyList<Flow.NodeSpec> nodes,
        Dictionary<Flow.NodeName, Flow.NodeSpec> byName,
        HashSet<Flow.NodeName> names)
    {
        foreach (Flow.NodeSpec node in nodes)
        {
            if (node.Name.Value.Length > 0)
            {
                names.Add(node.Name);
            }

            if (node.Nodes is { Count: > 0 })
            {
                CollectSubtreeNames(node.Nodes, byName, names);
            }
            else if (node.Use is { } use && byName.TryGetValue(use, out Flow.NodeSpec? referenced) && referenced.Nodes is { Count: > 0 })
            {
                CollectSubtreeNames(referenced.Nodes, byName, names);
            }
        }
    }
    /// <summary>校验容器的一个成员：结构不混用、引用存在、From 引用自包含。</summary>
    private static void CheckMember(
        Flow.NodeSpec member,
        HashSet<Flow.PortName> ports,
        HashSet<Flow.NodeName> subtreeNames,
        Dictionary<Flow.NodeName, Flow.NodeSpec> byName,
        List<Error> errors)
    {
        if (member.Use is { } use)
        {
            if (!byName.ContainsKey(use))
            {
                errors.Add(FlowErrors.Node("节点库", member.Name.Value, $"引用的节点 {use} 不在节点库。"));
            }

            if (member.Execution is not null || member.Nodes is not null)
            {
                errors.Add(FlowErrors.Node("节点库", member.Name.Value, "引用成员不能同时声明执行配置或子节点。"));
            }
        }
        else if (member.Execution is not null && member.Nodes is { Count: > 0 })
        {
            errors.Add(FlowErrors.Node("节点库", member.Name.Value, "成员不能同时声明执行配置与子节点。"));
        }
        else if (member.Execution is null && member.Nodes is not { Count: > 0 })
        {
            errors.Add(FlowErrors.Node("节点库", member.Name.Value, "成员必须声明执行配置或子节点或引用。"));
        }

        if (member.Use is not null && member.MaxRuns is { } referenceLimit && referenceLimit < 1)
        {
            errors.Add(FlowErrors.Node("节点库", member.Name.Value, "MaxRuns 必须是正整数。"));
        }

        if (member.Use is null && member.Execution is { MaxRuns: { } executionLimit } && executionLimit < 1)
        {
            errors.Add(FlowErrors.Node("节点库", member.Name.Value, "MaxRuns 必须是正整数。"));
        }

        foreach (Flow.NodeName from in member.From)
        {
            bool inScope = !IsPort(from)
                ? subtreeNames.Contains(from)
                : ports.Contains(new Flow.PortName(PortOf(from)));
            if (!inScope)
            {
                errors.Add(FlowErrors.Node("节点库", member.Name.Value,
                    IsPort(from) ? $"端口 {from} 没有在此容器上声明。" : $"From 引用的节点 {from} 不在容器 {member.Name} 的作用域里。"));
            }
        }

        foreach (Flow.NodeName from in (member.Execution?.AnyOf.SelectMany(group => group) ?? []).Distinct())
        {
            bool inScope = !IsPort(from)
                ? subtreeNames.Contains(from)
                : ports.Contains(new Flow.PortName(PortOf(from)));
            if (!inScope)
            {
                errors.Add(FlowErrors.Node("节点库", member.Name.Value,
                    IsPort(from) ? $"端口 {from} 没有在此容器上声明。" : $"AnyOf 引用的节点 {from} 不在容器 {member.Name} 的作用域里。"));
            }
        }
    }

    /// <summary>节点组内成员的模型来源：执行成员写模型槽位名，组内节点的模型从引用处绑定输入，不设全局模型引用。
    /// 引用不存在的定义只由 CheckMember 报错，这里不叠加槽位消息。</summary>
    private static void CheckMemberModel(
        Flow.NodeSpec member,
        Dictionary<Flow.NodeName, Flow.NodeSpec> byName,
        List<Error> errors)
    {
        if (member.Nodes is { Count: > 0 })
        {
            if (member.Model is not null || member.Models is not null)
            {
                errors.Add(FlowErrors.Node("节点库", member.Name.Value, "内联容器成员不声明模型与模型绑定。"));
            }

            return;
        }

        if (member.Use is { } use)
        {
            if (!byName.TryGetValue(use, out Flow.NodeSpec? referenced))
            {
                return;
            }

            if (referenced.Nodes is { Count: > 0 })
            {
                if (member.Model is not null)
                {
                    errors.Add(FlowErrors.Node("节点库", member.Name.Value, "引用节点组的成员不声明模型，节点组内节点的模型用 Models 绑定。"));
                }

                CheckModelBindings(member, errors);

                return;
            }
        }

        if (member.Execution?.Output == Flow.NodeOutput.Input)
        {
            if (member.Model is not null)
            {
                errors.Add(FlowErrors.Node("节点库", member.Name.Value, "输入成员不启动 run，不声明模型槽位。"));
            }

            return;
        }

        if (member.Model is null)
        {
            errors.Add(FlowErrors.Node("节点库", member.Name.Value, "节点组内执行成员必须声明模型槽位，模型从引用处绑定输入。"));
        }

        if (member.Models is not null)
        {
            errors.Add(FlowErrors.Node("节点库", member.Name.Value, "执行成员不写模型绑定，绑定只属于引用节点组。"));
        }
    }

    /// <summary>引用节点组的模型绑定：键与值都不能为空。</summary>
    private static void CheckModelBindings(Flow.NodeSpec member, List<Error> errors)
    {
        if (member.Models is null)
        {
            return;
        }

        foreach ((Flow.ModelRef slot, Flow.ModelRef bound) in member.Models)
        {
            if (string.IsNullOrWhiteSpace(slot.Value) || string.IsNullOrWhiteSpace(bound.Value))
            {
                errors.Add(FlowErrors.Node("节点库", member.Name.Value, "模型绑定键与值不能为空。"));
            }
        }
    }
}
