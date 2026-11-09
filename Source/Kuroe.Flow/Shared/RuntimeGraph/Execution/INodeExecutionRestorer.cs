using ErrorOr;
using Kuroe.Flow.Shared.StaticGraph.Graph;

namespace Kuroe.Flow.Shared.RuntimeGraph.Execution;

/// <summary>节点类型的恢复能力。能否恢复、状态校验与恢复运行器的建立都由节点类型决定。</summary>
public interface INodeExecutionRestorer
{
    /// <summary>校验恢复数据并装配一次恢复。不能恢复时返回错误。</summary>
    ErrorOr<PreparedRestore> Prepare(RestorePreparationContext context);

    /// <summary>按恢复结果建立运行器。</summary>
    IExecutionRunner CreateRestored(
        ExecutableNode node,
        ExecutionId execution,
        PreparedRestore restore);
}