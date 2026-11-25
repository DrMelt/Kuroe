using Kuroe.FlowDescription.Description.Shared;

namespace Kuroe.FlowDescription.Json.Shared;

/// <summary>导入文件的类别与内容，只承载其中一类。</summary>
public sealed record FlowImportContent(
    FlowImportKind Kind,
    IReadOnlyList<NodeDefinitionDescription> Nodes,
    IReadOnlyList<FlowEntry> Flows);