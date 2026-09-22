using System.ComponentModel;
using Kuroe.Agent;

namespace Kuroe.Tools;

/// <summary>系统时间工具。</summary>
sealed class TimeTool : IAgentTool
{
    [Description("获取当前系统时区的准确当地时间")]
    public static string GetLocalTime() => $"当前系统时间是：{DateTime.Now:yyyy-MM-dd HH:mm:ss}";
}
