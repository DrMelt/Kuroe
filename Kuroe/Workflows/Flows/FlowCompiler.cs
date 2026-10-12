using Kuroe.Shared.Workflows.Graph;
using Flow = Kuroe.Shared.Workflows.Flows;

namespace Kuroe.Workflows.Flows;

/// <summary>把流程树编译成执行视图：开始登记为序号 0 的唯一来源，节点按先根序统一编号，容器登记成员与子容器，
/// From 启动组编译为带取值域与输入类型的边：输入类型非 Data 的按整份消费，其余按来源与目标模式推导。
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
        nodes.Add(NewStart(flow.StartOutputs));
        byName[StartNode.ReservedName] = 0;
        RegisterNames(flow.RootNode, byName, nodes.Count);
        SecondPass(flow.RootNode, models, nodes, byName, containers, [], parent: null);

        // 用登记好的成员与子容器固化容器定义
        foreach (ContainerBuilder builder in containers.Values)
        {
            nodes[builder.Index] = new ContainerNode
            {
                Index = builder.Index,
                Name = builder.Name,
                Path = builder.Path,
                Gate = builder.Gate,
                Members = builder.Members,
                SubContainers = builder.SubContainers,
                PortBindings = builder.PortBindings,
            };
        }

        List<FlowEdge> edges = [];
        foreach (ExecutableNode executable in nodes.OfType<ExecutableNode>())
        {
            foreach (Dependency dependency in executable.From)
            {
                // 上下文与信号输入按整份消费，其余按来源与目标模式推导，来源端口与取值域正交
                (EdgeKind kind, FeedScope scope, FeedRecurrence recurrence) = dependency.Type == Flow.PortType.Data
                    ? FeedRules.Of(nodes[dependency.From], executable.Execution.Mode, dependency.Port)
                    : (EdgeKind.Value, FeedScope.Whole, FeedRecurrence.Repeat);

                edges.Add(new FlowEdge(
                    dependency.From,
                    executable.Index,
                    kind,
                    scope,
                    recurrence,
                    dependency.Port,
                    dependency.Input,
                    dependency.Type,
                    dependency.Group));
            }
        }

        return new NodeGraph(nodes, edges);
    }

    /// <summary>开始节点：序号 0，不属于任何容器，任务启动即已放行。</summary>
    private static StartNode NewStart(IReadOnlyList<Flow.Port> outputs) => new()
    {
        Index = 0,
        Name = StartNode.ReservedName,
        Path = new Flow.NodePath([StartNode.ReservedName]),
        Gate = Flow.NodeGate.Auto,
        Outputs = outputs,
    };

    /// <summary>容器定义的编译期登记，最终固化为 <see cref="ContainerNode"/>。</summary>
    private sealed record ContainerBuilder(
        int Index,
        Flow.NodeName Name,
        Flow.NodePath Path,
        Flow.NodeGate Gate,
        List<int> Members,
        List<int> SubContainers,
        IReadOnlyDictionary<Flow.PortName, Flow.PortRef>? PortBindings);

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
        List<Flow.NodeName> path,
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
        Flow.NodePath containerPath = new([.. path, node.Name]);
        IReadOnlyDictionary<Flow.PortName, Flow.PortRef>? bindings = ToBindings(node.ContainerOutputs);
        containers[container] = new ContainerBuilder(container, node.Name, containerPath, node.Gate, [], [], bindings);
        nodes.Add(new ContainerNode
        {
            Index = container,
            Name = node.Name,
            Path = containerPath,
            Gate = node.Gate,
            Members = [],
            SubContainers = [],
            PortBindings = bindings,
        });
        if (parent is { } childContainer)
        {
            containers[childContainer].SubContainers.Add(container);
        }

        path.Add(node.Name);
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
        List<Flow.NodeName> path,
        Dictionary<Flow.ModelRef, Flow.ModelDefinition> models,
        Dictionary<Flow.NodeName, int> order) => new()
        {
            Index = index,
            Name = node.Name,
            Path = new Flow.NodePath([.. path, node.Name]),
            Gate = node.Gate,
            Kind = node.EffectiveKind,
            Model = node.Model is { } modelReference ? models[modelReference] : null,
            Execution = execution,
            From = ResolveFrom(node.From, node, order),
            Outputs = node.Outputs,
            SystemPrompt = node.SystemPrompt,
            MaxRuns = execution.MaxRuns ?? ExecutableNode.DefaultMaxRuns,
        };

    /// <summary>容器输出端口声明投影成图上的绑定表：说明只属于定义与展示，图上只留成员引用。</summary>
    private static Dictionary<Flow.PortName, Flow.PortRef>? ToBindings(
        IReadOnlyDictionary<Flow.PortName, Flow.ContainerOutput>? outputs) =>
        outputs?.ToDictionary(entry => entry.Key, entry => entry.Value.Ref);

    /// <summary>把启动组编译成图依赖：来源序号经查表落定，类型取自目标输入端口的声明，组序号按组序给出。
    /// 绑定端口引用在 From 里不成立，编译前应由校验层拦截，这里给防御性错误。</summary>
    private static List<Dependency> ResolveFrom(
        IReadOnlyList<Flow.FromGroup> groups,
        Flow.NodeSpec node,
        Dictionary<Flow.NodeName, int> order)
    {
        List<Dependency> dependencies = [];
        int groupIndex = 0;
        foreach (Flow.FromGroup group in groups)
        {
            foreach (Flow.FromLink link in group.Links)
            {
                if (link.Ref.Source is not { } name)
                {
                    throw new InvalidOperationException(
                        $"From 连接 {link.Ref.Display} 是对绑定端口的引用，编译只接受已展开的流程树。");
                }

                Flow.InputPort? input = Flow.Ports.Find(node.Inputs, link.Input);
                Flow.PortType type = input?.Type ?? Flow.PortType.Data;
                dependencies.Add(new Dependency(order[name], link.Ref.Port, link.Input, type, groupIndex));
            }

            groupIndex++;
        }

        return dependencies;
    }
}
