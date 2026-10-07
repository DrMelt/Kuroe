namespace Kuroe.Shared.Workflows.Flows;

/// <summary>任务流程模板：命名的模型选择与根节点，根节点通常是承载流程全部内容的容器。配置读写由 FlowService 负责。</summary>
public sealed record FlowDefinition(
    FlowName Name,
    string? Description,
    IReadOnlyList<ModelDefinition> Models,
    NodeSpec RootNode);
