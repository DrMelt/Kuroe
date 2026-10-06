using Kuroe.Shared.Executions.Tools;

namespace Kuroe.Tools;

/// <summary>系统时间工具。</summary>
sealed class TimeTool : ITool
{
    public IReadOnlyList<ToolFunction> Functions { get; } =
    [
        new ToolFunction(new ToolName("GetLocalTime"), "获取当前系统时区与准确当地时间", [],
            _ =>
            {
                DateTimeOffset now = DateTimeOffset.Now;
                return $"当前系统时间是：{now:yyyy-MM-dd HH:mm:ss}，时区 UTC{now:zzz}";
            }),
    ];
}
