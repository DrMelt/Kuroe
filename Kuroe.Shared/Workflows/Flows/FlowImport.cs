namespace Kuroe.Shared.Workflows.Flows;

/// <summary>一次流程导入的结果：来源文件的绝对路径与逐条说明。</summary>
public sealed record FlowImport(string Source, IReadOnlyList<string> Names, IReadOnlyList<string> Notes);