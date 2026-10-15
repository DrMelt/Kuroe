using Kuroe.Flow.Shared.StaticGraph;
namespace Kuroe.Flow.StaticGraph;

using Flow = Shared.StaticGraph;

/// <summary>把流程树编译成执行视图：开始登记为序号 0 的唯一来源，节点按先根序统一编号，容器登记成员与子容器，
/// From 启动组编译为带取值域与输入类型的边：输入类型非 Text 的按整份消费，其余按来源与目标模式推导。
/// 要求已通过 FlowRules 校验，模型选择引用与节点名字都可解析。传入的树必须是展开后的节点树。</summary>
internal static class FlowCompiler
{
    /// <summary>编译流程树，类型经注册表解析。</summary>
    public static NodeGraph Compile(FlowDefinition flow, NodeTypeRegistry types)
    {
        Dictionary<ModelRef, ModelDefinition> models = flow.Models.ToDictionary(model => model.Name);

        var nodes = new List<GraphNode>();
        var byName = new Dictionary<NodeName, int>();
        var containers = new Dictionary<int, ContainerBuilder>();
        var dependencies = new Dictionary<int, List<Dependency>>();

        // 先登记全部名字与序号，From 对后面的节点引用才可解析
        nodes.Add(NewStart(flow.StartOutputs));
        byName[StartNode.ReservedName] = 0;
        RegisterNames(flow.RootNode, byName, nodes.Count);
        SecondPass(flow.RootNode, models, nodes, byName, containers, dependencies, [], parent: null);

        // 用登记好的成员与子容器固化容器定义
        foreach (ContainerBuilder builder in containers.Values)
        {
            nodes[builder.Index] = new ContainerNode
            {
                Index = builder.Index,
                Name = builder.Name,
                Path = builder.Path,
                Members = builder.Members,
                SubContainers = builder.SubContainers,
                PortBindings = builder.PortBindings,
            };
        }

        List<FlowEdge> edges = [];
        foreach (ExecutableNode executable in nodes.OfType<ExecutableNode>())
        {
            foreach (Dependency dependency in dependencies[executable.Index])
            {
                // 上下文与信号输入按整份消费，其余按来源与目标模式推导，来源端口与取值域正交
                (EdgeKind kind, FeedScope scope, FeedRecurrence recurrence) = dependency.Type == Flow.PortType.Text
                    ? FeedRules.Of(nodes[dependency.Source], executable.Execution.Mode, dependency.SourcePort)
                    : (EdgeKind.Value, FeedScope.Whole, FeedRecurrence.Repeat);

                edges.Add(new FlowEdge(
                    dependency.Source,
                    executable.Index,
                    kind,
                    scope,
                    recurrence,
                    dependency.SourcePort,
                    dependency.TargetInput,
                    dependency.Type,
                    dependency.Group));
            }
        }

        return new NodeGraph(nodes, edges, types);
    }

    /// <summary>开始节点：序号 0，不属于任何容器，任务启动即已放行。</summary>
    private static StartNode NewStart(IReadOnlyList<Port> outputs) => new()
    {
        Index = 0,
        Name = StartNode.ReservedName,
        Path = new NodePath([StartNode.ReservedName]),
        Outputs = outputs,
    };

    /// <summary>容器定义的编译期登记，最终固化为 <see cref="ContainerNode"/>。</summary>
    private sealed record ContainerBuilder(
        int Index,
        NodeName Name,
        NodePath Path,
        List<int> Members,
        List<int> SubContainers,
        IReadOnlyDictionary<PortName, PortRef>? PortBindings);

    /// <summary>编译期暂存的一条依赖：来源序号、来源端口、目标输入端口、类型与组序号，编译成边后不再保留。</summary>
    private readonly record struct Dependency(
        int Source,
        PortName SourcePort,
        PortName TargetInput,
        PortType Type,
        int Group);

