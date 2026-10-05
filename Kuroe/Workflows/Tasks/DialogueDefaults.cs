using Kuroe.Shared.Executions.Tools;
using Kuroe.Shared.Workflows.Flows;

namespace Kuroe.Workflows.Tasks;

/// <summary>前台对话的固定节点配置：节点名、产出契约与工具白名单。当前由代码固定，工具面只放行白名单内的工具。</summary>
internal static class DialogueDefaults
{
    /// <summary>前台对话在过程记录里的节点名。</summary>
    public static NodeName Name { get; } = NodeName.Dialogue;

    /// <summary>前台对话的产出契约。</summary>
    public static NodeOutput Output { get; } = NodeOutput.Text;

    /// <summary>前台对话可用的能力工具白名单，未列出的路径不可用。写上级路径即放行整棵子树，信息查询工具整组在 info 下，文件访问工具整组在 files 下。</summary>
    public static IReadOnlyList<ToolPath> Tools { get; } =
    [
        new ToolPath("GetLocalTime"),
        new ToolPath("info"),
        new ToolPath("files"),
    ];
}
