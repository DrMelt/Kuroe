using Kuroe.Shared.Workflows.Flows;

namespace Kuroe.Executions;

/// <summary>产出契约的展示名。</summary>
public static class NodeOutputLabels
{
    /// <summary>产出契约的展示名。</summary>
    public static string Label(this NodeOutput output) => output switch
    {
        NodeOutput.Plan => "规划",
        NodeOutput.Text => "实施",
        NodeOutput.Input => "输入",
        _ => output.ToString(),
    };
}
