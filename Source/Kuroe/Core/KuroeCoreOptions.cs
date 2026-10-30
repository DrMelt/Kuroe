using Kuroe.Flow.Compilation.Shared.NodeTypes;
using Kuroe.Flow.InputChannel.Shared;

namespace Kuroe;

/// <summary>核心装配选项：宿主追加的节点类型、流程库失败策略与任务持久化开关。</summary>
public sealed record KuroeCoreOptions
{
    /// <summary>追加节点类型描述。核心固定注册 Container、Llm、FileRead、FileWrite 与 Command。</summary>
    public Action<INodeTypeDescriptionRegistry, IInputChannel>? RegisterNodeTypes { get; init; }

    /// <summary>流程库个别流程编译失败时是否继续装配。为 false 时按启动错误返回。</summary>
    public bool AllowFlowFailures { get; init; }

    /// <summary>任务是否写检查点并由宿主加载。只跑一次、不恢复任务的宿主可关闭。</summary>
    public bool PersistTasks { get; init; } = true;
}