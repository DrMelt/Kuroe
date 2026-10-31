using Kuroe.Console.Views;
using Spectre.Console;
using Spectre.Console.Rendering;
using Spectre.Console.Testing;
using Xunit;

namespace Kuroe.Console.Tests.Views;

/// <summary>结构块：标题按纯文本处理、列换行策略与字段表对齐。</summary>
public sealed class BlocksTests
{
    [Fact]
    public void Section_treats_the_title_as_plain_text()
    {
        Terminal terminal = ConsoleSession.NewTerminal(out TestConsole output, out _);

        terminal.Write(Blocks.Section(BoxKind.View, "任务 [1]", new Text("内容")));

        Assert.Contains("┌─任务 [1]", output.Output);
    }

    [Fact]
    public void Section_expands_to_the_console_width()
    {
        Terminal terminal = ConsoleSession.NewTerminal(out TestConsole output, out _);

        terminal.Write(Blocks.Section(BoxKind.View, "标题", new Text("内容")));

        string[] lines = output.Output.Replace("\r", string.Empty).Split('\n');
        string top = lines[0];
        string bottom = lines.First(line => line.Contains('└'));

        Assert.Contains('┌', top);
        Assert.Contains('┐', top);
        Assert.Contains('└', bottom);
        Assert.Contains('┘', bottom);
        Assert.True(top.Length > 40, output.Output);
    }

    [Fact]
    public void Table_wraps_only_the_columns_declared_to_wrap()
    {
        Terminal terminal = ConsoleSession.NewTerminal(out TestConsole output, out _);
        Table table = Blocks.Table(("名称", false), ("说明", true));
        table.AddRow(new Text("a"), new Text(new string('字', 100)));

        terminal.Write(table);

        string[] lines = output.Output.Replace("\r", string.Empty).Split('\n');
        Assert.True(lines.Count(line => line.Contains('字')) >= 2, output.Output);
    }

    [Fact]
    public void Table_wraps_within_a_narrow_console()
    {
        Terminal terminal = ConsoleSession.NewTerminal(out TestConsole output, out _);
        output.Profile.Width = 40;
        Table table = Blocks.Table(("名称", false), ("说明", true));
        table.AddRow(new Text("a"), new Text(new string('字', 60)));

        terminal.Write(table);

        string[] lines = output.Output.Replace("\r", string.Empty).Split('\n');
        Assert.True(lines.Count(line => line.Contains('字')) >= 4, output.Output);
    }

    [Fact]
    public void Fields_aligns_labels_and_values()
    {
        Terminal terminal = ConsoleSession.NewTerminal(out TestConsole output, out _);

        terminal.Write(Blocks.Fields(
            ("目标", new Text("补齐 README")),
            ("流程", new Text("默认"))));

        string[] lines = output.Output.Replace("\r", string.Empty).Split('\n');
        int goal = Array.FindIndex(lines, line => line.Contains("补齐 README"));
        int flow = Array.FindIndex(lines, line => line.Contains("默认"));

        Assert.True(goal >= 0, output.Output);
        Assert.True(flow > goal, output.Output);
        Assert.Equal(
            lines[goal].IndexOf("补齐 README", StringComparison.Ordinal),
            lines[flow].IndexOf("默认", StringComparison.Ordinal));
    }

    [Fact]
    public void Section_without_a_title_draws_a_plain_top_border()
    {
        Terminal terminal = ConsoleSession.NewTerminal(out TestConsole output, out _);
        output.Profile.Width = 40;

        terminal.Write(Blocks.Section(BoxKind.View, null, new Text("内容")));

        Assert.Equal($"┌{new string('─', 38)}┐", Rows(output.Output)[0]);
    }

    [Fact]
    public void Frame_and_section_share_the_same_top_border()
    {
        foreach (BoxKind kind in Enum.GetValues<BoxKind>())
        {
            Terminal framed = ConsoleSession.NewTerminal(out TestConsole frameOutput, out _);
            frameOutput.Profile.Width = 40;
            framed.OpenFrame(kind, "标题").Dispose();

            Terminal paneled = ConsoleSession.NewTerminal(out TestConsole panelOutput, out _);
            panelOutput.Profile.Width = 40;
            paneled.Write(Blocks.Section(kind, "标题", new Text("内容")));

            Assert.Equal(Rows(panelOutput.Output)[0], Rows(frameOutput.Output)[0]);
        }
    }

    [Fact]
    public void Only_the_menu_uses_its_own_line()
    {
        Assert.NotEqual(BoxSkin.Of(BoxKind.View).Line, BoxSkin.Of(BoxKind.Menu).Line);
    }

    [Fact]
    public void Only_the_menu_uses_a_rounded_box()
    {
        Assert.Equal("╭", BoxSkin.Of(BoxKind.Menu).Shape.GetPart(BoxBorderPart.TopLeft));
        Assert.Equal("┌", BoxSkin.Of(BoxKind.Reply).Shape.GetPart(BoxBorderPart.TopLeft));
        Assert.Equal("┌", BoxSkin.Of(BoxKind.View).Shape.GetPart(BoxBorderPart.TopLeft));
    }

    private static string[] Rows(string output) =>
        output.Replace("\r", string.Empty).Split('\n', StringSplitOptions.RemoveEmptyEntries);
}