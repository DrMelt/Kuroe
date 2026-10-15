namespace Kuroe.Flows.Shared.Waiting;

/// <summary>等待型节点在等什么外部输入。等待型节点不启动 run，放行由外部输入给出。宿主据此区分待处理事项。</summary>
public enum AwaitKind
{
    /// <summary>等用户回答，放行后的产出是回答内容。</summary>
    Answer,

    /// <summary>等用户批准，放行后的产出是转发上游产出。</summary>
    Approve,
}
