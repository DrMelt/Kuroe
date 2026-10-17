using Kuroe.Flow.Shared.StaticGraph;

namespace Kuroe.Flow.Shared.RuntimeGraph;

/// <summary>消费账：按来源节点路径记已消费的版本，并记下已发生的一次性消费。
/// 执行节点的输入消费与容器的内容版本共用这一份记账。读写要求持有任务 Gate。</summary>
public sealed class ConsumptionLedger
{
    private readonly Dictionary<NodePath, long> _revisions = [];
    private readonly HashSet<NodePath> _consumedOnce = [];

    /// <summary>某来源的已消费版本，未消费过为零。</summary>
    public long Revision(NodePath source) => _revisions.GetValueOrDefault(source);

    /// <summary>某来源的一次性消费是否已发生。</summary>
    public bool ConsumedOnce(NodePath source) => _consumedOnce.Contains(source);

    /// <summary>登记方案内全部取值来源的当前版本，一次性消费的来源同时记下已消费。</summary>
    public void Record(NodeInputPlan plan, IExecutionSources sources)
    {
        foreach (InputBinding binding in plan.Groups.SelectMany(group => group.Bindings))
        {
            if (binding.Kind == EdgeKind.Split)
            {
                continue;
            }

            _revisions[binding.Source] = sources.Source(binding.Source).Revision;
            if (binding.Recurrence == FeedRecurrence.Once)
            {
                _consumedOnce.Add(binding.Source);
            }
        }
    }
}
