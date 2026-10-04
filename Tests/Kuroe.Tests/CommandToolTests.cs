using ErrorOr;
using Kuroe.Executions.Tools;
using Kuroe.Executions.Turns;
using Kuroe.Shared.Executions;
using Kuroe.Shared.Executions.Tools;
using Kuroe.Shared.Executions.Turns;
using Kuroe.Shared.Workflows.Flows;
using Kuroe.Tools.CommandTools;
using Microsoft.Extensions.AI;
using Xunit;

namespace Kuroe.Tests;

/// <summary>命令工具：配置文件装配校验与模板展开，进程执行只在 Windows 上冒烟。</summary>
public sealed class CommandToolTests
{
    [Fact]
    public void Load_returns_empty_when_file_missing()
    {
        ErrorOr<IReadOnlyList<CommandToolDefinition>> loaded =
            CommandToolStore.Load(Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N")), Path.GetTempPath());

        Assert.False(loaded.IsError);
        Assert.Empty(loaded.Value);
    }

    [Fact]
    public void Load_accepts_valid_definitions_with_scalar_and_list_parameters()
    {
        string file = Write("""
            {
              "Tools": [
                {
                  "Name": "RunDiff",
                  "Template": ["git", "diff", "{path}"],
                  "Parameters": [ { "Name": "path", "Required": true } ]
                },
                {
                  "Name": "RunLog",
                  "Template": ["git", "log", "--oneline", "--", "{files}"],
                  "Parameters": [ { "Name": "files", "List": true } ]
                }
              ]
            }
            """);

        ErrorOr<IReadOnlyList<CommandToolDefinition>> loaded = CommandToolStore.Load(file, Path.GetTempPath());
        Assert.False(loaded.IsError);

        CommandToolDefinition diff = loaded.Value[0];
        Assert.Equal("RunDiff", diff.Name.Value);
        Assert.Equal(["git", "diff", "{path}"], diff.Template.Select(item => item.Text));
        Assert.Null(diff.Template[2].OmitWhenMissing);
        Assert.Single(diff.Parameters);
        Assert.True(diff.Parameters[0].Required);

        CommandToolDefinition log = loaded.Value[1];
        Assert.True(log.Parameters[0].List);
        Assert.False(log.Parameters[0].Required);
    }

    [Theory]
    [InlineData("""{ "Tools": [ { "Name": "A", "Template": ["x", "{missing}"] } ] }""", "没有对应的参数声明")]
    [InlineData("""{ "Tools": [ { "Name": "A", "Template": ["x"], "Parameters": [ { "Name": "p" } ] } ] }""", "没有出现在模板里")]
    [InlineData("""{ "Tools": [ { "Name": "A", "Template": ["x", "--flag={files}"], "Parameters": [ { "Name": "files", "List": true } ] } ] }""", "独立的")]
    [InlineData("""{ "Tools": [ { "Name": "", "Template": ["x"] } ] }""", "工具名不能为空")]
    [InlineData("""{ "Tools": [ { "Name": "A", "Template": ["x", "{p}"], "Parameters": [ { "Name": "p" }, { "Name": "p" } ] } ] }""", "参数名 p 重复")]
    [InlineData("""{ "Tools": [ { "Name": "A", "Template": ["x"], "TimeoutSeconds": 0 } ] }""", "TimeoutSeconds 必须是正整数")]
    [InlineData("""{ "Tools": [ { "Name": "A", "Template": ["x"], "TimeoutSeconds": 2147484 } ] }""", "TimeoutSeconds 不能超过")]
    [InlineData("""{ "Tools": [ { "Name": "A", "Template": [] } ] }""", "模板不能为空")]
    [InlineData("""{ "Tools": [ { "Name": "A", "Template": ["  "] } ] }""", "模板项不能为空字符串")]
    [InlineData("""{ "Tools": [ { "Name": "A", "Template": ["x", "  "] } ] }""", "模板项不能为空字符串")]
    [InlineData("""{ "Tools": [ { "Name": "A", "Template": ["x"], "Directory": "bad\u0000dir" } ] }""", "Directory bad\u0000dir 无法解析")]
    [InlineData("""{ "Tools": [ { "Name": "A", "Template": ["x"] }, { "Name": "A", "Template": ["y"] } ] }""", "名字重复")]
    [InlineData("""{ "Tools": [ { "Name": "A", "Template": ["x", null] } ] }""", "模板项不能是 null")]
    [InlineData("""{ "Tools": [ { "Name": "A", "Template": ["x"], "Parameters": [null] } ] }""", "参数条目是 null")]
    [InlineData("""{ "Tools": [ null ] }""", "工具条目是 null")]
    [InlineData("""{ "Tools": [ { "Name": "A", "Template": [ { "Text": "x" } ] } ] }""", "OmitWhenMissing")]
    [InlineData("""{ "Tools": [ { "Name": "A", "Template": [ { "OmitWhenMissing": "p" } ], "Parameters": [ { "Name": "p" } ] } ] }""", "Text")]
    [InlineData("""{ "Tools": [ { "Name": "A", "Template": [ { "Text": "x", "OmitWhenMissing": "p", "Extra": "y" } ], "Parameters": [ { "Name": "p" } ] } ] }""", "未知字段")]
    [InlineData("""{ "Tools": [ { "Name": "A", "Template": [ { "Text": "x", "OmitWhenMissing": "missing" } ] } ] }""", "没有对应声明")]
    [InlineData("""{ "Tools": [ { "Name": "A", "Template": [ { "Text": "x", "OmitWhenMissing": "p" } ], "Parameters": [ { "Name": "p" } ] } ] }""", "必须出现")]
    [InlineData("""{ "Tools": [ { "Name": "A", "Template": [ "x", { "Text": "--flag={files}", "OmitWhenMissing": "files" } ], "Parameters": [ { "Name": "files", "List": true } ] } ] }""", "独立的")]
    [InlineData("""{ "Tools": [ { "Name": "A", "Template": [ { "Text": "--flag={files}", "OmitWhenMissing": "files" } ], "Parameters": [ { "Name": "files", "List": true } ] } ] }""", "独立的")]
    [InlineData("""{ "Tools": [ { "Name": "A", "Template": [ 3 ] } ] }""", "必须是字符串或")]
    public void Load_rejects_invalid_definitions(string json, string expected)
    {
        ErrorOr<IReadOnlyList<CommandToolDefinition>> loaded = CommandToolStore.Load(Write(json), Path.GetTempPath());

        Assert.True(loaded.IsError);
        Assert.Contains(loaded.ErrorsOrEmptyList.Select(error => error.Description),
            description => description.Contains(expected, StringComparison.Ordinal));
    }

    [Fact]
    public void Load_rejects_null_tools_list_without_crashing()
    {
        ErrorOr<IReadOnlyList<CommandToolDefinition>> loaded = CommandToolStore.Load(Write("""{ "Tools": null }"""), Path.GetTempPath());

        Assert.False(loaded.IsError);
        Assert.Empty(loaded.Value);
    }

    [Fact]
    public void Command_tool_declares_function_with_list_schema()
    {
        AIFunction function = BuiltAIFunction(["echo", "{files}"], [FileParameter]);

        Assert.Equal("Run", function.Name);
        Assert.Equal("array", function.JsonSchema.GetProperty("properties").GetProperty("files")
            .GetProperty("type").GetString());
    }

    [Fact]
    public void Execute_spreads_list_values_and_skips_empty_missing()
    {
        // 缺参与空列表在必填时都在展开期拒绝，不启动进程
        CommandTool required = Tool(["cmd", "/c", "echo", "{files}"], [RequiredFileParameter]);

        Assert.Equal("被拒绝：缺少参数 files。", Invoke(required, []));
        Assert.Equal("被拒绝：缺少参数 files。",
            Invoke(required, new Dictionary<string, object?> { ["files"] = Array.Empty<string>() }));

        if (!OperatingSystem.IsWindows())
        {
            return;
        }

        // 非必填空列表：占位参数项消失，命令只带周围文字启动
        CommandTool optional = Tool(["cmd", "/c", "echo", "开始", "{files}", "结束"], [FileParameter]);
        string result = Invoke(optional, new Dictionary<string, object?> { ["files"] = Array.Empty<string>() });

        Assert.DoesNotContain("{files}", result);
        Assert.Contains("开始", result);
        Assert.Contains("结束", result);
    }

    [Fact]
    public void Execute_with_list_arguments_spawns_separate_argv_entries()
    {
        if (!OperatingSystem.IsWindows())
        {
            return;
        }

        CommandTool tool = Tool(["cmd", "/c", "echo", "{files}"], [FileParameter]);

        string result = Invoke(tool, new Dictionary<string, object?> { ["files"] = stringArray });

        Assert.Contains("甲 乙", result);
    }

    [Fact]
    public void Execute_treats_array_item_with_spaces_as_single_argument()
    {
        if (!OperatingSystem.IsWindows())
        {
            return;
        }

        // 数组项含空格且无占位符时整体是一个参数项，不做拆分
        string result = Invoke(new CommandTool(
            new CommandToolDefinition(new ToolName("Run"), "说明", Items(["cmd", "/c", "echo", "开始 结束"]), [], null, 5, 200),
            Path.GetTempPath()), []);

        Assert.Contains("开始 结束", result);
        Assert.DoesNotContain("[", result);
        Assert.DoesNotContain("]", result);
    }

    [Fact]
    public void Execute_with_scalar_and_list_arguments_reaches_process()
    {
        if (!OperatingSystem.IsWindows())
        {
            return;
        }

        CommandTool tool = Tool(["cmd", "/c", "echo", "{text}"], [new ToolParameter(new ToolName("text"), "文本", Required: true)]);

        string result = Invoke(tool, new Dictionary<string, object?> { ["text"] = "你好" });

        Assert.Contains("你好", result);
        Assert.Contains("退出码 0", result);
    }

    [Fact]
    public void Execute_treats_scalar_empty_string_as_given_value()
    {
        if (!OperatingSystem.IsWindows())
        {
            return;
        }

        CommandTool tool = Tool(["cmd", "/c", "echo", "{text}"], [new ToolParameter(new ToolName("text"), "文本", Required: true)]);

        string result = Invoke(tool, new Dictionary<string, object?> { ["text"] = "" });

        Assert.DoesNotContain("被拒绝", result);
        Assert.Contains("退出码 0", result);
    }

    [Fact]
    public void Execute_reports_unstartable_process_as_text()
    {
        if (!OperatingSystem.IsWindows())
        {
            return;
        }

        CommandTool tool = Tool(["definitely-not-a-program", "x"], []);

        Assert.Contains("无法启动", Invoke(tool, []));
    }

    [Fact]
    public void Execute_terminates_timed_out_process()
    {
        if (!OperatingSystem.IsWindows())
        {
            return;
        }

        CommandTool tool = new(
            new CommandToolDefinition(new ToolName("Run"), "说明", Items(["cmd", "/c", "ping", "-n", "4", "127.0.0.1"]), [], null, 1, 200),
            Path.GetTempPath());

        Assert.Contains("未结束，已终止", Invoke(tool, []));
    }

    [Fact]
    public void Execute_truncates_output_at_limit()
    {
        if (!OperatingSystem.IsWindows())
        {
            return;
        }

        CommandTool tool = Tool(["cmd", "/c", "echo", new string('A', 300)], []);

        string result = Invoke(tool, []);

        Assert.Contains("输出已截断", result);
    }

    [Fact]
    public void Load_accepts_configured_template_items()
    {
        string file = Write("""
            {
              "Tools": [
                {
                  "Name": "RunCurl",
                  "Template": ["curl", "-f", { "Text": "--app={token}", "OmitWhenMissing": "token" }],
                  "Parameters": [ { "Name": "token" } ]
                }
              ]
            }
            """);

        ErrorOr<IReadOnlyList<CommandToolDefinition>> loaded = CommandToolStore.Load(file, Path.GetTempPath());
        Assert.False(loaded.IsError);

        CommandToolTemplateItem configured = loaded.Value[0].Template[2];
        Assert.Equal("--app={token}", configured.Text);
        Assert.Equal("token", configured.OmitWhenMissing);
    }

    [Fact]
    public void Load_accepts_case_insensitive_template_item_field_names()
    {
        string file = Write("""
            {
              "Tools": [
                {
                  "Name": "A",
                  "Template": [ { "text": "--{p}", "omitWhenMissing": "p" } ],
                  "Parameters": [ { "Name": "p" } ]
                }
              ]
            }
            """);

        ErrorOr<IReadOnlyList<CommandToolDefinition>> loaded = CommandToolStore.Load(file, Path.GetTempPath());

        Assert.False(loaded.IsError);
        CommandToolTemplateItem item = loaded.Value[0].Template[0];
        Assert.Equal("--{p}", item.Text);
        Assert.Equal("p", item.OmitWhenMissing);
    }

    [Fact]
    public void Execute_omits_configured_item_when_linked_missing()
    {
        if (!OperatingSystem.IsWindows())
        {
            return;
        }

        CommandTool tool = ConfiguredTool(
        [
            new CommandToolTemplateItem("cmd", null),
            new CommandToolTemplateItem("/c", null),
            new CommandToolTemplateItem("echo", null),
            new CommandToolTemplateItem("开始", null),
            new CommandToolTemplateItem("--app={token}", "token"),
            new CommandToolTemplateItem("结束", null),
        ], [new ToolParameter(new ToolName("token"), "令牌")]);

        string missing = Invoke(tool, []);
        Assert.DoesNotContain("app", missing);
        Assert.Contains("开始", missing);
        Assert.Contains("结束", missing);

        string given = Invoke(tool, new Dictionary<string, object?> { ["token"] = "x" });
        Assert.Contains("开始 --app=x 结束", given);
    }

    [Fact]
    public void Execute_rejects_configured_item_when_linked_required_missing()
    {
        CommandTool tool = ConfiguredTool(
        [
            new CommandToolTemplateItem("cmd", null),
            new CommandToolTemplateItem("/c", null),
            new CommandToolTemplateItem("echo", null),
            new CommandToolTemplateItem("--app={token}", "token"),
        ], [new ToolParameter(new ToolName("token"), "令牌", Required: true)]);

        Assert.Equal("被拒绝：缺少参数 token。", Invoke(tool, []));
    }

    [Fact]
    public void Execute_keeps_literal_placeholder_when_optional_inline_missing()
    {
        if (!OperatingSystem.IsWindows())
        {
            return;
        }

        CommandTool tool = ConfiguredTool(
        [
            new CommandToolTemplateItem("cmd", null),
            new CommandToolTemplateItem("/c", null),
            new CommandToolTemplateItem("echo", null),
            new CommandToolTemplateItem("--app={token}", null),
        ], [new ToolParameter(new ToolName("token"), "令牌")]);

        string result = Invoke(tool, []);

        Assert.DoesNotContain("被拒绝", result);
        Assert.Contains("--app={token}", result);
    }

    [Fact]
    public void Execute_keeps_missing_optional_placeholder_alongside_given_ones()
    {
        if (!OperatingSystem.IsWindows())
        {
            return;
        }

        CommandTool tool = ConfiguredTool(
        [
            new CommandToolTemplateItem("cmd", null),
            new CommandToolTemplateItem("/c", null),
            new CommandToolTemplateItem("echo", null),
            new CommandToolTemplateItem("--user={user}:{pass}", null),
        ], [new ToolParameter(new ToolName("user"), "用户"), new ToolParameter(new ToolName("pass"), "口令")]);

        string result = Invoke(tool, new Dictionary<string, object?> { ["user"] = "alice" });

        Assert.Contains("--user=alice:{pass}", result);
    }

    [Fact]
    public void Execute_spreads_configured_list_item_and_omits_when_empty()
    {
        if (!OperatingSystem.IsWindows())
        {
            return;
        }

        CommandTool tool = ConfiguredTool(
        [
            new CommandToolTemplateItem("cmd", null),
            new CommandToolTemplateItem("/c", null),
            new CommandToolTemplateItem("echo", null),
            new CommandToolTemplateItem("{files}", "files"),
        ], [new ToolParameter(new ToolName("files"), "文件列表", List: true)]);

        string missing = Invoke(tool, new Dictionary<string, object?> { ["files"] = Array.Empty<string>() });
        Assert.DoesNotContain("甲", missing);
        Assert.Contains("退出码 0", missing);

        string given = Invoke(tool, new Dictionary<string, object?> { ["files"] = stringArray });
        Assert.Contains("甲 乙", given);
    }

    /// <summary>经 ToolCollection 构建成模型可调用的函数，schema 在此生成。</summary>
    private static AIFunction BuiltAIFunction(IReadOnlyList<string> template, IReadOnlyList<ToolParameter> parameters)
    {
        CommandTool tool = Tool(template, parameters);
        TurnScope scope = Scope();
        AITool built = Assert.Single(new ToolCollection([tool]).Build(scope, scope.Sink, null));

        return (AIFunction)built;
    }

    private static string Invoke(CommandTool tool, Dictionary<string, object?> values) =>
        tool.Functions[0].Invoke(new ToolArguments(values));

    private static CommandTool Tool(IReadOnlyList<string> template, IReadOnlyList<ToolParameter> parameters) =>
        new(new CommandToolDefinition(new ToolName("Run"), "说明", Items(template), parameters, null, 5, 200),
            Path.GetTempPath());

    private static CommandTool ConfiguredTool(IReadOnlyList<CommandToolTemplateItem> template, IReadOnlyList<ToolParameter> parameters) =>
        new(new CommandToolDefinition(new ToolName("Run"), "说明", template, parameters, null, 5, 200),
            Path.GetTempPath());

    private static IReadOnlyList<CommandToolTemplateItem> Items(IEnumerable<string> texts) =>
        [.. texts.Select(text => new CommandToolTemplateItem(text, null))];

    private static readonly ToolParameter FileParameter = new(new ToolName("files"), "文件列表", List: true);

    private static readonly ToolParameter RequiredFileParameter = new(new ToolName("files"), "文件列表", Required: true, List: true);
    private static readonly string[] stringArray = ["甲", "乙"];

    private static TurnScope Scope() => new()
    {
        Task = new TaskId(1),
        Run = new RunId(1),
        Output = null,
        NodeName = NodeName.Dialogue,
        ItemIndex = null,
        Journal = new TurnJournal(),
        Sink = new NullSink(),
    };

    private sealed class NullSink : ITurnSink
    {
        public void OnText(string delta)
        {
        }

        public void OnToolCall(ToolCallRecord record)
        {
        }
    }

    private static string Write(string json)
    {
        string file = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N") + ".json");
        File.WriteAllText(file, json);

        return file;
    }
}
