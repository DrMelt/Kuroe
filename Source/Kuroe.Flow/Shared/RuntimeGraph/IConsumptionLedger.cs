using Kuroe.Flow.Shared.StaticGraph.Graph;

namespace Kuroe.Flow.Shared.RuntimeGraph;

/// <summary>消费账：按来源节点路径记已消费的版本。执行节点的输入消费与容器的内容版本共用这一份记账。
/// 读写要求持有任务 Gate。</summary>
public interface IConsumptionLedger
{
    /// <summary>某来源的已消费版本，未消费过为零。</summary>
    long Revision(NodePath source);

    /// <summary>登记方案内全部取值来源的当前版本。</summary>
    void Record(NodeInputPlan plan, IReadinessSources sources);
}