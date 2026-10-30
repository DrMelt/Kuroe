using Kuroe.Flow.Persistence.Shared;
using Kuroe.Flow.Shared.RuntimeGraph.Readiness;
using Kuroe.Flow.Shared.StaticGraph.Graph;

namespace Kuroe.Flow.Runtime.Graph;

/// <summary>已消费版本记录：按来源节点与来源端口记录已消费到的版本。执行节点与容器各持一份，
/// 输入消费与内容版本共用这套登记规则。读写要求持有任务 Gate。</summary>
public sealed class ConsumedRevisionStore : IConsumedRevisionStore
{
    private readonly Dictionary<ConsumptionKey, long> _revisions = [];

    /// <summary>某来源端口绑定已消费的版本，未消费过为零。</summary>
    public long Revision(ConsumptionKey key) => _revisions.GetValueOrDefault(key);

    /// <summary>登记一组绑定内全部来源端口的当前版本。</summary>
    public void Record(InputGroup group, IReadinessSources sources)
    {
        foreach (InputBinding binding in group.Bindings)
        {
            _revisions[binding.Key] = sources.Source(binding.Source).Revision;
        }
    }

    /// <summary>登记方案内全部启动组的当前版本。</summary>
    public void Record(NodeInputPlan plan, IReadinessSources sources)
    {
        foreach (InputGroup group in plan.Groups)
        {
            Record(group, sources);
        }
    }

    /// <summary>已消费版本的持久形态，按登记顺序。</summary>
    public IReadOnlyList<ConsumedRevisionCheckpoint> CaptureCheckpoint() =>
        [.. _revisions.Select(pair => new ConsumedRevisionCheckpoint(pair.Key.Source, pair.Key.Port, pair.Value))];

    /// <summary>从检查点回填已消费版本，覆盖现有登记。</summary>
    public void RestoreCheckpoint(IReadOnlyList<ConsumedRevisionCheckpoint> consumed)
    {
        _revisions.Clear();
        foreach (ConsumedRevisionCheckpoint entry in consumed)
        {
            _revisions[new ConsumptionKey(entry.Source, entry.Port)] = entry.Revision;
        }
    }
}
