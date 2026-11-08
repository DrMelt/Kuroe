namespace Kuroe.Tool.Time;

/// <summary>系统时间工具。</summary>
/// <param name="time">本地时间的来源。</param>
public sealed class TimeTool(TimeProvider time) : ITool
{
    /// <summary>获取当前系统时区与当地时间的函数定义。</summary>
    private static readonly ToolDefinition GetLocalTime =
        new(new ToolPath("GetLocalTime"), "获取当前系统时区与准确当地时间", []);

    /// <summary>本载体的函数声明。</summary>
    public IReadOnlyList<ToolFunction> Functions { get; } =
    [
        GetLocalTime.Bind(_ =>
        {
            DateTimeOffset now = time.GetLocalNow();

            return $"当前系统时间是：{now:yyyy-MM-dd HH:mm:ss}，时区 UTC{now:zzz}";
        }),
    ];
}
