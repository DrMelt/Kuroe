using Kuroe.Flow.Shared.StaticGraph;

namespace Kuroe.Flow.Shared.RuntimeGraph;

/// <summary>消费账：按来源节点路径记已消费的版本。
/// 执行节点的输入消费与容器的内容版本共用这一份记账。读写要求持有任务 Gate。</summary>
public sealed class ConsumptionLedger
{
    private readonly Dictionary<NodePath, long> _revisions = [];

    /// <summary>某来源的已消费版本，未消费过为零。</summary>
    public long Revision(NodePath source) => _revisions.GetValueOrDefault(source);

    /// <summary>登记方案内全部取值来源的当前版本。</summary>
    public void Record(NodeInputPlan plan, IReadinessSources sources)
    {
        foreach (NodePath source in plan.Groups.SelectMany(group => group.Bindings).Select(binding => binding.Source))
        {
            _revisions[source] = sources.Source(source).Revision;
        }
    }
}
