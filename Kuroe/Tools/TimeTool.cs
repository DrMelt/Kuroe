using Kuroe.Agent.Tools;
using Kuroe.Shared.Agent.Tools;

namespace Kuroe.Tools;

/// <summary>系统时间工具。</summary>
sealed class TimeTool : IAgentTool
{
    public IReadOnlyList<ToolFunction> Functions { get; } =
    [
        new ToolFunction("GetLocalTime", "获取当前系统时区的准确当地时间", [],
            _ => $"当前系统时间是：{DateTime.Now:yyyy-MM-dd HH:mm:ss}"),
    ];
}
