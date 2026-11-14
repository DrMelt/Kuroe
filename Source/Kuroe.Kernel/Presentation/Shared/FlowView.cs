namespace Kuroe.Presentation.Shared;

/// <summary>可构造的流程视图，供不同流程实现共用。</summary>
public sealed record FlowView(
    string Name,
    string Kind,
    string? Description,
    IReadOnlyList<FlowPortView> StartPorts,
    IReadOnlyList<FlowStepView> Steps,
    IReadOnlyList<FlowGroupView> Groups) : IFlowView
{
    /// <inheritdoc/>
    public int? IndexOf(string path)
    {
        for (int index = 0; index < Steps.Count; index++)
        {
            if (string.Equals(Steps[index].Path, path, StringComparison.Ordinal))
            {
                return index;
            }
        }

        return null;
    }
}