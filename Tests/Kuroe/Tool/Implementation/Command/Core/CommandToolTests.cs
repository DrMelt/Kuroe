using ErrorOr;
using Kuroe.Tool.Command;
using Kuroe.Tool.Command.Model;
using Xunit;

namespace Kuroe.Tool.Command.Tests;

/// <summary>命令工具：模板展开按命令行断言，全程不起进程。</summary>
public sealed class CommandToolTests
{
    [Fact]
    public void Expand_rejects_required_missing_and_omits_empty_optional_list()
    {
        // 缺参与空列表在必填时都在展开期拒绝，不启动进程
        Assert.Equal("缺少参数 files。", Rejected(Texts(["cmd", "/c", "echo", "{files}"]), [RequiredFileParameter], Given()));
        Assert.Equal("缺少参数 files。",
            Rejected(Texts(["cmd", "/c", "echo", "{files}"]), [RequiredFileParameter], Given(("files", Array.Empty<string>()))));

        // 非必填空列表：占位参数项消失，命令只带周围文字
        Assert.Equal(
            ["cmd", "/c", "echo", "开始", "结束"],
            Argv(Texts(["cmd", "/c", "echo", "开始", "{files}", "结束"]), [FileParameter], Given(("files", Array.Empty<string>()))));
    }

    [Fact]
    public void Expand_spawns_one_argument_per_list_value()
    {
        Assert.Equal(
            ["cmd", "/c", "echo", "甲", "乙"],
            Argv(Texts(["cmd", "/c", "echo", "{files}"]), [FileParameter], Given(("files", stringArray))));
    }

    [Fact]
    public void Expand_keeps_item_without_placeholder_as_single_argument()
    {
        // 无占位符的项整体是一个参数项，不做拆分
        Assert.Equal(
            ["cmd", "/c", "echo", "开始 结束"],
            Argv(Texts(["cmd", "/c", "echo", "开始 结束"]), [], Given()));
    }

    [Fact]
    public void Expand_carries_scalar_and_list_values()
    {
        Assert.Equal(
            ["cmd", "/c", "echo", "开始", "你好", "甲", "乙"],
            Argv(Texts(["cmd", "/c", "echo", "开始", "{text}", "{files}"]), [TextParameter, FileParameter], Given(("text", "你好"), ("files", stringArray))));
    }

    [Fact]
    public void Expand_treats_scalar_empty_string_as_given_value()
    {
        Assert.Equal(
            ["cmd", "/c", "echo", ""],
            Argv(Texts(["cmd", "/c", "echo", "{text}"]), [TextParameter], Given(("text", ""))));
    }

    [Fact]
    public void Expand_omits_configured_item_when_linked_missing()
    {
        IReadOnlyList<CommandToolTemplateItem> template =
            Linked(("cmd", null), ("/c", null), ("echo", null), ("开始", null), ("--app={token}", "token"), ("结束", null));

        Assert.Equal(["cmd", "/c", "echo", "开始", "结束"], Argv(template, [TokenParameter], Given()));
        Assert.Equal(["cmd", "/c", "echo", "开始", "--app=x", "结束"], Argv(template, [TokenParameter], Given(("token", "x"))));
    }

    [Fact]
    public void Expand_adds_fixed_text_item_when_linked_flag_is_true()
    {
        IReadOnlyList<CommandToolTemplateItem> template =
            Linked(("cmd", null), ("/c", null), ("echo", null), ("开始", null), ("--staged", "staged"), ("结束", null));

        Assert.Equal(["cmd", "/c", "echo", "开始", "--staged", "结束"], Argv(template, [StagedParameter], Given(("staged", true))));
        Assert.Equal(["cmd", "/c", "echo", "开始", "结束"], Argv(template, [StagedParameter], Given(("staged", false))));
        Assert.Equal(["cmd", "/c", "echo", "开始", "结束"], Argv(template, [StagedParameter], Given()));
    }

    [Fact]
    public void Expand_omits_fixed_text_item_with_its_list_parameter()
    {
        IReadOnlyList<CommandToolTemplateItem> template =
            Linked(("cmd", null), ("/c", null), ("echo", null), ("开始", null), ("--", "files"), ("{files}", null));

        Assert.Equal(["cmd", "/c", "echo", "开始", "--", "甲", "乙"], Argv(template, [FileParameter], Given(("files", stringArray))));
        Assert.Equal(["cmd", "/c", "echo", "开始"], Argv(template, [FileParameter], Given(("files", Array.Empty<string>()))));
    }

    [Fact]
    public void Expand_rejects_configured_item_when_linked_required_missing()
    {
        IReadOnlyList<CommandToolTemplateItem> template =
            Linked(("cmd", null), ("/c", null), ("echo", null), ("--app={token}", "token"));

        Assert.Equal("缺少参数 token。", Rejected(template, [RequiredTokenParameter], Given()));
    }

