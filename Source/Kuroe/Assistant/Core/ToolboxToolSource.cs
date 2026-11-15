using Kuroe.Assistant.Dialogue;
using Kuroe.Tool;
using Kuroe.Tool.Json;
using Kuroe.Tool.KuroeInfo;

namespace Kuroe.Assistant;

/// <summary>工具面状态查询源：内置对话各模式白名单与配置工具的只读呈现。载体只在本轮放行时创建。</summary>
internal sealed class ToolboxToolSource(
    IReadOnlyList<DialogueMode> modes,
    IReadOnlyList<ConfiguredTool> configuredTools) : ToolSource(EntriesOf(modes, configuredTools))
{
    private static IReadOnlyList<ToolEntry> EntriesOf(
        IReadOnlyList<DialogueMode> modes,
        IReadOnlyList<ConfiguredTool> configuredTools)
    {
        IReadOnlyList<DialogueToolGroup> groups =
        [
            .. modes.Select(mode => new DialogueToolGroup(mode.DisplayName, mode.Tools)),
        ];

        return
        [
            .. new ToolboxKuroeInfoTool(groups, configuredTools).Functions.Select(function =>
                ToolEntry.Of(
                    function.Definition,
                    () => new ToolboxKuroeInfoTool(groups, configuredTools))),
        ];
    }
}