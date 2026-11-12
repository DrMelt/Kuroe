namespace Kuroe.Flow.Presentation;

/// <summary>流程模板的只读表现：开始端口、执行步骤与分组。宿主只读取它，不读取节点与图。</summary>
public sealed record FlowTemplateView(
    string Name,
    string? Description,
    IReadOnlyList<FlowStartPortView> StartPorts,
    IReadOnlyList<FlowStepView> Steps,
    IReadOnlyList<FlowGroupView> Groups);