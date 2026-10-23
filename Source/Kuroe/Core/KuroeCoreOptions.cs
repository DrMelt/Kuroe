using Kuroe.Flow.InputChannel.Shared;
using Kuroe.Flow.Shared.StaticGraph.Node;

namespace Kuroe;

/// <summary>核心装配选项：宿主追加的节点类型与流程库失败策略。</summary>
public sealed record KuroeCoreOptions
{
    /// <summary>追加节点类型描述。核心固定注册 Container 与 Llm。</summary>
    public Action<INodeTypeDescriptionRegistry, IInputChannel>? RegisterNodeTypes { get; init; }

    /// <summary>流程库个别流程编译失败时是否继续装配。为 false 时按启动错误返回。</summary>
    public bool AllowFlowFailures { get; init; }
}