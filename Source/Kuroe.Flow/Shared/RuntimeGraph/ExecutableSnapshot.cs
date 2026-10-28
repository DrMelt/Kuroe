using Kuroe.Flow.Shared.Execution;
using Kuroe.Flow.Shared.StaticGraph.Graph;

namespace Kuroe.Flow.Shared.RuntimeGraph;

/// <summary>一个执行节点在某一刻的只读形状，含该执行节点的静态端口文本视图与全部执行。</summary>
public sealed record ExecutableSnapshot(
    ExecutableNode Executable,
    IPortTexts StaticTexts,
    IReadOnlyList<ExecutionSnapshot> Executions);
