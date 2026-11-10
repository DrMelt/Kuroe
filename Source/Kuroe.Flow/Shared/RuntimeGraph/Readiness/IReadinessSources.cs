using Kuroe.Flow.Shared.StaticGraph.Graph;

namespace Kuroe.Flow.Shared.RuntimeGraph.Readiness;

/// <summary>判定读的来源侧：节点或流程开始端口的状态。</summary>
public interface IReadinessSources
{
    /// <summary>来源对应的状态。节点来源未实例化时给未放行的默认状态，开始来源由任务提供。</summary>
    ISourceState Source(BindingSource source);
}
