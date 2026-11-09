using ErrorOr;
using Kuroe.Flow.Shared.StaticGraph.Graph;

namespace Kuroe.Flow.Shared.RuntimeGraph.Execution;

/// <summary>按节点类型选择恢复能力。节点类型自带恢复器时用类型实现，其余取默认执行器。</summary>
public interface IExecutionRestorerSource
{
    /// <summary>校验恢复数据并装配一次恢复。</summary>
    ErrorOr<PreparedRestore> Prepare(
        ExecutableNode node,
        RestorePreparationContext context);

    /// <summary>按恢复结果建立运行器。</summary>
    IExecutionRunner CreateRestored(
        ExecutableNode node,
        ExecutionId execution,
        PreparedRestore restore);
}