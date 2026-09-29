using Kuroe.Shared.Workflows.Graph;
using Flow = Kuroe.Shared.Workflows.Flows;

namespace Kuroe.Workflows.Flows;

/// <summary>把流程树编译成执行视图：节点按先根序统一编号，容器登记成员与子容器，From 引用编译为带消费方式的边。
/// 要求已通过 WorkflowRules 校验，模型配置引用与节点名字都可解析。传入的树必须是展开后的节点树。</summary>
internal static class FlowCompiler
{
    /// <summary>编译流程树。</summary>
    public static NodeGraph Compile(Flow.Workflow flow)
    {
        Dictionary<Flow.ModelRef, Flow.ModelDefinition> models = flow.Models.ToDictionary(model => model.Name);

        var nodes = new List<GraphNode>();
        var byName = new Dictionary<Flow.NodeName, int>();
        var containers = new Dictionary<int, ContainerBuilder>();

        // 先登记全部名字与序号，From 对后面的节点引用才可解析
        RegisterNames(flow.Nodes, byName, 0);
        SecondPass(flow.Nodes, models, nodes, byName, containers, [], parent: null);

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
    private static int RegisterNames(IReadOnlyList<Flow.NodeSpec> specs, Dictionary<Flow.NodeName, int> order, int next)
    {
        foreach (Flow.NodeSpec node in specs)
        {
            order[node.Name] = next;
            next++;
            if (node.Nodes is { Count: > 0 } children)
            {
                next = RegisterNames(children, order, next);
            }
        }

        return next;
    }

    private static void SecondPass(
        IReadOnlyList<Flow.NodeSpec> specs,
        Dictionary<Flow.ModelRef, Flow.ModelDefinition> models,
        List<GraphNode> nodes,
        Dictionary<Flow.NodeName, int> byName,
        Dictionary<int, ContainerBuilder> containers,
        List<string> path,
        int? parent)
    {
        foreach (Flow.NodeSpec node in specs)
        {
            if (node.Execution is { } execution)
            {
                int index = nodes.Count;
                nodes.Add(NewExecutable(node, execution, index, path, models, byName));
                if (parent is { } memberContainer)
                {
                    containers[memberContainer].Members.Add(index);
                }

                continue;
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
            SecondPass(node.Nodes!, models, nodes, byName, containers, path, container);
            path.RemoveAt(path.Count - 1);
        }
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
        models[execution.Model],
        execution.Tools,
        execution.Prompt,
        execution.Output,
        execution.Mode,
        execution.Branch,
        [.. node.From.Select(name => order[name])],
        execution.OnReject,
        execution.MaxAttempts,
        execution.Split);
}