    /// <summary>先根序登记全部节点名与统一序号，第二遍用它把 From 名字转成序号。</summary>
    private static int RegisterNames(NodeSpec node, Dictionary<NodeName, int> order, int next)
    {
        order[node.Name] = next;
        next++;
        if (node.Nodes is { Count: > 0 } children)
        {
            foreach (NodeSpec child in children)
            {
                next = RegisterNames(child, order, next);
            }
        }

        return next;
    }

    private static void SecondPass(
        NodeSpec node,
        Dictionary<ModelRef, ModelDefinition> models,
        List<GraphNode> nodes,
        Dictionary<NodeName, int> byName,
        Dictionary<int, ContainerBuilder> containers,
        Dictionary<int, List<Dependency>> dependencies,
        List<NodeName> path,
        int? parent)
    {
        if (node.Execution is { } execution)
        {
            int index = nodes.Count;
            nodes.Add(NewExecutable(node, execution, index, path, models));
            dependencies[index] = ResolveFrom(node.From, node, byName);
            if (parent is { } memberContainer)
            {
                containers[memberContainer].Members.Add(index);
            }

            return;
        }

        int container = nodes.Count;
        NodePath containerPath = new([.. path, node.Name]);
        IReadOnlyDictionary<PortName, PortRef>? bindings = ToBindings(node.ContainerOutputs);
        containers[container] = new ContainerBuilder(container, node.Name, containerPath, [], [], bindings);
        nodes.Add(new ContainerNode
        {
            Index = container,
            Name = node.Name,
            Path = containerPath,
            Members = [],
            SubContainers = [],
            PortBindings = bindings,
        });
        if (parent is { } childContainer)
        {
            containers[childContainer].SubContainers.Add(container);
        }

        path.Add(node.Name);
        foreach (NodeSpec child in node.Nodes ?? [])
        {
            SecondPass(child, models, nodes, byName, containers, dependencies, path, container);
        }
        path.RemoveAt(path.Count - 1);
    }

    private static ExecutableNode NewExecutable(
        NodeSpec node,
        ExecutableSpec execution,
        int index,
        List<NodeName> path,
        Dictionary<ModelRef, ModelDefinition> models) => new()
        {
            Index = index,
            Name = node.Name,
            Path = new NodePath([.. path, node.Name]),
            Kind = node.EffectiveKind,
            Model = node.Model is { } modelReference ? models[modelReference] : null,
            Execution = execution,
            Outputs = node.Outputs,
            Parameters = node.Parameters,
            MaxRuns = execution.MaxRuns ?? ExecutableNode.DefaultMaxRuns,
        };

    /// <summary>容器输出端口声明投影成图上的绑定表：说明只属于定义与展示，图上只留成员引用。</summary>
    private static Dictionary<PortName, PortRef>? ToBindings(
        IReadOnlyDictionary<PortName, ContainerOutput>? outputs) =>
        outputs?.ToDictionary(entry => entry.Key, entry => entry.Value.Ref);

    /// <summary>把启动组编译成编译期依赖：来源序号经查表落定，类型取自目标输入端口的声明，组序号按组序给出。
    /// 绑定端口引用在 From 里不成立，编译前应由校验层拦截，这里给防御性错误。</summary>
    private static List<Dependency> ResolveFrom(
        IReadOnlyList<FromGroup> groups,
        NodeSpec node,
        Dictionary<NodeName, int> order)
    {
        List<Dependency> dependencies = [];
        int groupIndex = 0;
        foreach (FromGroup group in groups)
        {
            foreach (FromLink link in group.Links)
            {
                if (link.Ref.Source is not { } name)
                {
                    throw new InvalidOperationException(
                        $"From 连接 {link.Ref.Display} 是对绑定端口的引用，编译只接受已展开的流程树。");
                }

                InputPort? input = Flow.Ports.Find(node.Inputs, link.Input);
                PortType type = input?.Type ?? Flow.PortType.Text;
                dependencies.Add(new Dependency(order[name], link.Ref.Port, link.Input, type, groupIndex));
            }

            groupIndex++;
        }

        return dependencies;
    }
}
