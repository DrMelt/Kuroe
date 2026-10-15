namespace Kuroe.Flow.Shared.Waiting;

/// <summary>等待型节点的对外契约：宿主据此定位待处理节点并交付外部输入。接收外部输入由扩展实现，引擎不参与。</summary>
public interface IWaitNode
{
    /// <summary>等待的外部输入种类。</summary>
    AwaitKind Kind { get; }

    /// <summary>此刻是否停在等待。</summary>
    bool Awaiting { get; }

    /// <summary>交付一次外部输入，节点在其中放行并产出。</summary>
    void Receive(string text);
}