    [Fact]
    public void Expand_keeps_literal_placeholder_when_optional_inline_missing()
    {
        IReadOnlyList<CommandToolTemplateItem> template =
            Linked(("cmd", null), ("/c", null), ("echo", null), ("--app={token}", null));

        Assert.Equal(["cmd", "/c", "echo", "--app={token}"], Argv(template, [TokenParameter], Given()));
    }

    [Fact]
    public void Expand_keeps_missing_optional_placeholder_alongside_given_ones()
    {
        IReadOnlyList<CommandToolTemplateItem> template =
            Linked(("cmd", null), ("/c", null), ("echo", null), ("--user={user}:{pass}", null));

        Assert.Equal(["cmd", "/c", "echo", "--user=alice:{pass}"], Argv(template, [UserParameter, PasswordParameter], Given(("user", "alice"))));
    }

    [Fact]
    public void Expand_spreads_configured_list_item_and_omits_when_empty()
    {
        IReadOnlyList<CommandToolTemplateItem> template =
            Linked(("cmd", null), ("/c", null), ("echo", null), ("{files}", "files"));

        Assert.Equal(["cmd", "/c", "echo"], Argv(template, [FileParameter], Given(("files", Array.Empty<string>()))));
        Assert.Equal(["cmd", "/c", "echo", "甲", "乙"], Argv(template, [FileParameter], Given(("files", stringArray))));
    }

    [Fact]
    public void Expand_rejects_definition_without_program()
    {
        Assert.Equal("模板展开后没有任何可执行的程序。", Rejected([], [], Given()));
    }

    /// <summary>展开得到命令行，被拒时用例失败。</summary>
    private static IReadOnlyList<string> Argv(IReadOnlyList<CommandToolTemplateItem> template, IReadOnlyList<CommandToolParameter> parameters, Dictionary<string, object?> values)
    {
        ErrorOr<IReadOnlyList<string>> argv = Expand(template, parameters, values);

        Assert.False(argv.IsError);

        return argv.Value;
    }

    /// <summary>展开被拒的错误文本，未拒绝时用例失败。</summary>
    private static string Rejected(IReadOnlyList<CommandToolTemplateItem> template, IReadOnlyList<CommandToolParameter> parameters, Dictionary<string, object?> values)
    {
        ErrorOr<IReadOnlyList<string>> argv = Expand(template, parameters, values);

        Assert.True(argv.IsError);

        return argv.FirstError.Description;
    }

    /// <summary>调用命令面的展开，只组装命令行，不起进程。</summary>
    private static ErrorOr<IReadOnlyList<string>> Expand(IReadOnlyList<CommandToolTemplateItem> template, IReadOnlyList<CommandToolParameter> parameters, Dictionary<string, object?> values) =>
        Definition(template, parameters).CommandLine(new ToolArguments(values));

    /// <summary>测试用的命令定义，执行目录、超时与输出上限取固定值。</summary>
    private static CommandToolDefinition Definition(IReadOnlyList<CommandToolTemplateItem> template, IReadOnlyList<CommandToolParameter> parameters) =>
        new(new ToolPath("Execution"), "说明", parameters, template, null, 5, 200);

    /// <summary>模板项：文字，无关联参数。</summary>
    private static IReadOnlyList<CommandToolTemplateItem> Texts(IEnumerable<string> texts) =>
        [.. texts.Select(text => new CommandToolTemplateItem(text, null))];

    /// <summary>模板项：文字与关联参数名。</summary>
    private static IReadOnlyList<CommandToolTemplateItem> Linked(params (string Text, string? Link)[] items) =>
        [.. items.Select(item => new CommandToolTemplateItem(item.Text, item.Link))];

    /// <summary>一次调用的实参。</summary>
    private static Dictionary<string, object?> Given(params (string Name, object? Value)[] values) =>
        values.ToDictionary(value => value.Name, value => value.Value);

    private static readonly CommandToolParameter FileParameter = new("files", "文件列表", List: true);
    private static readonly CommandToolParameter RequiredFileParameter = new("files", "文件列表", Required: true, List: true);
    private static readonly CommandToolParameter TextParameter = new("text", "文本", Required: true);
    private static readonly CommandToolParameter TokenParameter = new("token", "令牌");
    private static readonly CommandToolParameter RequiredTokenParameter = new("token", "令牌", Required: true);
    private static readonly CommandToolParameter UserParameter = new("user", "用户");
    private static readonly CommandToolParameter PasswordParameter = new("pass", "口令");
    private static readonly CommandToolParameter StagedParameter = new("staged", "开关", Flag: true);
    private static readonly string[] stringArray = ["甲", "乙"];
}
