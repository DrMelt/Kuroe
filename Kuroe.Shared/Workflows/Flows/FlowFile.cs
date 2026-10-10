namespace Kuroe.Shared.Workflows.Flows;

/// <summary>流程文件的内容：节点库与流程装配。文件读写由 FlowStore 负责，装配与校验由 FlowAssembler 负责。</summary>
public sealed record FlowFile(
    IReadOnlyList<NodeSpec> Nodes,
    IReadOnlyList<FlowDefinition> Flows);
