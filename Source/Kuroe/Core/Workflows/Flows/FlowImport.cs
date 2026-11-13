using Kuroe.Flow.Tasks;

namespace Kuroe.Workflows.Flows;

/// <summary>一次导入的结果：来源文件的绝对路径、导入的流程与节点定义及逐条说明。</summary>
public sealed record FlowImport(
    string Source,
    IReadOnlyList<FlowName> Flows,
    IReadOnlyList<string> Nodes,
    IReadOnlyList<string> Notes);
