using ErrorOr;
using Kuroe.Tasks.Shared;

namespace Kuroe.Assistant.Dialogue;

/// <summary>对话会话操作错误。</summary>
internal static class DialogueErrors
{
    /// <summary>会话正在执行一轮，不能同时发送。</summary>
    public static Error Busy(TaskId id) =>
        Error.Conflict("Dialogue.Busy", $"{id} 正在执行，等本轮结束后再发送。");

    /// <summary>会话没有停止执行，不能删除。</summary>
    public static Error DeleteBusy(TaskId id) =>
        Error.Conflict("Dialogue.Delete", $"{id} 还有执行没有收口，稍后再试。");
}