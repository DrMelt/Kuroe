using Kuroe.Tool;
using Kuroe.Tool.Json;
using Kuroe.Tool.KuroeInfo;

namespace Kuroe.Assistant;

/// <summary>工具面状态查询源：内置对话白名单与配置工具的只读呈现。载体只在本轮放行时创建。</summary>
internal sealed class ToolboxToolSource(
    IReadOnlyList<ToolPath> dialogueTools,
    IReadOnlyList<ConfiguredTool> configuredTools) : ToolSource(EntriesOf(dialogueTools, configuredTools))
{
    private static IReadOnlyList<ToolEntry> EntriesOf(
        IReadOnlyList<ToolPath> dialogueTools,
        IReadOnlyList<ConfiguredTool> configuredTools) =>
        [.. new ToolboxKuroeInfoTool(dialogueTools, configuredTools).Functions.Select(function =>
            ToolEntry.Of(
                function.Definition,
                () => new ToolboxKuroeInfoTool(dialogueTools, configuredTools)))];
}