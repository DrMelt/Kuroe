using Kuroe.Shared.Workflows.Flows;

namespace Kuroe.Executions;

/// <summary>产出契约的展示名。</summary>
public static class NodeOutputLabels
{
    /// <summary>产出契约的展示名。</summary>
    public static string Label(this NodeOutput output) => output switch
    {
        NodeOutput.Plan => "规划",
        NodeOutput.Plain => "实施",
        _ => output.ToString(),
    };
}