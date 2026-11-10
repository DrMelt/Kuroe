using ErrorOr;
using Xunit;

namespace Kuroe.Tool.Command.Shared.Tests;

/// <summary>命令工具：模板展开按命令行断言，全程不起进程。</summary>
public sealed class CommandToolTemplateTests
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

    [Fact]
    public void Expand_replaces_path_variables_before_parameters()
    {
        CommandToolDefinition definition = new(
            new ToolPath("Build"),
            "说明",
            [],
            [new("{root}/src", null)],
            null,
            5,
            200,
            ["root"]);

        ErrorOr<IReadOnlyList<string>> argv = definition.CommandLine(
            new ToolArguments(Given()),
            new Dictionary<string, string> { ["root"] = "D:/task" });

        Assert.False(argv.IsError);
        Assert.Equal(["D:/task/src"], argv.Value);
    }

    [Fact]
    public void Expand_repeats_embedded_list_item_per_value()
    {
        IReadOnlyList<CommandToolTemplateItem> template =
            Linked(("cmd", null), ("--files={files}", null));

        Assert.Equal(
            ["cmd", "--files=a", "--files=b"],
            Argv(template, [FileParameter], Given(("files", stringArray0))));
    }

    [Fact]
    public void Expand_repeats_prefix_and_suffix_in_order()
    {
        IReadOnlyList<CommandToolTemplateItem> template =
            Linked(("cmd", null), ("--file={files}:v1", null));

        Assert.Equal(
            ["cmd", "--file=a:v1", "--file=b:v1"],
            Argv(template, [FileParameter], Given(("files", stringArray0))));
    }

    [Fact]
    public void Expand_omits_empty_optional_embedded_list()
    {
        IReadOnlyList<CommandToolTemplateItem> template =
            Linked(("cmd", null), ("--files={files}", null));

        Assert.Equal(["cmd"], Argv(template, [FileParameter], Given(("files", Array.Empty<string>()))));
    }

    [Fact]
    public void Expand_rejects_required_empty_embedded_list()
    {
        IReadOnlyList<CommandToolTemplateItem> template =
            Linked(("cmd", null), ("--files={files}", null));

        Assert.Equal(
            "缺少参数 files。",
            Rejected(template, [RequiredFileParameter], Given(("files", Array.Empty<string>()))));
    }

    [Fact]
    public void Expand_pairs_multiple_lists_by_index()
    {
        IReadOnlyList<CommandToolTemplateItem> template =
            Linked(("cmd", null), ("{files}:{others}", null));

        Assert.Equal(
            ["cmd", "甲:x", "乙:y"],
            Argv(template, [FileParameter, OthersParameter], Given(("files", stringArray), ("others", stringArray2))));
    }

    [Fact]
    public void Expand_rejects_unequal_list_lengths()
    {
        IReadOnlyList<CommandToolTemplateItem> template =
            Linked(("cmd", null), ("{files}:{others}", null));

        Assert.Equal(
            "列表参数 files 与 others 的元素数量必须一致。",
            Rejected(template, [FileParameter, OthersParameter], Given(("files", stringArray), ("others", stringArray3))));
    }

    [Fact]
    public void Expand_omits_item_when_all_optional_lists_are_empty()
    {
        IReadOnlyList<CommandToolTemplateItem> template =
            Linked(("cmd", null), ("{files}:{others}", null));

        Assert.Equal(
            ["cmd"],
            Argv(template, [FileParameter, OthersParameter],
                Given(("files", Array.Empty<string>()), ("others", Array.Empty<string>()))));
    }

    [Fact]
    public void Expand_rejects_required_empty_list_among_multiple()
    {
        IReadOnlyList<CommandToolTemplateItem> template =
            Linked(("cmd", null), ("{files}:{others}", null));

        Assert.Equal(
            "缺少参数 files。",
            Rejected(template, [RequiredFileParameter, OthersParameter],
                Given(("files", Array.Empty<string>()), ("others", stringArray3))));
    }

    [Fact]
    public void Expand_repeats_same_list_parameter_with_one_value()
    {
        IReadOnlyList<CommandToolTemplateItem> template =
            Linked(("cmd", null), ("{files}:{files}", null));

        Assert.Equal(
            ["cmd", "甲:甲", "乙:乙"],
            Argv(template, [FileParameter], Given(("files", stringArray))));
    }

    [Fact]
    public void Expand_rejects_missing_optional_scalar_in_list_item()
    {
        IReadOnlyList<CommandToolTemplateItem> template =
            Linked(("cmd", null), ("--user={files}:{role}", null));

        Assert.Contains(
            "参数 role 缺失",
            Rejected(template, [FileParameter, RoleParameter], Given(("files", stringArray))),
            StringComparison.Ordinal);
    }

    [Fact]
    public void Expand_applies_scalar_to_each_embedded_list_value()
    {
        IReadOnlyList<CommandToolTemplateItem> template =
            Linked(("cmd", null), ("--user={files}:{role}", null));

        Assert.Equal(
            ["cmd", "--user=甲:admin", "--user=乙:admin"],
            Argv(template, [FileParameter, RoleParameter], Given(("files", stringArray), ("role", "admin"))));
    }

    [Fact]
    public void Expand_does_not_rescan_replacement_values()
    {
        IReadOnlyList<CommandToolTemplateItem> template =
            Linked(("cmd", null), ("--x={text}:{role}", null));

        Assert.Equal(
            ["cmd", "--x={role}:v"],
            Argv(template, [TextParameter, RoleParameter], Given(("text", "{role}"), ("role", "v"))));
    }

    [Fact]
    public void Expand_rejects_bare_value_starting_with_dash()
    {
        IReadOnlyList<CommandToolTemplateItem> template = [new("{name}", null)];

        Assert.Equal(
            "参数 name 的值不能以 - 开头。",
            Rejected(template, [NameParameter], Given(("name", "--help"))));
    }

    [Fact]
    public void Expand_accepts_bare_value_without_dash()
    {
        IReadOnlyList<CommandToolTemplateItem> template = [new("{name}", null)];

        Assert.Equal(
            ["HEAD~1"],
            Argv(template, [NameParameter], Given(("name", "HEAD~1"))));
    }

    [Fact]
    public void Expand_rejects_list_values_starting_with_dash()
    {
        IReadOnlyList<CommandToolTemplateItem> template = [new("{files}", null)];

        Assert.Equal(
            "参数 files 的值不能以 - 开头。",
            Rejected(template, [FileParameter], Given(("files", stringArray1))));
    }

    [Fact]
    public void Expand_rejects_dash_value_after_terminator()
    {
        IReadOnlyList<CommandToolTemplateItem> template = [new("--", null), new("{name}", null)];

        Assert.Equal(
            "参数 name 的值不能以 - 开头。",
            Rejected(template, [NameParameter], Given(("name", "-a.txt"))));
    }

    [Fact]
    public void Expand_accepts_dash_value_after_terminator_when_parameter_allows_it()
    {
        IReadOnlyList<CommandToolTemplateItem> template = [new("--", null), new("{name}", null)];

        Assert.Equal(
            ["--", "-a.txt"],
            Argv(template, [AllowedNameParameter], Given(("name", "-a.txt"))));
    }

    [Fact]
    public void Expand_rejects_embedded_value_starting_with_dash()
    {
        IReadOnlyList<CommandToolTemplateItem> template = [new("--name={name}", null)];

        Assert.Equal(
            "参数 name 的值不能以 - 开头。",
            Rejected(template, [NameParameter], Given(("name", "--help"))));
    }

    [Fact]
    public void Expand_accepts_embedded_dash_value_when_parameter_allows_it()
    {
        IReadOnlyList<CommandToolTemplateItem> template = [new("--name={name}", null)];

        Assert.Equal(
            ["--name=--help"],
            Argv(template, [AllowedNameParameter], Given(("name", "--help"))));
    }

    [Fact]
    public void Expand_accepts_list_dash_value_when_parameter_allows_it()
    {
        IReadOnlyList<CommandToolTemplateItem> template = [new("{files}", null)];

        Assert.Equal(
            ["a", "-b"],
            Argv(template, [AllowedFileParameter], Given(("files", stringArray1))));
    }

    [Fact]
    public void Expand_rejects_dash_value_after_linked_option()
    {
        IReadOnlyList<CommandToolTemplateItem> template = [new("--name", "name"), new("{name}", null)];

        Assert.Equal(
            "参数 name 的值不能以 - 开头。",
            Rejected(template, [NameParameter], Given(("name", "--help"))));
    }

    [Fact]
    public void Expand_rejects_value_after_unlinked_option()
    {
        IReadOnlyList<CommandToolTemplateItem> template = [new("--name", null), new("{name}", null)];

        Assert.Equal(
            "参数 name 的值不能以 - 开头。",
            Rejected(template, [NameParameter], Given(("name", "--help"))));
    }

    [Fact]
    public void Expand_does_not_check_path_variable_values()
    {
        CommandToolDefinition definition = new(
            new ToolPath("Execution"),
            "说明",
            [],
            [new("{root}/src", null)],
            null,
            5,
            200,
            ["root"]);

        ErrorOr<IReadOnlyList<string>> argv = definition.CommandLine(
            new ToolArguments(Given()),
            new Dictionary<string, string> { ["root"] = "-root" });

        Assert.False(argv.IsError);
        Assert.Equal(["-root/src"], argv.Value);
    }

    /// <summary>展开得到命令行，被拒时用例失败。</summary>
    private static IReadOnlyList<string> Argv(IReadOnlyList<CommandToolTemplateItem> template, IReadOnlyList<ToolParameter> parameters, Dictionary<string, object?> values)
    {
        ErrorOr<IReadOnlyList<string>> argv = Expand(template, parameters, values);

        Assert.False(argv.IsError);

        return argv.Value;
    }

    /// <summary>展开被拒的错误文本，未拒绝时用例失败。</summary>
    private static string Rejected(IReadOnlyList<CommandToolTemplateItem> template, IReadOnlyList<ToolParameter> parameters, Dictionary<string, object?> values)
    {
        ErrorOr<IReadOnlyList<string>> argv = Expand(template, parameters, values);

        Assert.True(argv.IsError);

        return argv.FirstError.Description;
    }

    /// <summary>调用命令面的展开，只组装命令行，不起进程。</summary>
    private static ErrorOr<IReadOnlyList<string>> Expand(IReadOnlyList<CommandToolTemplateItem> template, IReadOnlyList<ToolParameter> parameters, Dictionary<string, object?> values) =>
        Definition(template, parameters).CommandLine(new ToolArguments(values));

    /// <summary>测试用的命令定义，执行目录、超时与输出上限取固定值。</summary>
    private static CommandToolDefinition Definition(IReadOnlyList<CommandToolTemplateItem> template, IReadOnlyList<ToolParameter> parameters) =>
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

    private static readonly ToolParameter FileParameter = new(new ToolName("files"), "文件列表", List: true);
    private static readonly ToolParameter RequiredFileParameter = new(new ToolName("files"), "文件列表", Required: true, List: true);
    private static readonly ToolParameter TextParameter = new(new ToolName("text"), "文本", Required: true);
    private static readonly ToolParameter TokenParameter = new(new ToolName("token"), "令牌");
    private static readonly ToolParameter RequiredTokenParameter = new(new ToolName("token"), "令牌", Required: true);
    private static readonly ToolParameter RoleParameter = new(new ToolName("role"), "角色");
    private static readonly ToolParameter OthersParameter = new(new ToolName("others"), "另一个列表", List: true);
    private static readonly ToolParameter NameParameter = new(new ToolName("name"), "名字");
    private static readonly ToolParameter UserParameter = new(new ToolName("user"), "用户");
    private static readonly ToolParameter PasswordParameter = new(new ToolName("pass"), "口令");
    private static readonly ToolParameter StagedParameter = new(new ToolName("staged"), "开关", Flag: true);
    private static readonly ToolParameter AllowedNameParameter = new(new ToolName("name"), "名字", AllowLeadingDash: true);
    private static readonly ToolParameter AllowedFileParameter = new(new ToolName("files"), "文件列表", List: true, AllowLeadingDash: true);
    private static readonly string[] stringArray = ["甲", "乙"];
    private static readonly string[] stringArray0 = ["a", "b"];
    private static readonly string[] stringArray1 = ["a", "-b"];
    private static readonly string[] stringArray2 = ["x", "y"];
    private static readonly string[] stringArray3 = ["x"];
}
