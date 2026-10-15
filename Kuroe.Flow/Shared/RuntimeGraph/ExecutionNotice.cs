using ErrorOr;

namespace Kuroe.Flow.Shared.RuntimeGraph;

/// <summary>值得宿主单独提示一句的执行事件。成功进展由发起方当场呈现，这里只承载问题。</summary>
public enum NoticeLevel
{
    /// <summary>可恢复的问题：异常、停驻与取消。</summary>
    Warning,

    /// <summary>失败。</summary>
    Error,
}

/// <summary>一条执行侧的通知文本，宿主把它打成一行。</summary>
public sealed record ExecutionNotice(NoticeLevel Level, string Text)
{
    /// <summary>由错误列表构成一条通知，用于装配与校验失败。</summary>
    public static ExecutionNotice From(IEnumerable<Error> errors, string prefix) =>
        new(NoticeLevel.Error, $"{prefix}：{string.Join("；", errors.Select(error => error.Description))}");
}
