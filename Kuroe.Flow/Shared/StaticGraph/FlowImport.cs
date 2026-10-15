namespace Kuroe.Flow.Shared.StaticGraph;

/// <summary>一次流程导入的结果：来源文件的绝对路径与逐条说明。</summary>
public sealed record FlowImport(string Source, IReadOnlyList<FlowName> Names, IReadOnlyList<string> Notes);
