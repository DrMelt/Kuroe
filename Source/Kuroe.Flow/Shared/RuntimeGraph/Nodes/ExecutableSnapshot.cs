using Kuroe.Flow.Shared.RuntimeGraph.Execution;
using Kuroe.Flow.Shared.RuntimeGraph.Ports;
using Kuroe.Flow.Shared.StaticGraph.Graph;

namespace Kuroe.Flow.Shared.RuntimeGraph.Nodes;

/// <summary>一个执行节点在某一刻的只读形状，含该执行节点的静态端口文本视图与全部执行。</summary>
public sealed record ExecutableSnapshot(
    ExecutableNode Executable,
    IPortTexts StaticTexts,
    IReadOnlyList<ExecutionSnapshot> Executions);
