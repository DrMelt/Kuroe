using Kuroe.Shared.Workflows.Flows;

namespace Kuroe.Workflows.Flows;

/// <summary>把流程树编译成执行视图：叶子按先根序展平，段记录拆分的消费范围。
/// 要求已通过 WorkflowRules 校验，agent 引用与叶子名字都可解析。</summary>
internal static class FlowCompiler
{
    /// <summary>编译流程树。</summary>
    public static NodeGraph Compile(Workflow flow)
    {
        Dictionary<string, AgentDefinition> agents = flow.Agents.ToDictionary(agent => agent.Name);

        // 先根序登记叶子名与展平序号，From 只允许引用叶子
        Dictionary<string, int> order = [];
        FirstPass(flow.Nodes, order, 0);

        var result = new List<LeafNode>();
        var parallelGroups = new List<List<int>>();
        SecondPass(flow.Nodes, agents, order, result, [], parallelGroups);

        return new NodeGraph(result, BuildSegments(result, parallelGroups));
    }

    /// <summary>登记叶子名与展平序号，返回该段叶子数。</summary>
    private static int FirstPass(IReadOnlyList<NodeSpec> nodes, Dictionary<string, int> order, int next)
    {
        foreach (NodeSpec node in nodes)
        {
            if (node is AgentNode leaf)
            {
                order[node.Name] = next;
                next++;
            }
            else if (node is FlowNode flow)
            {
                next = FirstPass(flow.Nodes, order, next);
            }
        }

        return next;
    }

    private static void SecondPass(
        IReadOnlyList<NodeSpec> nodes,
        Dictionary<string, AgentDefinition> agents,
        Dictionary<string, int> order,
        List<LeafNode> result,
        List<string> path,
        List<List<int>> parallelGroups)
    {
        foreach (NodeSpec node in nodes)
        {
            switch (node)
            {
                case AgentNode leaf:
                    result.Add(NewLeaf(leaf, result.Count, path, agents, order));
                    break;

                case FlowNode flow:
                    path.Add(node.Name);
                    if (flow.Mode == ContainerMode.Parallel)
                    {
                        CollectParallelChildren(flow.Nodes, agents, order, result, path, parallelGroups);
                    }
                    else
                    {
                        SecondPass(flow.Nodes, agents, order, result, path, parallelGroups);
                    }

                    path.RemoveAt(path.Count - 1);
                    break;
            }
        }
    }

    /// <summary>并行容器的直接子叶子各是一个分支，登记为一个分支组。</summary>
    private static void CollectParallelChildren(
        IReadOnlyList<NodeSpec> nodes,
        Dictionary<string, AgentDefinition> agents,
        Dictionary<string, int> order,
        List<LeafNode> result,
        List<string> path,
        List<List<int>> parallelGroups)
    {
        parallelGroups.Add([]);
        foreach (NodeSpec node in nodes)
        {
            if (node is not AgentNode leaf)
            {
                continue;
            }

            result.Add(NewLeaf(leaf, result.Count, path, agents, order));
            parallelGroups[^1].Add(result.Count - 1);
        }
    }

    private static LeafNode NewLeaf(
        AgentNode leaf,
        int index,
        List<string> path,
        Dictionary<string, AgentDefinition> agents,
        Dictionary<string, int> order) => new(
        index,
        leaf.Name,
        string.Join('/', [.. path, leaf.Name]),
        agents[leaf.Agent],
        leaf.Prompt,
        leaf.Output,
        leaf.Mode,
        [.. leaf.From.Select(name => order[name])],
        leaf.Gate,
        leaf.OnReject,
        leaf.MaxAttempts,
        leaf.Split);

    /// <summary>段：并行容器各成一个并行段，组外的连续 PerItem 叶子构成单分支段。拆分源取段首叶 From 里的规划产出。
    /// 存在并行段时不再识别单分支段，段后的逐条检查仍沿展开出的单元推进。</summary>
    private static List<UnitSegment> BuildSegments(List<LeafNode> leaves, List<List<int>> parallelGroups)
    {
        List<UnitSegment> segments = [];

        foreach (List<int> group in parallelGroups)
        {
            int start = group[0];
            segments.Add(new UnitSegment(PlanSource(leaves, start) ?? -1, start, group[^1] + 1, group));
        }

        if (parallelGroups.Count > 0)
        {
            return segments;
        }

        for (int index = 0; index < leaves.Count;)
        {
            if (leaves[index].Mode == NodeMode.PerItem)
            {
                int start = index;
                while (index < leaves.Count && leaves[index].Mode == NodeMode.PerItem)
                {
                    index++;
                }

                segments.Add(new UnitSegment(PlanSource(leaves, start) ?? -1, start, index, []));
                continue;
            }

            index++;
        }

        return segments;
    }

    /// <summary>叶子 From 里第一个规划产出的序号，没有引用规划产出时为空。</summary>
    private static int? PlanSource(List<LeafNode> leaves, int leafIndex)
    {
        foreach (int from in leaves[leafIndex].From)
        {
            if (leaves[from].Output == NodeOutput.Plan)
            {
                return from;
            }
        }

        return null;
    }
}