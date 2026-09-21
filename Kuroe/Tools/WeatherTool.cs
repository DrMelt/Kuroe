using System.ComponentModel;
using Kuroe.Agent;

namespace Kuroe.Tools;

/// <summary>天气工具，当前为占位实现，接入真实天气服务时替换方法体。</summary>
public sealed class WeatherTool : IAgentTool
{
    [Description("查询指定城市当天的实时天气情况")]
    public string GetWeather([Description("城市的中文名称，例如：北京、上海")] string city) =>
        $"{city} 今天多云，气温 20~24°C，局部有小雨。";
}
