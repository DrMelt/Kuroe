using Kuroe.Shared.Workflows.Graph;

namespace Kuroe.Workflows.Tasks;

/// <summary>一个执行节点的输入侧：从全部入边收集提供器，另把 AnyOf 组按组聚合。
/// 启动条件 = From 组全部可用 ∧（无 AnyOf ∨ 任一组成员可用）∧ 任一相关提供器有新版本。
/// 读写要求持有任务 Gate。</summary>
internal sealed class NodeInput
{
    private readonly IReadOnlyList<FeedProvider> _required;
    private readonly IReadOnlyList<IReadOnlyList<FeedProvider>> _anyOf;

    private NodeInput(IReadOnlyList<FeedProvider> required, IReadOnlyList<IReadOnlyList<FeedProvider>> anyOf)
    {
        _required = required;
        _anyOf = anyOf;
    }

    /// <summary>按执行节点与图装配输入：每条入边一个提供器，AnyOf 组的源再按组聚合。</summary>
    public static NodeInput Build(WorkTask task, RuntimeExecutable node, Func<int, RuntimeNode> resolve)
    {
        var bySource = new Dictionary<int, FeedProvider>();
        foreach (FlowEdge edge in task.Graph.Incoming(node.Index))
        {
            bySource[edge.From] = new FeedProvider(resolve(edge.From), edge.Feed);
        }

        HashSet<int> anySources = [.. node.Executable.AnyOf.SelectMany(group => group)];
        IReadOnlyList<FeedProvider> required = [.. task.Graph.Incoming(node.Index)
            .Where(edge => !anySources.Contains(edge.From))
            .Select(edge => bySource[edge.From])];
        IReadOnlyList<IReadOnlyList<FeedProvider>> anyOf =
        [
            .. node.Executable.AnyOf.Select(group =>
                (IReadOnlyList<FeedProvider>)[.. group.Select(source => bySource[source])]),
        ];

        return new NodeInput(required, anyOf);
    }

    /// <summary>启动资格：From 组全部可用，且无 AnyOf 或任一组成员可用，且任一相关提供器有新版本。</summary>
    public bool Ready
    {
        get
        {
            bool requiredAvailable = _required.All(provider => provider.Available);
            bool anyOfAvailable = _anyOf.Count == 0 || _anyOf.Any(group => group.All(provider => provider.Available));

            if (!requiredAvailable || !anyOfAvailable)
            {
                return false;
            }

            return All().Any(provider => provider.Fresh);
        }
    }

    /// <summary>启动即登记全部来源的消费版本，防止同版本重复触发。</summary>
    public void Consume()
    {
        foreach (FeedProvider provider in All())
        {
            provider.Consume();
        }
    }

    private IEnumerable<FeedProvider> All() => _required.Concat(_anyOf.SelectMany(group => group));
}
