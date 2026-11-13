using Kuroe.Flow.Presentation.Shared;
using Kuroe.Workflows.Flows;

namespace Kuroe.Cli.Views;

/// <summary>流程列表与节点树的纯文本呈现。</summary>
internal static class PlainFlowView
{
    /// <summary>按文件顺序列出流程与规模，随后列出不可用流程。</summary>
    public static void List(
        CliOutput output,
        IReadOnlyList<FlowTemplateView> flows,
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
            foreach (FlowTemplateView flow in flows)
            {
                output.Line($"{flow.Name}\t{flow.Steps.Count}\t{flow.Groups.Count}\t{flow.Description ?? string.Empty}");
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
    public static void Show(CliOutput output, FlowTemplateView flow)
    {
        output.Line($"流程\t{flow.Name}");
        output.Line($"执行节点\t{flow.Steps.Count}");
        output.Line($"容器\t{flow.Groups.Count}");
        output.Line($"说明\t{flow.Description ?? "—"}");
        output.Line();
        output.Line($"开始端口\t{string.Join('、', flow.StartPorts.Select(port => port.Label))}");
        Walk(output, flow, null, 0);
    }

    private static void Walk(CliOutput output, FlowTemplateView flow, string? parent, int depth)
    {
        string indent = new(' ', depth * 2);
        foreach (FlowGroupView group in flow.Groups.Where(group => group.ParentPath == parent))
        {
            output.Line($"{indent}{Container(group)}");
            Walk(output, flow, group.Path, depth + 1);
        }

        foreach (FlowStepView step in flow.Steps.Where(step => step.ParentPath == parent))
        {
            output.Line($"{indent}{Executable(step)}");
        }
    }

    private static string Container(FlowGroupView group)
    {
        List<string> parts = [group.Label, group.Kind ?? "容器"];
        if (group.Outputs.Count > 0)
        {
            parts.Add($"输出 {string.Join('、', group.Outputs.Select(port => port.Name))}");
        }

        if (group.MaxExecutions != int.MaxValue)
        {
            parts.Add($"上限 {group.MaxExecutions}");
        }

        return string.Join('\t', parts);
    }

    private static string Executable(FlowStepView step)
    {
        List<string> parts = [step.Label, step.Kind];
        if (step.Model is { Length: > 0 } model)
        {
            parts.Add($"模型 {model}");
        }

        IReadOnlyList<FlowPortView> inputs = [.. step.Inputs.Where(port => !port.Static)];
        if (inputs.Count > 0)
        {
            parts.Add($"输入 {string.Join('、', inputs.Select(port => port.Name))}");
        }

        if (step.Outputs.Count > 0)
        {
            parts.Add($"输出 {string.Join('、', step.Outputs.Select(port => port.Name))}");
        }

        if (step.MaxExecutions != FlowStepView.DefaultMaxExecutions)
        {
            parts.Add($"上限 {step.MaxExecutions}");
        }

        if (step.Upstream.Count > 0)
        {
            parts.Add($"上游 {string.Join(" 或 ", step.Upstream)}");
        }

        return string.Join('\t', parts);
    }
}