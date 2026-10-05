using System.Text;
using ErrorOr;
using Kuroe.Executions;
using Kuroe.Shared.Executions.Tools;
using Kuroe.Shared.Workflows.Flows;
using Kuroe.Workflows.Flows;

namespace Kuroe.Tools.KuroeTools;

/// <summary>流程模板的信息查询工具。只读，不产生状态改动。</summary>
internal sealed class FlowInfoTool : ITool
{
    private readonly FlowService _flows;

    /// <summary>本载体的函数声明。</summary>
    public IReadOnlyList<ToolFunction> Functions { get; }

    public FlowInfoTool(FlowService flows)
    {
        _flows = flows;
        Functions =
        [
            new ToolFunction(new ToolName("ListFlows"),
                "列出流程模板：流程名、执行节点路径与说明，标出默认流程。",
                [], _ => List(), new ToolPath("info/ListFlows")),
            new ToolFunction(new ToolName("GetFlow"),
                "查看指定流程的展开节点表：模型、产出契约、展开方式与放行规则。",
                [new ToolParameter(new ToolName("flowName"), "要查看的流程名", Required: true)],
                arguments => Show(arguments), new ToolPath("info/GetFlow")),
        ];
    }

    /// <summary>流程列表。</summary>
    private ErrorOr<string> List()
    {
        var text = new StringBuilder();
        foreach (FlowDefinition flow in _flows.All())
        {
            string standard = flow.Name == _flows.DefaultName ? "默认 " : string.Empty;
            text.AppendLine($"{standard}{flow.Name} · {string.Join(" → ", Flatten(flow.RootNode))}"
                + $" · {flow.Description ?? string.Empty}");
        }

        text.Append($"默认流程：{_flows.DefaultName}");

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

        ErrorOr<FlowDefinition> found = _flows.Find(name);
        if (found.IsError)
        {
            return found.ErrorsOrEmptyList;
        }

        var text = new StringBuilder();
        text.AppendLine($"流程「{found.Value.Name}」· {found.Value.Description ?? string.Empty}");

        foreach ((NodeSpec node, int depth) in Walk(found.Value.RootNode))
        {
            if (node.Execution is { } executable)
            {
                text.AppendLine($"  {Indent(depth)}{node.Name.Value}（执行节点）");
                text.AppendLine($"    模型 {NodeModel(node)}"
                    + $" · 契约 {executable.Output.Label()}"
                    + $" · 展开 {InfoLabels.Of(executable.Mode)}"
                    + $" · 放行 {InfoLabels.Of(node.Gate)}");

                string requirement = Requirement(node, executable);
                if (requirement.Length > 0)
                {
                    text.AppendLine($"    {requirement}");
                }
            }
            else if (node.Nodes is { Count: > 0 })
            {
                text.AppendLine($"  {Indent(depth)}{node.Name.Value}（容器）· 放行 {InfoLabels.Of(node.Gate)}");
            }
        }

        return text.ToString().TrimEnd();
    }

    /// <summary>展开节点上的模型引用，未绑定为空。</summary>
    private static string NodeModel(NodeSpec node) => node.Model is { } model ? model.Value : "未绑定";

    /// <summary>节点要求：分支、工具、上游、可选启动组与拆分约束。</summary>
    private static string Requirement(NodeSpec node, ExecutableSpec executable)
    {
        List<string> parts = [];
        if (executable.Branch is { } branch)
        {
            parts.Add($"分支 {branch}");
        }

        if (executable.Tools.Count > 0)
        {
            parts.Add($"工具 {string.Join("、", executable.Tools)}");
        }

        if (node.From.Count > 0)
        {
            parts.Add($"取自 {string.Join("、", node.From)}");
        }

        if (node.AnyOf.Count > 0)
        {
            parts.Add($"任选一组满足 {string.Join(" 或 ", node.AnyOf.Select(group => string.Join("、", group)))}");
        }

        if (executable.Split is { } split)
        {
            if (split.Items is { Count: > 0 } items)
            {
                parts.Add($"固定 {items.Count} 条");
            }

            if (split.ExtrasMax is { } limit)
            {
                parts.Add($"补充至多 {limit} 条");
            }

            if (split.Acceptance is { Length: > 0 })
            {
                parts.Add("统一验收");
            }
        }

        return parts.Count == 0 ? string.Empty : $"要求：{string.Join("；", parts)}";
    }

    /// <summary>递归收集流程里的全部执行节点名。</summary>
    private static IEnumerable<NodeName> Flatten(NodeSpec node)
    {
        if (node.Nodes is { Count: > 0 } children)
        {
            foreach (NodeSpec child in children)
            {
                foreach (NodeName descendant in Flatten(child))
                {
                    yield return descendant;
                }
            }
        }
        else
        {
            yield return node.Name;
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
