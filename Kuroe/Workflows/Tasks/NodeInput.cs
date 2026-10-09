using Kuroe.Shared.Workflows.Graph;

namespace Kuroe.Workflows.Tasks;

/// <summary>一个执行节点的输入侧：从全部入边收集消费账本，可选启动组按组名聚合、信号边单独成组。
/// 启动条件 = 数据组全部可用 ∧（无可选组 ∨ 任一组成员可用）∧ 触发源全部可用
/// ∧（有信号边时任一触发源有新版本，否则任一相关账本有新版本）。
/// Context 条目归于数据组，与数据条目同判齐备与版本驱动。
/// 读写要求持有任务 Gate。</summary>
internal sealed class NodeInput
{
    private readonly IReadOnlyList<FeedLedger> _required;
    private readonly IReadOnlyList<IReadOnlyList<FeedLedger>> _orGroups;
    private readonly IReadOnlyList<FeedLedger> _triggers;

    private NodeInput(
        IReadOnlyList<FeedLedger> required,
        IReadOnlyList<IReadOnlyList<FeedLedger>> orGroups,
        IReadOnlyList<FeedLedger> triggers)
    {
        _required = required;
        _orGroups = orGroups;
        _triggers = triggers;
    }

    /// <summary>按执行节点与图装配输入：每条入边一个独立账本，可选组按边上的组名聚合、信号边单独成组。
    /// 同源多条边各自记账，不互相覆盖。</summary>
    public static NodeInput Build(WorkTask task, RuntimeExecutable node, Func<int, RuntimeNode> resolve)
    {
        (FlowEdge Edge, FeedLedger Ledger)[] entries = [.. task.Graph.Incoming(node.Index)
            .Select(edge => (edge, new FeedLedger(resolve(edge.From), edge.Feed)))];

        IReadOnlyList<FeedLedger> required = [.. entries
            .Where(entry => entry.Edge.Role != EdgeRole.Trigger && entry.Edge.Or is null)
            .Select(entry => entry.Ledger)];
        IReadOnlyList<FeedLedger> triggers = [.. entries
            .Where(entry => entry.Edge.Role == EdgeRole.Trigger)
            .Select(entry => entry.Ledger)];
        IReadOnlyList<IReadOnlyList<FeedLedger>> orGroups =
        [
            .. entries.Where(entry => entry.Edge.Or is not null)
                .GroupBy(entry => entry.Edge.Or)
                .Select(group => (IReadOnlyList<FeedLedger>)[.. group.Select(entry => entry.Ledger)]),
        ];

        return new NodeInput(required, orGroups, triggers);
    }

    /// <summary>启动资格：数据组与信号源全部可用、无可选组或任一组成员可用，
    /// 且有信号条目时由任一信号源的新版本驱动，否则任一相关账本的新版本驱动。</summary>
    public bool Ready
    {
        get
        {
            bool requiredAvailable = _required.All(provider => provider.Available);
            bool triggersAvailable = _triggers.All(provider => provider.Available);
            bool orAvailable = _orGroups.Count == 0 || _orGroups.Any(group => group.All(provider => provider.Available));

            if (!requiredAvailable || !triggersAvailable || !orAvailable)
            {
                return false;
            }

            return _triggers.Count > 0 ? _triggers.Any(provider => provider.Fresh) : All().Any(provider => provider.Fresh);
        }
    }

    /// <summary>启动即登记全部来源的消费版本，防止同版本重复触发。</summary>
    public void Consume()
    {
        foreach (FeedLedger ledger in All())
        {
            ledger.Consume();
        }
    }

    private IEnumerable<FeedLedger> All() =>
        _required.Concat(_orGroups.SelectMany(group => group)).Concat(_triggers);
}
