using Xunit;

namespace Kuroe.Tasks.Shared.Tests;

/// <summary>任务标题：直接构造提交输入与标题，断言截断与优先级。</summary>
public sealed class TaskTitleTests
{
    [Fact(DisplayName = "标题保留第一行")]
    public void Title_keeps_the_first_line()
    {
        Assert.Equal("第一行", TaskTitle.Shorten("第一行\n第二行"));
    }

    [Fact(DisplayName = "标题截断到四十个字符")]
    public void Title_is_truncated_at_forty_characters()
    {
        string title = TaskTitle.Shorten(new string('目', 50));

        Assert.Equal(41, title.Length);
        Assert.StartsWith(new string('目', 40), title, StringComparison.Ordinal);
        Assert.EndsWith("…", title, StringComparison.Ordinal);
    }

    [Fact(DisplayName = "显式标题优先于首条输入")]
    public void Explicit_title_wins_over_the_first_input()
    {
        StartInputs inputs = StartInputs.Single(new PortName("目标"), "目标文本");

        Assert.Equal("显式标题", TaskTitle.Compose(inputs, new FlowName("默认"), "显式标题"));
    }

    [Fact(DisplayName = "没有显式标题时使用首条输入")]
    public void First_input_is_used_without_an_explicit_title()
    {
        StartInputs inputs = StartInputs.Single(new PortName("目标"), "目标文本");

        Assert.Equal("目标文本", TaskTitle.Compose(inputs, new FlowName("默认"), null));
    }

    [Fact(DisplayName = "没有输入时使用流程名")]
    public void Flow_name_is_used_without_inputs()
    {
        Assert.Equal("默认", TaskTitle.Compose(new StartInputs([]), new FlowName("默认"), null));
    }
}
