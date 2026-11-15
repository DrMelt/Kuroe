using Xunit;

namespace Kuroe.Tool.KuroeInfo.Tests;

/// <summary>工具面查询工具：放行前缀与未配置命令工具的提示。</summary>
public sealed class ToolboxKuroeInfoToolTests
{
    [Fact]
    public void Describe_lists_the_dialogue_modes_and_the_empty_command_tools_hint()
    {
        ToolboxKuroeInfoTool tool = new(
            [
                new DialogueToolGroup("只读", [new ToolPath("file/read"), new ToolPath("git/read")]),
                new DialogueToolGroup("更改", [new ToolPath("file/read"), new ToolPath("file/write")]),
            ],
            []);

        string output = ToolResult.Render(Assert.Single(tool.Functions)
            .Invoke(new ToolArguments(new Dictionary<string, object?>())));

        Assert.Contains("只读：file/read、git/read", output, StringComparison.Ordinal);
        Assert.Contains("更改：file/read、file/write", output, StringComparison.Ordinal);
        Assert.Contains("没有配置命令工具", output, StringComparison.Ordinal);
    }
}
