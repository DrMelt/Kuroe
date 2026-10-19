using Kuroe.Flow.Shared.StaticGraph;

namespace Kuroe.Flow.Shared.RuntimeGraph;

/// <summary>判定要读的目标自身状态：消费账、执行次数与重跑意图。</summary>
public interface IInputProgress
{
    /// <summary>某来源的已消费版本，未消费过为零。</summary>
    long ConsumedRevision(NodePath source);

    /// <summary>已启动的执行数。</summary>
    int Executions { get; }

    /// <summary>返工作废后要求越过就绪条件重启一次。</summary>
    bool ForceRerun { get; }
}
