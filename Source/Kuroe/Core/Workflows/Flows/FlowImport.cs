using Kuroe.Flow.Shared.Model;
using Kuroe.Flow.Shared.StaticGraph.Flow;

namespace Kuroe.Workflows.Flows;

/// <summary>一次导入的结果：来源文件的绝对路径、导入的流程与节点定义及逐条说明。</summary>
public sealed record FlowImport(
    string Source,
    IReadOnlyList<FlowName> Flows,
    IReadOnlyList<NodeName> Nodes,
    IReadOnlyList<string> Notes);
