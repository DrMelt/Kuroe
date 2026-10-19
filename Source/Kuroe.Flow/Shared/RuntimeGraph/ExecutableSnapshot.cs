using Kuroe.Flow.Shared.Execution;
using Kuroe.Flow.Shared.StaticGraph;

namespace Kuroe.Flow.Shared.RuntimeGraph;

/// <summary>一个执行节点在某一刻的只读形状，含该执行节点上全部执行。</summary>
public sealed record ExecutableSnapshot(ExecutableNode Executable, IReadOnlyList<ExecutionSnapshot> Executions);
