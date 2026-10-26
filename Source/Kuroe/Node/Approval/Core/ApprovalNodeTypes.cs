using Kuroe.Flow.InputChannel.Shared;
using Kuroe.Flow.Shared.StaticGraph.Node;
using Kuroe.Flow.Shared.StaticGraph.PortModel;

namespace Kuroe.Node.Approval;

/// <summary>审批节点类型的对外入口：类型名、静态形状与实例建立。审批节点不启动执行，批准后按同名输出端口放出停驻时的取值。</summary>
public static class ApprovalNodeTypes
{
    /// <summary>类型名，与流程文件里 Kind 的取值对应。</summary>
    public static NodeKind Name { get; } = new("Approval");

    /// <summary>审批节点类型的静态形状：对外端口由输入端口派生，Signal 只作驱动，Text 与 Signal 的 Trigger 版随停驻快照转发触发值。</summary>
    public static NodeTypeShape Shape { get; } = new()
    {
        AwaitsApproval = true,
        DerivesOutputsFromInputs = true,
        InputTypes = [PortTypes.Text, PortTypes.TextTrigger, PortTypes.Context, PortTypes.Signal, PortTypes.SignalTrigger],
        OutputTypes = [PortTypes.Text, PortTypes.TextTrigger, PortTypes.Context, PortTypes.Signal, PortTypes.SignalTrigger],
    };

    /// <summary>按类型专属声明建出类型实例，未声明时取默认声明。外部输入协议是建运行期对象所需的运行期依赖，由装配处给出。</summary>
    public static INodeType Create(ApprovalParameters? parameters, IInputChannel channel) =>
        new ApprovalNodeType(parameters, channel);
}
