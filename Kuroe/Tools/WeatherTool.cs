using Kuroe.Agent.Tools;
using Kuroe.Shared.Agent.Tools;

namespace Kuroe.Tools;

/// <summary>天气工具，当前为占位实现，接入真实天气服务时替换调用体。</summary>
sealed class WeatherTool : IAgentTool
{
    public IReadOnlyList<ToolFunction> Functions { get; } =
    [
        new ToolFunction("GetWeather", "查询指定城市当天的实时天气情况",
            [new ToolParameter("city", "城市的中文名称，例如：北京、上海", Required: true)],
            arguments => arguments.Text("city") is { Length: > 0 } city
                ? $"{city} 今天多云，气温 20~24°C，局部有小雨。"
                : "被拒绝：缺少 city。"),
    ];
}
