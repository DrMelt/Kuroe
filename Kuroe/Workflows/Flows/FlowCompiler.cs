using Kuroe.Shared.Workflows.Flows;

namespace Kuroe.Workflows.Flows;

/// <summary>把流程树编译成执行视图：叶子按先根序展平，From 的名字解析成叶子序号。
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
        SecondPass(flow.Nodes, agents, order, result, []);

        return new NodeGraph(result);
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
        List<string> path)
    {
        foreach (NodeSpec node in nodes)
        {
            if (node is AgentNode leaf)
            {
                int index = result.Count;
                result.Add(new LeafNode(
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
                    leaf.MaxAttempts));
            }
            else if (node is FlowNode flow)
            {
                path.Add(node.Name);
                SecondPass(flow.Nodes, agents, order, result, path);
                path.RemoveAt(path.Count - 1);
            }
        }
    }
}