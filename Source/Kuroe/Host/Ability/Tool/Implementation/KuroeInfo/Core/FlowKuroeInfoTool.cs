using Kuroe.Kernel.Control.Shared;
using Kuroe.Kernel.Tasks.Shared;
using System.Text;
using ErrorOr;
using Kuroe.Kernel.Presentation.Shared;

namespace Kuroe.Host.Ability.Tool.Implementation.KuroeInfo;

/// <summary>流程模板的状态查询工具。只读，不产生状态改动。</summary>
public sealed class FlowKuroeInfoTool : ITool
{
    private readonly IFlowTemplateCatalog _flows;

    /// <summary>本载体的函数声明。</summary>
    public IReadOnlyList<ToolFunction> Functions { get; }

    private static readonly ToolDefinition ListFlows = new(
        new ToolPath("KuroeInfo/ListFlows"),
        "列出流程模板：流程名、引擎、执行节点路径与说明。",
        []);

    private static readonly ToolDefinition GetFlow = new(
        new ToolPath("KuroeInfo/GetFlow"),
        "查看指定流程的展开节点表：模型、节点类型名与要求。",
        [
            new ToolParameter(new ToolName("flowName"), "要查看的流程名", Required: true),
            new ToolParameter(new ToolName("engine"), "流程实现，如 graph 或 maf；省略时列出全部实现", Required: false),
        ]);

    /// <summary>用流程模板目录建立载体。</summary>
    public FlowKuroeInfoTool(IFlowTemplateCatalog flows)
    {
        _flows = flows;
        Functions =
        [
            ListFlows.Bind(_ => List()),
            GetFlow.Bind(arguments => Show(arguments)),
        ];
    }

    private ErrorOr<string> List()
    {
        var text = new StringBuilder();
        foreach (FlowTemplateView flow in _flows.All())
        {
            string nodes = string.Join(" → ", flow.Steps.Select(step => step.Label));
            text.AppendLine($"{flow.Name} · {flow.Engine ?? "未知"} · {nodes} · {flow.Description ?? string.Empty}");
        }

        return text.ToString().TrimEnd();
    }

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

        var text = new StringBuilder();
        if (arguments.Text(new ToolName("engine")) is { Length: > 0 } engine)
        {
            ErrorOr<FlowTemplateView> found =
                _flows.Find(flowName.Value, new FlowImplementation(engine.Trim()));
            if (found.IsError)
            {
                return found.ErrorsOrEmptyList;
            }

            Append(text, found.Value);
        }
        else
        {
            IReadOnlyList<FlowTemplateView> all = _flows.FindAll(flowName.Value);
            if (all.Count == 0)
            {
                return FlowErrors.FlowNotFound(flowName.Value.Value);
            }

            foreach (FlowTemplateView flow in all)
            {
                if (text.Length > 0)
                {
                    text.AppendLine();
                }

                Append(text, flow);
            }
        }

        return text.ToString().TrimEnd();
    }

    private static void Append(StringBuilder text, FlowTemplateView flow)
    {
        text.AppendLine($"流程「{flow.Name}」· {flow.Engine ?? "未知"} · {flow.Description ?? string.Empty}");
        text.AppendLine($"开始 {string.Join("、", flow.StartPorts.Select(port => port.Label))}");
        Walk(text, flow, null, 0);
    }

    private static void Walk(StringBuilder text, FlowTemplateView flow, string? parent, int depth)
    {
        foreach (FlowGroupView group in flow.Groups.Where(group => group.ParentGroupPath == parent))
        {
            text.AppendLine($"  {Indent(depth)}{group.Label}（{group.TypeName ?? "容器"}节点）");
            AppendPorts(text, depth + 1, "传入端口", group.Inputs);
            AppendPorts(text, depth + 1, "输出端口", group.Outputs);
            Walk(text, flow, group.Path, depth + 1);
        }

        foreach (FlowStepView step in flow.Steps.Where(step => step.GroupPath == parent))
        {
            text.AppendLine($"  {Indent(depth)}{step.Label}（{step.Kind}节点）");
            string requirement = Requirement(step);
            if (requirement.Length > 0)
            {
                text.AppendLine($"    {Indent(depth)}{requirement}");
            }
        }
    }

    private static void AppendPorts(
        StringBuilder text,
        int depth,
        string label,
        IReadOnlyList<FlowPortView> ports)
    {
        if (ports.Count > 0)
        {
            text.AppendLine($"    {Indent(depth)}{label} {string.Join("、", ports.Select(port => port.Name))}");
        }
    }

    private static string Requirement(FlowStepView step)
    {
        List<string> parts = [];
        foreach (FlowAttributeView attribute in step.Attributes)
        {
            parts.Add($"{attribute.Name} {attribute.Value}");
        }

        if (step.Model is { Length: > 0 } model)
        {
            parts.Add($"模型 {model}");
        }

        IReadOnlyList<FlowPortView> inputs = [.. step.Inputs.Where(port => !port.Static)];
        if (inputs.Count > 0)
        {
            parts.Add($"输入 {string.Join("、", inputs.Select(port => port.Name))}");
        }

        if (step.SourceBindings.Count > 0)
        {
            parts.Add($"取自 {string.Join("；", step.SourceBindings)}");
        }

        if (step.Outputs.Count > 0)
        {
            parts.Add($"端口 {string.Join("、", step.Outputs.Select(port => port.Name))}");
        }

        return parts.Count == 0 ? string.Empty : $"要求：{string.Join("；", parts)}";
    }

    private static string Indent(int depth) => new(' ', depth * 2);
}