using Kuroe.Kernel.Tasks.Shared;

namespace Kuroe.Kernel.Presentation.Shared;

/// <summary>流程模板的只读表现：开始端口、执行步骤与分组。</summary>
public sealed record FlowTemplateView(
    string Name,
    string? Description,
    IReadOnlyList<FlowStartPortView> StartPorts,
    IReadOnlyList<FlowStepView> Steps,
    IReadOnlyList<FlowGroupView> Groups)
{
    /// <summary>处理该流程的流程实现显示名，未标注时为空。</summary>
    public string? Engine { get; init; }

    /// <summary>处理该流程的流程实现标识，未标注时为空。</summary>
    public FlowImplementation? Implementation { get; init; }

    /// <summary>列表展示文本：流程名，标注引擎时附上引擎。</summary>
    public string Label => Engine is { Length: > 0 } engine ? $"{Name} · {engine}" : Name;
}