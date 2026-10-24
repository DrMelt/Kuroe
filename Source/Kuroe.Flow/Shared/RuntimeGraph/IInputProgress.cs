using Kuroe.Flow.Shared.StaticGraph.Graph;

namespace Kuroe.Flow.Shared.RuntimeGraph;

/// <summary>判定要读的目标自身状态：已消费版本、执行次数与重跑意图。</summary>
public interface IInputProgress
{
    /// <summary>某来源端口绑定已消费的版本，未消费过为零。</summary>
    long ConsumedRevision(ConsumptionKey key);

    /// <summary>登记一组绑定的当前版本，这一组视为已消费。</summary>
    void Consume(InputGroup group, IReadinessSources sources);

    /// <summary>已启动的执行数。</summary>
    int Executions { get; }

    /// <summary>返工作废后要求越过就绪条件重启一次。</summary>
    bool ForceRerun { get; }
}
