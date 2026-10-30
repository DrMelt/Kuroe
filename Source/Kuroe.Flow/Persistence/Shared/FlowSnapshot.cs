using ErrorOr;
using Kuroe.Flow.Shared.StaticGraph.Flow;

namespace Kuroe.Flow.Persistence.Shared;

/// <summary>任务提交时锁定的流程快照。</summary>
/// <param name="Name">流程名。</param>
/// <param name="Document">流程文档文本。</param>
public sealed record FlowSnapshot(FlowName Name, string Document);

/// <summary>按流程名捕获当前流程文档快照。由宿主实现。</summary>
public interface IFlowSnapshotSource
{
    /// <summary>捕获指定流程的快照。</summary>
    ErrorOr<FlowSnapshot> Capture(FlowName name);
}

/// <summary>把流程快照重新编译为可运行流程。由宿主实现。</summary>
public interface IFlowSnapshotCompiler
{
    /// <summary>编译快照，失败时返回流程错误。</summary>
    ErrorOr<CompiledFlow> Compile(FlowSnapshot snapshot);
}
