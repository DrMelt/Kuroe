namespace Kuroe.Shared.Workflows.Flows;

/// <summary>流程文件的内容：节点库与流程装配。文件读写属于 FlowFiles 域，装配与校验属于 FlowAssembly 域。</summary>
public sealed record FlowFile(
    IReadOnlyList<NodeSpec> Nodes,
    IReadOnlyList<FlowDefinition> Flows);
