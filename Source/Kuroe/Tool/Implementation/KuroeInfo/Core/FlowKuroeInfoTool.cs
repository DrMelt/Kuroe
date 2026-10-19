using System.Text;
using ErrorOr;
using Kuroe.Flow.Shared.StaticGraph;
using Kuroe.Node.Llm;
using Kuroe.Presentation;

namespace Kuroe.Tool.KuroeInfo;

/// <summary>流程模板的状态查询工具。只读，不产生状态改动。</summary>
public sealed class FlowKuroeInfoTool : ITool
{
    private readonly IFlowCatalog _flows;

    /// <summary>本载体的函数声明。</summary>
    public IReadOnlyList<ToolFunction> Functions { get; }

    /// <summary>列出流程模板的函数定义。</summary>
    private static readonly ToolDefinition ListFlows = new(
        new ToolPath("KuroeInfo/ListFlows"),
        "列出流程模板：流程名、执行节点路径与说明。",
        []);

    /// <summary>查看指定流程的函数定义。</summary>
    private static readonly ToolDefinition GetFlow = new(
        new ToolPath("KuroeInfo/GetFlow"),
        "查看指定流程的展开节点表：模型、节点类型名与要求。",
        [new ToolParameter(new ToolName("flowName"), "要查看的流程名", Required: true)]);

    /// <summary>用流程模板查询建立载体。</summary>
    public FlowKuroeInfoTool(IFlowCatalog flows)
    {
        _flows = flows;
        Functions =
        [
            ListFlows.Bind(_ => List()),
            GetFlow.Bind(arguments => Show(arguments)),
        ];
    }

    /// <summary>流程列表。</summary>
    private ErrorOr<string> List()
    {
        var text = new StringBuilder();
        foreach (FlowDefinition flow in _flows.All())
        {
            text.AppendLine($"{flow.Name} · {string.Join(" → ", Flatten(flow.RootNode))}"
                + $" · {flow.Description ?? string.Empty}");
        }

        return text.ToString().TrimEnd();
    }

    /// <summary>按流程名查看展开节点表，流程不存在时返回拒绝错误。</summary>
    private ErrorOr<string> Show(ToolArguments arguments)
    {
        string? name = arguments.Text(new ToolName("flowName"));
        if (string.IsNullOrWhiteSpace(name))
        {
            return ToolErrors.Argument("缺少流程名。");
        }

        ErrorOr<FlowName> flowName = FlowName.Create(name);
        if (flowName.IsError)
        {
            return flowName.ErrorsOrEmptyList;
        }

        ErrorOr<FlowDefinition> found = _flows.Find(flowName.Value);
        if (found.IsError)
        {
            return found.ErrorsOrEmptyList;
        }

        var text = new StringBuilder();
        text.AppendLine($"流程「{found.Value.Name}」· {found.Value.Description ?? string.Empty}");
        text.AppendLine($"开始 {string.Join("、", found.Value.StartOutputs.Select(StartPort))}");

        foreach ((NodeSpec node, int depth) in Walk(found.Value.RootNode))
        {
            if (node.Spec is not null)
            {
                NodeKind kind = node.EffectiveKind;
                text.AppendLine($"  {Indent(depth)}{node.Label.Value}（{kind.Value}节点）");

                List<string> attributes = [];
                if (node.Type is { Shape.DispatchesExecutions: true })
                {
                    attributes.Add($"模型 {NodeModel(node)}");
                }

                if (attributes.Count > 0)
                {
                    text.AppendLine($"    {string.Join(" · ", attributes)}");
                }

                string requirement = Requirement(node);
                if (requirement.Length > 0)
                {
                    text.AppendLine($"    {requirement}");
                }
            }
            else if (node.Nodes is { Count: > 0 })
            {
                text.AppendLine($"  {Indent(depth)}{node.Label.Value}（{node.EffectiveKind.Value}节点）");
                if (node.Inputs.Count > 0)
                {
                    text.AppendLine($"    传入端口 {string.Join("、", node.Inputs.Select(port => port.Label()))}");
                }

                if (node.ContainerOutputs is { Count: > 0 } outs)
                {
                    text.AppendLine($"    输出端口 {string.Join("、", outs.Select(entry => DescribeOutput(entry.Key, entry.Value)))}");
                }
            }
        }

        return text.ToString().TrimEnd();
    }

    /// <summary>展开节点上的模型引用，未绑定为空。</summary>
    private static string NodeModel(NodeSpec node) => node.Model is { } model ? model.Value : "未绑定";

    /// <summary>开始端口的展示：带固定值的端口写出值。</summary>
    private static string StartPort(Port port) =>
        port.Value is { Length: > 0 } value ? $"{port.Name}＝{value}" : port.Name.Value;

    /// <summary>节点要求：工具、输入端口、上游启动组、提问、输出端口与系统指令。</summary>
    private static string Requirement(NodeSpec node)
    {
        LlmParameters? parameters = NodeParameterView.Llm(node.Type);
        List<string> parts = [];
        if (parameters?.Tools is { Count: > 0 } tools)
        {
            parts.Add($"工具 {string.Join("、", tools)}");
        }

        if (node.Inputs.Count > 0)
        {
            parts.Add($"输入 {string.Join("、", node.Inputs.Select(port => port.Label()))}");
        }

        if (node.From.Count > 0)
        {
            parts.Add($"取自 {string.Join("；", node.From.Select(group => group.Label()))}");
        }

        if (NodeParameterView.Question(node.Type) is { Length: > 0 } question)
        {
            parts.Add($"提问 {question}");
        }

        if (node.Outputs.Count > 0)
        {
            parts.Add($"端口 {string.Join("、", node.Outputs.Select(port => port.Label()))}");
        }

        if (parameters?.SystemPrompt is { Count: > 0 } blocks)
        {
            parts.Add($"系统指令 {blocks.Count} 块");
        }

        return parts.Count == 0 ? string.Empty : $"要求：{string.Join("；", parts)}";
    }

    /// <summary>容器输出端口的展示：写转发目标，说明存在时写在末尾。</summary>
    private static string DescribeOutput(PortName port, ContainerOutput output) =>
        output.Description is { Length: > 0 } description
            ? $"{port.Value} ← {output.Ref.Display}：{description}"
            : $"{port.Value} ← {output.Ref.Display}";

    /// <summary>递归收集流程里的全部执行节点标签。</summary>
    private static IEnumerable<NodeLabel> Flatten(NodeSpec node)
    {
        if (node.Nodes is { Count: > 0 } children)
        {
            foreach (NodeSpec child in children)
            {
                foreach (NodeLabel descendant in Flatten(child))
                {
                    yield return descendant;
                }
            }
        }
        else
        {
            yield return node.Label;
        }
    }

    /// <summary>先根序遍历节点树，附带层级深度供缩进。</summary>
    private static IEnumerable<(NodeSpec Node, int Depth)> Walk(NodeSpec node, int depth = 0)
    {
        yield return (node, depth);
        if (node.Nodes is { Count: > 0 } children)
        {
            foreach (NodeSpec child in children)
            {
                foreach ((NodeSpec descendant, int childDepth) in Walk(child, depth + 1))
                {
                    yield return (descendant, childDepth);
                }
            }
        }
    }

    private static string Indent(int depth) => new(' ', depth * 2);
}
