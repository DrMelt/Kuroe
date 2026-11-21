using ErrorOr;

namespace Kuroe.Kernel.Execution.Shared.Notice;

/// <summary>通知的严重程度。</summary>
public enum NoticeLevel
{
    /// <summary>不需要处理的推进事件，如启动组因不满足触发条件被消耗。</summary>
    Information,

    /// <summary>可恢复的问题：异常、停驻与取消。</summary>
    Warning,

    /// <summary>失败。</summary>
    Error,
}

/// <summary>执行侧需要单独说明的推进与问题。</summary>
public sealed record ExecutionNotice(NoticeLevel Level, string Text)
{
    /// <summary>由错误列表构成一条通知，用于装配与校验失败。</summary>
    public static ExecutionNotice From(IEnumerable<Error> errors, string prefix) =>
        new(NoticeLevel.Error, $"{prefix}：{string.Join("；", errors.Select(error => error.Description))}");
}
