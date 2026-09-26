using ErrorOr;
using Kuroe.Shared.Agent;
using Kuroe.Shared.Workflows.Flows;

namespace Kuroe.Workflows.Flows;

/// <summary>流程模板的校验规则。任务推进依赖这些不变量成立，加载与导入时逐条校验。
/// 一条流程内的错误一次给全。</summary>
static class WorkflowRules
{
    /// <summary>校验一条流程。</summary>
    public static ErrorOr<Success> Validate(Workflow flow, int attemptLimit)
    {
        List<Error> errors = [];
        if (flow.Nodes.Count == 0)
        {
            errors.Add(WorkflowErrors.Body(flow.Name, "至少要有一个节点。"));
        }

        var agentNames = new HashSet<string>();
        foreach (AgentDefinition agent in flow.Agents)
        {
            if (string.IsNullOrWhiteSpace(agent.Name))
            {
                errors.Add(WorkflowErrors.Agent(flow.Name, "(未命名)", "agent 名不能为空。"));
            }
            else if (!agentNames.Add(agent.Name))
            {
                errors.Add(WorkflowErrors.Agent(flow.Name, agent.Name, "agent 名重复。"));
            }
        }

        var containerNames = new HashSet<string>();
        CollectContainers(flow.Nodes, containerNames);

        var names = new HashSet<string>();
        CheckTree(flow.Nodes, [], names, agentNames, containerNames, flow.Name, errors);

        // 引用类错误不存在时才展平，避免编译时的 agent 查表落空
        if (errors.Count == 0)
        {
            CheckShape(FlowCompiler.Compile(flow), flow.Name, attemptLimit, errors);
        }

        return errors.Count > 0 ? errors : Result.Success;
    }

    /// <summary>收集全部容器名，From 不允许引用容器。</summary>
    private static void CollectContainers(IReadOnlyList<NodeSpec> nodes, HashSet<string> names)
    {
        foreach (NodeSpec node in nodes)
        {
            if (node is FlowNode flow)
            {
                names.Add(node.Name);
                CollectContainers(flow.Nodes, names);
            }
        }
    }

