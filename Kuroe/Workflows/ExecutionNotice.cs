using ErrorOr;

namespace Kuroe.Workflows;

/// <summary>值得宿主单独提示一句的执行事件。</summary>
public enum NoticeLevel
{
    Info,
    Done,
    Warning,
    Error,
}

/// <summary>一条执行侧的通知文本，宿主把它打成一行。</summary>
public sealed record ExecutionNotice(NoticeLevel Level, string Text)
{
    /// <summary>由错误列表构成一条通知，用于装配与校验失败。</summary>
    public static ExecutionNotice From(IEnumerable<Error> errors, string prefix) =>
        new(NoticeLevel.Error, $"{prefix}：{string.Join("；", errors.Select(error => error.Description))}");
}
