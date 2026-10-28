using Kuroe.Flow.Compiler.Shared;
using Kuroe.Flow.Shared.StaticGraph.Flow;
using Kuroe.Flow.Shared.StaticGraph.Graph;
using Kuroe.Flow.Shared.StaticGraph.Node;
using Kuroe.Flow.Shared.StaticGraph.PortModel;
using Kuroe.Node.Llm;
using Kuroe.Presentation;
using Kuroe.Workflows.Flows;

namespace Kuroe.Cli.Views;

/// <summary>流程列表与节点树的纯文本呈现。</summary>
internal static class PlainFlowView
{
    /// <summary>按文件顺序列出流程与规模，随后列出不可用流程。</summary>
    public static void List(
        CliOutput output,
        IReadOnlyList<FlowDefinition> flows,
        IReadOnlyList<FlowFailure> failures)
    {
        if (flows.Count == 0 && failures.Count == 0)
        {
            output.Hint("流程库为空，用 flow add <文件> 导入流程。");

            return;
        }

        if (flows.Count > 0)
        {
            output.Line("流程\t执行节点\t容器\t说明");
            foreach (FlowDefinition flow in flows)
            {
                (int executables, int containers) = Count(flow.RootNode);
                output.Line($"{flow.Name.Value}\t{executables}\t{containers}\t{flow.Description ?? string.Empty}");
            }
        }

        if (failures.Count > 0)
        {
            if (flows.Count > 0)
            {
                output.Line();
            }

            output.Line("不可用流程\t原因");
            foreach (FlowFailure failure in failures)
            {
                string reason = failure.Errors.Count > 0 ? failure.Errors[0].Description : "流程不可用。";
                output.Line($"{failure.Name.Value}\t{reason}");
            }
        }
    }

    /// <summary>打印流程字段与按层级缩进的节点树。</summary>
    public static void Show(CliOutput output, CompiledFlow compiled)
    {
        output.Line($"流程\t{compiled.Model.Name.Value}");
        output.Line($"执行节点\t{compiled.Graph.TotalExecutables}");
        output.Line($"容器\t{compiled.Graph.Containers.Count}");
        output.Line($"说明\t{compiled.Model.Description ?? "—"}");
        output.Line();
        Walk(output, compiled.Graph.ChildrenOf(null), 0, compiled.Model.HostStartInputs);
    }

    private static void Walk(CliOutput output, IReadOnlyList<GraphNode> nodes, int depth, IReadOnlyDictionary<PortName, string> hostInputs)
    {
        string indent = new(' ', depth * 2);
        foreach (GraphNode node in nodes)
        {
            output.Line($"{indent}{Describe(node, hostInputs)}");
            if (node is ContainerNode container)
            {
                Walk(output, container.Children, depth + 1, hostInputs);
            }
        }
    }

    private static string Describe(GraphNode node, IReadOnlyDictionary<PortName, string> hostInputs) => node switch
    {
        StartNode start => Start(start, hostInputs),
        ContainerNode container => Container(container),
        ExecutableNode executable => Executable(executable, hostInputs),
        _ => node.Label.Value,
    };

    private static string Start(StartNode start, IReadOnlyDictionary<PortName, string> hostInputs)
    {
        List<string> parts = ["Start", "开始"];
        IReadOnlyList<Port> ports = start.Outputs;
        if (ports.Count > 0)
        {
            parts.Add($"端口 {string.Join('、', ports.Select(port => port.StartLabel(hostInputs)))}");
        }

        return string.Join('\t', parts);
    }

    private static string Container(ContainerNode container)
    {
        List<string> parts = [container.Label.Value, "容器"];
        if (container.PortBindings is { Count: > 0 } bindings)
        {
            string[] dynamicPorts = [.. bindings.Keys
                .Where(port => container.StaticOutputs?.Contains(port) != true)
                .Select(port => port.Value)];
            if (dynamicPorts.Length > 0)
            {
                parts.Add($"端口 {string.Join('、', dynamicPorts)}");
            }
        }

        if (container.StaticOutputs is { Count: > 0 } statics)
        {
            parts.Add($"静态 {string.Join('、', statics.Select(port => port.Value))}");
        }

        return string.Join('\t', parts);
    }

    private static string Executable(ExecutableNode executable, IReadOnlyDictionary<PortName, string> hostInputs)
    {
        List<string> parts = [executable.Label.Value, executable.Kind.Value];
        if (executable.StaticValues is { } values && values.TryGetValue(LlmNodeTypes.ModelPort, out StaticValue model))
        {
            parts.Add($"模型 {model.StaticLabel(hostInputs: hostInputs)}");
        }

        IReadOnlyList<InputPort> inputs = [.. executable.Inputs.Where(port => !port.IsStatic)];
        if (inputs.Count > 0)
        {
            parts.Add($"输入 {string.Join('、', inputs.Select(port => port.Name.Value))}");
        }

        if (executable.Outputs.Count > 0)
        {
            parts.Add($"输出 {string.Join('、', executable.Outputs.Select(port => port.Name.Value))}");
        }

        if (executable.MaxExecutions != ExecutableNode.DefaultMaxExecutions)
        {
            parts.Add($"上限 {executable.MaxExecutions}");
        }

        string upstream = Upstream(executable.Incoming);
        if (upstream.Length > 0)
        {
            parts.Add($"上游 {upstream}");
        }

        return string.Join('\t', parts);
    }

    /// <summary>入边按启动组展开，组内以顿号连接，组间表示任一齐备。</summary>
    private static string Upstream(IReadOnlyList<FlowEdge> edges) =>
        string.Join(" 或 ", edges
            .GroupBy(edge => edge.Group)
            .OrderBy(group => group.Key)
            .Select(group => string.Join('、', group.Select(edge =>
                $"{edge.TargetInput.Value}←{edge.Source.Value}@{edge.SourcePort.Value}"))));

    /// <summary>流程里的执行节点与容器数量。</summary>
    private static (int Executables, int Containers) Count(NodeSpec root)
    {
        int executables = 0;
        int containers = 0;
        foreach (NodeSpec node in WalkSpec(root))
        {
            if (node.IsContainer)
            {
                containers++;
            }
            else if (node.Spec is not null)
            {
                executables++;
            }
        }

        return (executables, containers);
    }

    /// <summary>先根序遍历节点树。</summary>
    private static IEnumerable<NodeSpec> WalkSpec(NodeSpec node)
    {
        yield return node;
        if (node.Nodes is { Count: > 0 } children)
        {
            foreach (NodeSpec child in children)
            {
                foreach (NodeSpec descendant in WalkSpec(child))
                {
                    yield return descendant;
                }
            }
        }
    }
}