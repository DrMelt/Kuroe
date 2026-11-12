using Kuroe.Flow.Shared.Model;
using Kuroe.Flow.Shared.RuntimeGraph.Execution;
using Kuroe.Flow.Shared.RuntimeGraph.Ports;

namespace Kuroe.Flow.Shared.RuntimeGraph.Nodes;

/// <summary>节点可见的输入内容：按绑定顺序的取值、按输入端口聚合的取值与端口文本。
/// 触发位与连接状态属于流程控制与执行装配，不在节点视图里；静态输入值并入端口文本。</summary>
public interface INodeInputs : IPortTexts
{
    /// <summary>按绑定顺序排列的取值，每条给出目标端口、端口类型与取值。</summary>
    IReadOnlyList<NodeInputValue> Ordered { get; }

    /// <summary>该输入端口的全部取值，端口没有来源时为空。</summary>
    IReadOnlyList<ContextValue> Values(PortName input);
}