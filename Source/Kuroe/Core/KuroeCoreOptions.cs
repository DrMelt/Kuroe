using Kuroe.Flow.Compilation.Shared.NodeTypes;
using Kuroe.Flow.InputChannel.Shared;
using Kuroe.Process;

namespace Kuroe;

/// <summary>核心装配选项：宿主追加的节点类型、流程库失败策略、任务持久化开关与进程执行边界。</summary>
public sealed record KuroeCoreOptions
{
    /// <summary>追加节点类型描述。核心固定注册 Container、Llm、FileRead、FileWrite 与 Command。</summary>
    public Action<INodeTypeDescriptionRegistry, IInputChannel>? RegisterNodeTypes { get; init; }

    /// <summary>流程库个别流程编译失败时是否继续装配。为 false 时按启动错误返回。</summary>
    public bool AllowFlowFailures { get; init; }

    /// <summary>任务是否写存档并占用租约。开启后宿主可调用恢复服务加载存档。</summary>
    public bool PersistTasks { get; init; } = true;

    /// <summary>命令节点的进程执行边界，未给出时使用系统进程。命令工具使用自己的进程边界。</summary>
    public IProcessRunner? CommandNodeProcessRunner { get; init; }
}