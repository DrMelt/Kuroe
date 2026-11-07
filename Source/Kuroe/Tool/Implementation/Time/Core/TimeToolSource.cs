namespace Kuroe.Tool.Time;

/// <summary>系统时间工具源，只在本轮放行时创建载体。</summary>
/// <param name="time">载体读取本地时间的来源。</param>
public sealed class TimeToolSource(TimeProvider time) : ToolSource(
[
    .. new TimeTool(time).Functions.Select(function =>
        ToolEntry.Of(function.Definition, () => new TimeTool(time))),
])
{
}