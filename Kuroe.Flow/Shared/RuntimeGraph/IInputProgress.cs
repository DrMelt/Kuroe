using Kuroe.Flow.Shared.StaticGraph;

namespace Kuroe.Flow.Shared.RuntimeGraph;

/// <summary>判定要读的目标自身状态：实例集、消费账、产出记录与执行次数。</summary>
public interface IInputProgress
{
    /// <summary>已固定的实例集，尚未展开为空。</summary>
    IReadOnlyList<int>? AdoptedItems { get; }

    /// <summary>某来源的已消费版本，未消费过为零。</summary>
    long ConsumedRevision(NodePath source);

    /// <summary>某来源的一次性消费是否已发生。</summary>
    bool ConsumedOnce(NodePath source);

    /// <summary>整节点或某实例是否已有发表产出。</summary>
    bool Produced(int? item);

    /// <summary>整节点或某实例已启动的 run 数。</summary>
    int Runs(int? item);

    /// <summary>返工作废后要求越过就绪条件重启一次。</summary>
    bool ForceRerun { get; }
}