    /// <summary>递归校验名字、From 引用、容器与 agent 引用。可见集取自上架作用域的更早兄弟加祖先容器。</summary>
    private static void CheckTree(
        IReadOnlyList<NodeSpec> siblings,
        IReadOnlyList<string> inherited,
        HashSet<string> names,
        HashSet<string> agentNames,
        HashSet<string> containerNames,
        string flowName,
        List<Error> errors)
    {
        var visible = new List<string>(inherited);
        foreach (NodeSpec node in siblings)
        {
            foreach (string from in node.From)
            {
                if (node is FlowNode)
                {
                    // 容器的 From 在节点分支被拒绝
                    break;
                }

                if (containerNames.Contains(from))
                {
                    errors.Add(WorkflowErrors.Node(flowName, node.Name, $"From 引用的 {from} 是容器节点，请直接引用其中的叶子。"));
                }
                else if (!visible.Contains(from))
                {
                    errors.Add(WorkflowErrors.Node(flowName, node.Name, $"From 引用的节点 {from} 不在可见作用域，或尚未轮到它执行。"));
                }
            }

            if (string.IsNullOrWhiteSpace(node.Name))
            {
                errors.Add(WorkflowErrors.Node(flowName, "(未命名)", "节点名不能为空。"));
            }
            else if (!names.Add(node.Name))
            {
                errors.Add(WorkflowErrors.Node(flowName, node.Name, "节点名重复。"));
            }

            switch (node)
            {
                case FlowNode flow:
                    if (flow.Nodes.Count == 0)
                    {
                        errors.Add(WorkflowErrors.Node(flowName, node.Name, "容器至少要有一个子节点。"));
                    }
                    else if (flow.Mode == ContainerMode.Parallel && flow.Nodes.Any(child => child is FlowNode))
                    {
                        errors.Add(WorkflowErrors.Node(flowName, node.Name, "并行容器只能包含叶子节点。"));
                    }
                    else
                    {
                        CheckTree(flow.Nodes, [.. visible, flow.Name], names, agentNames, containerNames, flowName, errors);
                    }

                    if (flow.Mode == ContainerMode.Parallel)
                    {
                        // 并行容器的分支叶对本层后续兄弟可见，段后的检查才能引用它们
                        foreach (NodeSpec child in flow.Nodes)
                        {
                            if (child is AgentNode branch)
                            {
                                visible.Add(branch.Name);
                            }
                        }
                    }

                    if (flow.From.Count > 0)
                    {
                        errors.Add(WorkflowErrors.Node(flowName, node.Name, "容器节点不是叶子，不支持 From。"));
                    }

                    if (flow.Prompt is not null)
                    {
                        errors.Add(WorkflowErrors.Node(flowName, node.Name, "容器节点不是叶子，不支持 Prompt。"));
                    }

                    break;

                case AgentNode leaf:
                    if (string.IsNullOrWhiteSpace(leaf.Agent) || !agentNames.Contains(leaf.Agent))
                    {
                        errors.Add(WorkflowErrors.Node(flowName, node.Name, $"引用的 agent {leaf.Agent} 不存在。"));
                    }

                    break;
            }

            visible.Add(node.Name);
        }
    }
    /// <summary>展平后的形状规则：展开段、并行段与检查节点。</summary>
    private static void CheckShape(NodeGraph graph, string flowName, int attemptLimit, List<Error> errors)
    {
        for (int index = 0; index < graph.Count; index++)
        {
            LeafNode leaf = graph[index];
            if (leaf.Output != NodeOutput.Review && (leaf.OnReject is not null || leaf.MaxAttempts is not null))
            {
                errors.Add(WorkflowErrors.Node(flowName, leaf.Name, "OnReject 与 MaxAttempts 只适用于检查节点。"));
            }
        }

        for (int index = 0; index < graph.Count; index++)
        {
            // 按条目展开的实施依赖段拆分，段后检查叶的 PerItem 是逐条检查，不需要落段
            if (graph[index].Mode == NodeMode.PerItem && graph[index].Output != NodeOutput.Review
                && graph.SegmentFor(index) is null)
            {
                errors.Add(WorkflowErrors.Node(flowName, graph[index].Name, "按条目展开的实施必须落入展开段或并行段。"));
            }
        }

        if (graph.Segments.Count > 1)
        {
            errors.Add(WorkflowErrors.Body(flowName, "流程只允许一个展开或并行段。"));
        }

        UnitSegment? segment = graph.Segments.Count > 0 ? graph.Segments[0] : null;
        if (segment is not null)
        {
            if (segment.Source < 0 || graph[segment.Source].Output != NodeOutput.Plan)
            {
                errors.Add(WorkflowErrors.Node(flowName, graph[segment.Start].Name,
                    segment.IsParallel ? "并行段的分支需要在之前有一个分配节点。" : "按条目展开的叶子需要先有规划节点。"));
            }

            if (segment.IsParallel)
            {
                // 分支叶按分配条目各派实施者，PerItem 是并行分支的展开依据
                for (int branch = segment.Start; branch < segment.Exit; branch++)
                {
                    if (graph[branch].Mode != NodeMode.PerItem)
                    {
                        errors.Add(WorkflowErrors.Node(flowName, graph[branch].Name, "并行段的分支叶必须按条目展开。"));
                    }
                }

                // 并行段之后只能有一个检查叶子收尾：收拢检查整体过，逐条检查分别过
                int exit = segment.Exit;
                if (exit >= graph.Count || exit != graph.Count - 1 || graph[exit].Output != NodeOutput.Review)
                {
                    errors.Add(WorkflowErrors.Body(flowName, "并行段之后只能有一个检查叶子收尾。"));
                }
            }
            else
            {
                // 段后只能跟一个收拢检查收尾，否则整叶在展开段后会被每条目各跑一次
                for (int index = segment.Exit; index < graph.Count; index++)
                {
                    if (index == segment.Exit && graph.IsFunnel(index) && index == graph.Count - 1)
                    {
                        continue;
                    }

                    errors.Add(WorkflowErrors.Body(flowName, "展开段之后只能有一个收拢检查叶子收尾。"));
                    break;
                }
            }

            // 段内每条实施都必须被某个检查节点引用
            for (int index = segment.Start; index < segment.Exit; index++)
            {
                if (graph[index].Output != NodeOutput.Plain)
                {
                    continue;
                }

                if (!graph.Leaves.Any(leaf => leaf.Output == NodeOutput.Review && leaf.From.Contains(index)))
                {
                    errors.Add(WorkflowErrors.Node(flowName, graph[index].Name, "按条目展开的实施必须被某个检查节点引用。"));
                }
            }
        }

        int reviews = 0;
        for (int index = 0; index < graph.Count; index++)
        {
            LeafNode leaf = graph[index];
            if (leaf.Output != NodeOutput.Review)
            {
                continue;
            }

            reviews++;
            if (leaf.From.Count == 0 || leaf.From.All(from => graph[from].Output == NodeOutput.Plan))
            {
                errors.Add(WorkflowErrors.Node(flowName, leaf.Name, "检查节点必须用 From 引用被检查的实施产出。"));
            }

            if (graph.IsFunnel(index) && leaf.Gate == NodeGate.Review)
            {
                errors.Add(WorkflowErrors.Node(flowName, leaf.Name, "收拢检查不支持待批准门控。"));
            }

            if (leaf.MaxAttempts is < 1)
            {
                errors.Add(WorkflowErrors.Node(flowName, leaf.Name, "MaxAttempts 必须为正整数。"));
            }

            if (leaf.MaxAttempts > attemptLimit)
            {
                errors.Add(WorkflowErrors.Node(flowName, leaf.Name, $"MaxAttempts 超过 Agent:MaxAttempts={attemptLimit}。"));
            }
        }

        if (reviews == 0)
        {
            errors.Add(WorkflowErrors.Body(flowName, "流程需要有检查节点。"));
        }
    }

    /// <summary>一组流程里重复的流程名，没有时为空。</summary>
    public static string? Duplicated(IReadOnlyList<Workflow> flows) =>
        flows.GroupBy(flow => flow.Name).FirstOrDefault(group => group.Count() > 1)?.Key;
}