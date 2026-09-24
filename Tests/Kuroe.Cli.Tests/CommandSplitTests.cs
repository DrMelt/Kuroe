using Kuroe.Cli.Commands;
using Xunit;

namespace Kuroe.Cli.Tests;

/// <summary>命令参数按空白拆分，双引号内的空白算一个参数，引号本身不出现在结果里。</summary>
public sealed class CommandSplitTests
{
    [Fact]
    public void Plain_space_separates_arguments()
    {
        Assert.Equal(["/set", "Agent:Temperature", "0.7"], ReplCommands.Split("/set Agent:Temperature 0.7"));
    }

    [Fact]
    public void Whitespace_runs_are_collapsed()
    {
        Assert.Equal(["a", "b"], ReplCommands.Split("a\t  b"));
    }

    [Fact]
    public void Quoted_spaces_stay_within_one_argument()
    {
        Assert.Equal(["/set", "Agent:SystemPrompt", "多 词"], ReplCommands.Split("/set Agent:SystemPrompt \"多 词\""));
    }

    [Fact]
    public void Quotes_are_dropped_from_tokens()
    {
        Assert.Equal(["/task", "title", "1", "文档 补齐"], ReplCommands.Split("/task title 1 \"文档 补齐\""));
    }

    [Fact]
    public void Unclosed_quote_runs_to_the_end()
    {
        Assert.Equal(["未闭合引号"], ReplCommands.Split("未闭合\"引号"));
    }

    [Fact]
    public void Empty_arguments_are_kept_when_quoted()
    {
        Assert.Equal(["/set", "Agent:SystemPrompt", ""], ReplCommands.Split("/set Agent:SystemPrompt \"\""));
    }
}