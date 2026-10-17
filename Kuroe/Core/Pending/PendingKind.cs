namespace Kuroe.Pending;

/// <summary>宿主对等待的读法：节点在等回答还是等批准。命令、文案与浏览器动作按它分派，节点一侧不表达这个区分。</summary>
public enum PendingKind
{
    /// <summary>等回答，交付内容。</summary>
    Answer,

    /// <summary>等批准，交付空的放行。</summary>
    Approve,
}