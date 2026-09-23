namespace Kuroe.Workflows;

/// <summary>一次流程导入的结果：来源文件的绝对路径与逐条说明。</summary>
public sealed record WorkflowImport(string Source, IReadOnlyList<string> Names, IReadOnlyList<string> Notes);
