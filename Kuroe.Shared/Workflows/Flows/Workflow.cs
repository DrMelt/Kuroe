namespace Kuroe.Shared.Workflows.Flows;

/// <summary>任务流程模板：命名的模型选择与由节点组成的流程图。配置读写由 WorkflowService 负责。</summary>
public sealed record Workflow(
    string Name,
    string? Description,
    IReadOnlyList<ModelDefinition> Models,
    IReadOnlyList<NodeSpec> Nodes);
