namespace Kuroe.Presentation.Shared;

/// <summary>流程的只读表现视图。宿主只读取它，不读取节点与图运行期。</summary>
public interface IFlowView
{
    /// <summary>流程名。</summary>
    string Name { get; }

    /// <summary>流程种类。</summary>
    string Kind { get; }

    /// <summary>流程说明，未写时为空。</summary>
    string? Description { get; }

    /// <summary>开始端口。</summary>
    IReadOnlyList<FlowPortView> StartPorts { get; }

    /// <summary>步骤列表，按流程顺位排列。</summary>
    IReadOnlyList<FlowStepView> Steps { get; }

    /// <summary>分组或容器列表。</summary>
    IReadOnlyList<FlowGroupView> Groups { get; }

    /// <summary>按步骤路径取顺位，不存在时为空。</summary>
    int? IndexOf(string path);
}