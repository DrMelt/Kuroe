using Kuroe.Flows.Shared.Execution.Runs;
using Kuroe.Flows.Shared.StaticGraph;

namespace Kuroe.Flows.Shared.RuntimeGraph;

/// <summary>一个执行节点在某一刻的只读形状，含该执行节点上全部 run。</summary>
public sealed record ExecutableSnapshot(int Index, ExecutableNode Executable, IReadOnlyList<RunSnapshot> Runs);
