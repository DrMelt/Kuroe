using Kuroe.FlowDescription.Definition.Shared.Model;
using Kuroe.FlowDescription.Definition.Shared.Nodes;
using Kuroe.FlowDescription.Definition.Shared.Ports;

namespace Kuroe.Node.Approval;

/// <summary>审批节点类型的对外入口：类型名、静态形状与实例建立。审批节点启动执行后登记等待，批准后按同名输出端口放出停驻时的取值。</summary>
public static class ApprovalNodeTypes
{
    /// <summary>类型名，与流程文件里 Kind 的取值对应。</summary>
    public static NodeKind Name { get; } = new("Approval");

    /// <summary>审批节点类型的静态形状：派发执行并等待外部批准，对外端口由输入端口派生，<see cref="PortTypes.Signal"/> 只作驱动，端口只转发停驻时的内容。</summary>
    public static NodeTypeShape Shape { get; } = new()
    {
        DispatchesExecutions = true,
        AwaitsExternalInput = true,
        AcceptsInferredInputs = true,
        Outputs = OutputDeclaration.DerivedFromInputs.Instance,
        InputTypes = [PortTypes.Text, PortTypes.Context, PortTypes.Signal],
        OutputTypes = [PortTypes.Text, PortTypes.Context, PortTypes.Signal],
    };

    /// <summary>按类型专属声明建出类型实例，未声明时取默认声明。</summary>
    public static INodeType Create(ApprovalParameters? parameters) =>
        new ApprovalNodeType(parameters);
}
