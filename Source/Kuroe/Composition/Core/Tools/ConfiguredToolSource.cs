using Kuroe.Ability.Tool;
using Kuroe.Ability.Tool.Json;

namespace Kuroe.Composition.Tools;

/// <summary>配置工具源：装配期提供定义，运行期调用配置项的创建函数，只创建本轮放行的工具。</summary>
internal sealed class ConfiguredToolSource(IReadOnlyList<ConfiguredTool> tools) : ToolSource(
[
    .. tools.Select(tool => new ToolEntry(tool.Definition, tool.Create)),
])
{
}