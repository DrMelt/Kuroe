using Kuroe.Shared.Executions.Runs;
using Kuroe.Shared.Workflows.Graph;

namespace Kuroe.Shared.Workflows.Tasks;

/// <summary>一个执行节点在某一刻的只读形状，含该执行节点上全部 run。</summary>
public sealed record ExecutableSnapshot(int Index, ExecutableNode Executable, IReadOnlyList<RunSnapshot> Runs);
