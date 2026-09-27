using Kuroe.Shared.Workflows.Flows;

namespace Kuroe.Workflows.Flows;

/// <summary>把流程树编译成执行视图：执行节点按先根序展平，From 引用编译为带消费方式的边。
/// 要求已通过 WorkflowRules 校验，agent 引用与执行节点名字都可解析。</summary>
internal static class FlowCompiler
{
    /// <summary>编译流程树。</summary>
    public static NodeGraph Compile(Workflow flow)
    {
        Dictionary<string, AgentDefinition> agents = flow.Agents.ToDictionary(agent => agent.Name);

        // 先根序登记执行节点名与展平序号
        Dictionary<string, int> order = [];
        FirstPass(flow.Nodes, order, 0);

        var result = new List<ExecutableNode>();
        SecondPass(flow.Nodes, agents, order, result, []);

        List<FlowEdge> edges = [];
        for (int index = 0; index < result.Count; index++)
        {
            ExecutableNode executable = result[index];
            foreach (int from in executable.From)
            {
                edges.Add(new FlowEdge(from, index, EdgeFeedRules.Of(result[from].Mode, executable.Mode)));
            }
        }

        return new NodeGraph(result, edges);
    }

    /// <summary>登记执行节点名与展平序号，返回该段执行节点数。</summary>
    private static int FirstPass(IReadOnlyList<NodeSpec> nodes, Dictionary<string, int> order, int next)
    {
        foreach (NodeSpec node in nodes)
        {
            if (node is AgentNode executable)
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
        List<ExecutableNode> result,
        List<string> path)
    {
        foreach (NodeSpec node in nodes)
        {
            switch (node)
            {
                case AgentNode executable:
                    result.Add(NewExecutable(executable, result.Count, path, agents, order));
                    break;

                case FlowNode flow:
                    path.Add(node.Name);
                    SecondPass(flow.Nodes, agents, order, result, path);
                    path.RemoveAt(path.Count - 1);
                    break;
            }
        }
    }

    private static ExecutableNode NewExecutable(
        AgentNode executable,
        int index,
        List<string> path,
        Dictionary<string, AgentDefinition> agents,
        Dictionary<string, int> order) => new(
        index,
        executable.Name,
        string.Join('/', [.. path, executable.Name]),
        agents[executable.Agent],
        executable.Prompt,
        executable.Output,
        executable.Mode,
        executable.Branch,
        [.. executable.From.Select(name => order[name])],
        executable.Gate,
        executable.OnReject,
        executable.MaxAttempts,
        executable.Split);
}