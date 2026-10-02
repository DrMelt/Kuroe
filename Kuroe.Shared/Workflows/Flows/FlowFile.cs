namespace Kuroe.Shared.Workflows.Flows;

/// <summary>流程文件的内容：节点库与流程装配。配置读写与校验由 FlowService 负责。</summary>
public sealed record FlowFile(
    IReadOnlyList<NodeSpec> Nodes,
    IReadOnlyList<FlowDefinition> Flows);