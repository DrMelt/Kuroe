using Kuroe.Shared.Workflows.Flows;

namespace Kuroe.Workflows.Flows;

/// <summary>把流程树编译成执行视图：节点按先根序统一编号，容器登记成员与子容器，From 引用编译为带消费方式的边。
/// 要求已通过 WorkflowRules 校验，模型配置引用与节点名字都可解析。</summary>
internal static class FlowCompiler
{
    /// <summary>编译流程树。</summary>
    public static NodeGraph Compile(Workflow flow)
    {
        Dictionary<string, ModelDefinition> models = flow.Models.ToDictionary(model => model.Name);

        var nodes = new List<GraphNode>();
        var byName = new Dictionary<string, int>();
        var groups = new Dictionary<int, GroupBuilder>();

        // 先登记全部名字与序号，From 对后面的节点引用才可解析
        RegisterNames(flow.Nodes, byName, 0);
        SecondPass(flow.Nodes, models, nodes, byName, groups, [], parent: null);

        // 用登记好的成员与子容器固化组定义
        foreach (GroupBuilder builder in groups.Values)
        {
            nodes[builder.Index] = new FlowGroup(builder.Index, builder.Name, builder.Path, builder.Gate,
                builder.Members, builder.SubGroups);
        }

        List<FlowEdge> edges = [];
        foreach (ExecutableNode executable in nodes.OfType<ExecutableNode>())
        {
            foreach (int from in executable.From)
            {
                edges.Add(new FlowEdge(from, executable.Index, EdgeFeedRules.Of(nodes[from], executable.Mode)));
            }
        }

        return new NodeGraph(nodes, edges);
    }

    /// <summary>容器定义的编译期登记，最终固化为 <see cref="FlowGroup"/>。</summary>
    private sealed record GroupBuilder(
        int Index,
        string Name,
        string Path,
        NodeGate Gate,
        List<int> Members,
        List<int> SubGroups);

    /// <summary>先根序登记全部节点名与统一序号，第二遍用它把 From 名字转成序号。</summary>
    private static int RegisterNames(IReadOnlyList<NodeSpec> specs, Dictionary<string, int> order, int next)
    {
        foreach (NodeSpec node in specs)
        {
            order[node.Name] = next;
            next++;
            if (node is FlowNode flow)
            {
                next = RegisterNames(flow.Nodes, order, next);
            }
        }

        return next;
    }

    private static void SecondPass(
        IReadOnlyList<NodeSpec> specs,
        Dictionary<string, ModelDefinition> models,
        List<GraphNode> nodes,
        Dictionary<string, int> byName,
        Dictionary<int, GroupBuilder> groups,
        List<string> path,
        int? parent)
    {
        foreach (NodeSpec node in specs)
        {
            switch (node)
            {
                case ExecuteNode executable:
                    int index = nodes.Count;
                    nodes.Add(NewExecutable(executable, index, path, models, byName));
                    if (parent is { } memberGroup)
                    {
                        groups[memberGroup].Members.Add(index);
                    }

                    break;

                case FlowNode flow:
                    int group = nodes.Count;
                    string groupPath = string.Join('/', [.. path, flow.Name]);
                    groups[group] = new GroupBuilder(group, flow.Name, groupPath, flow.Gate, [], []);
                    nodes.Add(new FlowGroup(group, flow.Name, groupPath, flow.Gate, [], []));
                    if (parent is { } childGroup)
                    {
                        groups[childGroup].SubGroups.Add(group);
                    }

                    path.Add(flow.Name);
                    SecondPass(flow.Nodes, models, nodes, byName, groups, path, group);
                    path.RemoveAt(path.Count - 1);
                    break;
            }
        }
    }

    private static ExecutableNode NewExecutable(
        ExecuteNode executable,
        int index,
        List<string> path,
        Dictionary<string, ModelDefinition> models,
        Dictionary<string, int> order) => new(
        index,
        executable.Name,
        string.Join('/', [.. path, executable.Name]),
        executable.Gate,
        models[executable.Model],
        executable.Tools,
        executable.Prompt,
        executable.Output,
        executable.Mode,
        executable.Branch,
        [.. executable.From.Select(name => order[name])],
        executable.OnReject,
        executable.MaxAttempts,
        executable.Split);
}