using Kuroe.Flow.Shared.RuntimeGraph;
using Kuroe.Flow.Shared.StaticGraph.Graph;

namespace Kuroe.Flow.InputChannel;

/// <summary>外部输入协议：执行中的节点据此声明此刻接收外部输入，宿主据此定位节点并交付输入。
/// 登记即代表本节点收下交付，交付承载是外部输入的文本、节点自行解释；等待是节点自己的执行状态，协议不表达它，也不表达节点在执行什么。
/// 一次声明对应一次交付，宿主交付后声明随之撤销；取消态下不得声明接收，随任务取消时撤销已作出的声明。
/// 读写都在任务 Gate 下。宿主侧的读取与交付见 <see cref="IInputDelivery"/>，只按登记节点交付，未登记的交付是调用方不变量违背。</summary>
public interface IInputChannel
{
    /// <summary>声明本节点此刻接收外部输入，交付经 receive 收下。节点在自己的执行中调用，取消态下不调用。</summary>
    void Accept(INodeContext task, NodePath node, Action<string> receive);

    /// <summary>声明本节点不再接收外部输入。节点随任务取消时调用，已交付的声明无需再撤销。</summary>
    void Decline(INodeContext task, NodePath node);
}