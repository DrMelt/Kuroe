using Kuroe.Shared.Workflows.Graph;

namespace Kuroe.Workflows.TaskExecution.Tasks;

/// <summary>一个执行节点对一条入边的消费账本：持有已消费版本账，负责就绪判定与消费登记。
/// 使用它的执行节点必须是整节点执行，入边只可能是 <see cref="EdgeFeed.Single"/> 或 <see cref="EdgeFeed.AllInstances"/>。
/// 读写要求持有任务 Gate。</summary>
internal sealed class FeedLedger(RuntimeNode source, EdgeFeed feed)
{
    private readonly RuntimeNode _source = source;
    private readonly EdgeFeed _feed = feed;
    private long _consumedRevision;
    private bool _consumedOnce;

    /// <summary>来源当前是否有放行的产出可消费。</summary>
    public bool Available => _source.Released;

    /// <summary>来源是否发布了本边尚未消费的新版本。</summary>
    public bool Fresh => _feed switch
    {
        EdgeFeed.Single => _source.Released && _source.Revision > _consumedRevision,
        _ => Available && !_consumedOnce,
    };

    /// <summary>启动即登记来源的当前版本，该份产出对本边即告消费完毕。</summary>
    public void Consume()
    {
        if (_feed == EdgeFeed.Single)
        {
            _consumedRevision = _source.Revision;
        }
        else
        {
            _consumedOnce = true;
        }
    }
}
