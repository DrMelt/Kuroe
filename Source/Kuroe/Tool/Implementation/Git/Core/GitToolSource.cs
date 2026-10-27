using Kuroe.Tool.Command;

namespace Kuroe.Tool.Git;

/// <summary>git 工具源：装配期提供定义，运行期按执行绑定创建本轮可用的命令工具。</summary>
public sealed class GitToolSource : ToolSource
{
    private static readonly IReadOnlyList<ToolEntry> Entries =
        [.. GitTools.Definitions.Select(definition =>
            new ToolEntry(definition.Definition, binding => new CommandTool(definition, binding)))];

    /// <summary>建立 git 工具源。</summary>
    public GitToolSource()
        : base(Entries)
    {
    }
}