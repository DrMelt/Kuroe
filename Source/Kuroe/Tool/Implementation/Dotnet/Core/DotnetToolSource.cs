using Kuroe.Tool.Command;

namespace Kuroe.Tool.Dotnet;

/// <summary>dotnet 工具源：装配期提供定义，运行期按执行绑定创建本轮可用的命令工具。</summary>
public sealed class DotnetToolSource : ToolSource
{
    private static readonly IReadOnlyList<ToolEntry> Entries =
        [.. DotnetTools.Definitions.Select(definition =>
            new ToolEntry(definition.Definition, binding => new CommandTool(definition, binding)))];

    /// <summary>建立 dotnet 工具源。</summary>
    public DotnetToolSource()
        : base(Entries)
    {
    }
}