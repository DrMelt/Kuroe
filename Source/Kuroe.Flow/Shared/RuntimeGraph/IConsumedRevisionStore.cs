using Kuroe.Flow.Shared.StaticGraph.Graph;

namespace Kuroe.Flow.Shared.RuntimeGraph;

/// <summary>已消费版本记录的读写契约：按来源节点与来源端口记录已消费到的版本。
/// 执行节点的输入消费与容器的内容版本共用这套登记规则。读写要求持有任务 Gate。</summary>
public interface IConsumedRevisionStore
{
    /// <summary>某来源端口绑定已消费的版本，未消费过为零。</summary>
    long Revision(ConsumptionKey key);

    /// <summary>登记一组绑定的当前版本。</summary>
    void Record(InputGroup group, IReadinessSources sources);

    /// <summary>登记方案内全部启动组的当前版本。</summary>
    void Record(NodeInputPlan plan, IReadinessSources sources);
}