using Kuroe.Flow.Compilation.Shared;

namespace Kuroe.Flow.Hosting.Flows.Files;

/// <summary>一次导入文件的内容：节点库文件或流程文件，只承载其中一类。</summary>
public abstract record FlowImportFile(string Source);

/// <summary>导入的节点库文件。</summary>
internal sealed record NodeLibraryImportFile(string Source, IReadOnlyList<NodeDefinitionDocument> Nodes)
    : FlowImportFile(Source);

/// <summary>导入的流程文件。</summary>
internal sealed record FlowDefinitionsImportFile(string Source, IReadOnlyList<FlowDocumentFlow> Flows)
    : FlowImportFile(Source);