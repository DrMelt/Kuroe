using Kuroe.Executions.Tools;
using Kuroe.Shared.Executions.Tools;

namespace Kuroe.Tools;

/// <summary>系统时间工具。</summary>
sealed class TimeTool : ITool
{
    public IReadOnlyList<ToolFunction> Functions { get; } =
    [
        new ToolFunction(new ToolName("GetLocalTime"), "获取当前系统时区的准确当地时间", [],
            _ => $"当前系统时间是：{DateTime.Now:yyyy-MM-dd HH:mm:ss}"),
    ];
}
