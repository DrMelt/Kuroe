using ErrorOr;
using Kuroe.Tasks.Shared;

namespace Kuroe.Assistant.Dialogue;

/// <summary>一轮后台对话结束的通知。</summary>
public sealed record DialogueTurnNotice(TaskId Session, IReadOnlyList<Error> Errors)
{
    /// <summary>本轮是否成功。</summary>
    public bool Succeeded => Errors.Count == 0;
}