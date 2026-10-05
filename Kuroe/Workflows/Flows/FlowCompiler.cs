using Kuroe.Shared.Workflows.Graph;
using Flow = Kuroe.Shared.Workflows.Flows;

namespace Kuroe.Workflows.Flows;

/// <summary>把流程树编译成执行视图：节点按先根序统一编号，容器登记成员与子容器，From 引用编译为带消费方式的边。
/// 声明 AnyOf 的执行节点按来源各生成一条单份边，组内与组间重复来源不重复建边。
/// 要求已通过 FlowRules 校验，模型选择引用与节点名字都可解析。传入的树必须是展开后的节点树。</summary>
internal static class FlowCompiler
{
    /// <summary>编译流程树。</summary>
    public static NodeGraph Compile(Flow.FlowDefinition flow)
    {
        Dictionary<Flow.ModelRef, Flow.ModelDefinition> models = flow.Models.ToDictionary(model => model.Name);

        var nodes = new List<GraphNode>();
        var byName = new Dictionary<Flow.NodeName, int>();
        var containers = new Dictionary<int, ContainerBuilder>();

        // 先登记全部名字与序号，From 对后面的节点引用才可解析
        RegisterNames(flow.RootNode, byName, 0);
        SecondPass(flow.RootNode, models, nodes, byName, containers, [], parent: null);

        // 用登记好的成员与子容器固化容器定义
        foreach (ContainerBuilder builder in containers.Values)
        {
            nodes[builder.Index] = new ContainerNode(builder.Index, builder.Name, builder.Path, builder.Gate,
                builder.Members, builder.SubContainers);
        }

        List<FlowEdge> edges = [];
        foreach (ExecutableNode executable in nodes.OfType<ExecutableNode>())
        {
            foreach (int from in executable.From)
            {
                edges.Add(new FlowEdge(from, executable.Index, EdgeFeedRules.Of(nodes[from], executable.Mode)));
            }

            // AnyOf 组的成员按来源各接一条单份产出边，组内与组间重复来源不重复建边
            foreach (int from in executable.AnyOf.SelectMany(group => group).Distinct())
            {
                edges.Add(new FlowEdge(from, executable.Index, EdgeFeed.Single));
            }
        }

        return new NodeGraph(nodes, edges);
    }

    /// <summary>容器定义的编译期登记，最终固化为 <see cref="ContainerNode"/>。</summary>
    private sealed record ContainerBuilder(
        int Index,
        Flow.NodeName Name,
        string Path,
        Flow.NodeGate Gate,
        List<int> Members,
        List<int> SubContainers);

    /// <summary>先根序登记全部节点名与统一序号，第二遍用它把 From 名字转成序号。</summary>
    private static int RegisterNames(Flow.NodeSpec node, Dictionary<Flow.NodeName, int> order, int next)
    {
        order[node.Name] = next;
        next++;
        if (node.Nodes is { Count: > 0 } children)
        {
            foreach (Flow.NodeSpec child in children)
            {
                next = RegisterNames(child, order, next);
            }
        }

        return next;
    }

    private static void SecondPass(
        Flow.NodeSpec node,
        Dictionary<Flow.ModelRef, Flow.ModelDefinition> models,
        List<GraphNode> nodes,
        Dictionary<Flow.NodeName, int> byName,
        Dictionary<int, ContainerBuilder> containers,
        List<string> path,
        int? parent)
    {
        if (node.Execution is { } execution)
        {
            int index = nodes.Count;
            nodes.Add(NewExecutable(node, execution, index, path, models, byName));
            if (parent is { } memberContainer)
            {
                containers[memberContainer].Members.Add(index);
            }

            return;
        }

        int container = nodes.Count;
        string containerPath = string.Join('/', [.. path, node.Name.Value]);
        containers[container] = new ContainerBuilder(container, node.Name, containerPath, node.Gate, [], []);
        nodes.Add(new ContainerNode(container, node.Name, containerPath, node.Gate, [], []));
        if (parent is { } childContainer)
        {
            containers[childContainer].SubContainers.Add(container);
        }

        path.Add(node.Name.Value);
        foreach (Flow.NodeSpec child in node.Nodes!)
        {
            SecondPass(child, models, nodes, byName, containers, path, container);
        }
        path.RemoveAt(path.Count - 1);
    }

    private static ExecutableNode NewExecutable(
        Flow.NodeSpec node,
        Flow.ExecutableSpec execution,
        int index,
        List<string> path,
        Dictionary<Flow.ModelRef, Flow.ModelDefinition> models,
        Dictionary<Flow.NodeName, int> order) => new(
        index,
        node.Name,
        string.Join('/', [.. path, node.Name.Value]),
        node.Gate,
        models[node.Model!.Value],
        execution.Tools,
        execution.Prompt,
        execution.Output,
        execution.Mode,
        execution.Branch,
        [.. node.From.Select(name => order[name])],
        [.. execution.AnyOf.Select(group => (IReadOnlyList<int>)[.. group.Select(name => order[name])])],
        execution.Validate,
        execution.MaxRuns ?? ExecutableNode.DefaultMaxRuns,
        execution.Split);
}
