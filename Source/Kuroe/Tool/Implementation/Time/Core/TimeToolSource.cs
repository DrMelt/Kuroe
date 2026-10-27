namespace Kuroe.Tool.Time;

/// <summary>系统时间工具源，只在本轮放行时创建载体。</summary>
public sealed class TimeToolSource() : ToolSource(
[
    .. new TimeTool().Functions.Select(function =>
        ToolEntry.Of(function.Definition, () => new TimeTool())),
])
{
}