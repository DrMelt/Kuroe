using ErrorOr;
using Kuroe.TestSupport;
using Kuroe.Host.Ability.Tool.Json;
using Xunit;

namespace Kuroe.Host.Ability.Tool.Implementation.Command.Json.Tests;

/// <summary>命令工具文件：读入与装配校验，条目不合法时一次给出全部错误。</summary>
public sealed class CommandToolFileTests
{
    private static readonly string[] values = ["src/secret/token.txt"];

    private readonly InMemoryWorkspaceFiles _files = new();
    private readonly CommandToolFile _toolFile;

    public CommandToolFileTests() => _toolFile = new CommandToolFile(_files);

    [Fact(DisplayName = "文件名固定为 tools.json")]
    public void File_name_is_tools_json() => Assert.Equal("tools.json", _toolFile.FileName);

    [Fact(DisplayName = "文件缺失时加载返回空")]
    public void Load_returns_empty_when_file_missing()
    {
        ErrorOr<IReadOnlyList<ConfiguredTool>> loaded = _toolFile.Load(ConfigDirectory(null), Path.GetTempPath());

        Assert.False(loaded.IsError);
        Assert.Empty(loaded.Value);
    }

    [Fact(DisplayName = "加载接受含标量与列表参数的定义")]
    public void Load_accepts_valid_definitions_with_scalar_and_list_parameters()
    {
        ErrorOr<IReadOnlyList<ConfiguredTool>> loaded = Load("""
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
        Assert.False(loaded.IsError);

        ConfiguredTool diff = loaded.Value[0];
        Assert.Equal(new ToolPath("RunDiff"), diff.Definition.Path);
        Assert.Equal("git diff {path}", diff.Configuration);
        var item = Assert.Single(diff.Definition.Parameters);
        Assert.True(item.Required);

        ConfiguredTool log = loaded.Value[1];
        Assert.Equal("git log --oneline -- {files}", log.Configuration);
        Assert.True(log.Definition.Parameters[0].List);
        Assert.False(log.Definition.Parameters[0].Required);
    }

    [Fact(DisplayName = "加载接受组成完整路径的路径组")]
    public void Load_accepts_path_group_forming_full_path()
    {
        ErrorOr<IReadOnlyList<ConfiguredTool>> loaded = Load("""
            {
              "Tools": [
                { "Name": "RunDiff", "Group": "git", "Template": ["git", "diff", "{path}"], "Parameters": [ { "Name": "path", "Required": true } ] }
              ]
            }
            """);
        Assert.False(loaded.IsError);

        ConfiguredTool tool = loaded.Value[0];
        Assert.Equal(new ToolPath("git/RunDiff"), tool.Definition.Path);
    }

    [Fact(DisplayName = "加载接受不同组中的同名工具")]
    public void Load_accepts_the_same_name_in_different_groups()
    {
        ErrorOr<IReadOnlyList<ConfiguredTool>> loaded = Load("""
            {
              "Tools": [
                { "Name": "RunDiff", "Group": "git", "Template": ["git", "diff"] },
                { "Name": "RunDiff", "Group": "hg", "Template": ["hg", "diff"] }
              ]
            }
            """);

        Assert.False(loaded.IsError);
        Assert.Equal(
            [new ToolPath("git/RunDiff"), new ToolPath("hg/RunDiff")],
            loaded.Value.Select(tool => tool.Definition.Path));
    }

    [Fact(DisplayName = "加载接受配置的模板项")]
    public void Load_accepts_configured_template_items()
    {
        ErrorOr<IReadOnlyList<ConfiguredTool>> loaded = Load("""
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
        Assert.False(loaded.IsError);

        Assert.Equal("curl -f --app={token}?", loaded.Value[0].Configuration);
    }

    [Fact(DisplayName = "加载接受不区分大小写的模板项字段名")]
    public void Load_accepts_case_insensitive_template_item_field_names()
    {
        ErrorOr<IReadOnlyList<ConfiguredTool>> loaded = Load("""
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

        Assert.False(loaded.IsError);
        Assert.Equal("--{p}?", loaded.Value[0].Configuration);
    }

    [Fact(DisplayName = "加载接受内嵌列表参数")]
    public void Load_accepts_embedded_list_parameter()
    {
        ErrorOr<IReadOnlyList<ConfiguredTool>> loaded = Load("""
            {
              "Tools": [
                {
                  "Name": "RunDiff",
                  "Template": ["git", "diff", "--", ".", ":(exclude){excludePaths}"],
                  "Parameters": [ { "Name": "excludePaths", "List": true, "Path": true } ]
                }
              ]
            }
            """);

        Assert.False(loaded.IsError);
        Assert.Equal("git diff -- . :(exclude){excludePaths}", loaded.Value[0].Configuration);
    }

    [Fact(DisplayName = "加载接受关联选项之后的值")]
    public void Load_accepts_value_after_linked_option()
    {
        ErrorOr<IReadOnlyList<ConfiguredTool>> loaded = Load("""
            {
              "Tools": [
                {
                  "Name": "RunCommit",
                  "Template": ["git", "commit", { "Text": "-m", "OmitWhenMissing": "message" }, "{message}"],
                  "Parameters": [ { "Name": "message", "Required": true } ]
                }
              ]
            }
            """);

        Assert.False(loaded.IsError);
        Assert.True(loaded.Value[0].Definition.Parameters[0].Required);
    }

    [Fact(DisplayName = "加载接受允许前导短横线的参数")]
    public void Load_accepts_parameter_allowing_leading_dash()
    {
        ErrorOr<IReadOnlyList<ConfiguredTool>> loaded = Load("""
            {
              "Tools": [
                {
                  "Name": "Run",
                  "Template": ["cmd", "{value}"],
                  "Parameters": [ { "Name": "value", "AllowLeadingDash": true } ]
                }
              ]
            }
            """);

        Assert.False(loaded.IsError);
        Assert.True(loaded.Value[0].Definition.Parameters[0].AllowLeadingDash);
    }

    [Fact(DisplayName = "加载接受关联布尔与列表参数的固定文本项")]
    public void Load_accepts_fixed_text_items_linked_to_flag_and_list_parameters()
    {
        ErrorOr<IReadOnlyList<ConfiguredTool>> loaded = Load("""
            {
              "Tools": [
                {
                  "Name": "RunDiff",
                  "Template": ["git", "diff", { "Text": "--staged", "OmitWhenMissing": "staged" }, "--", { "Text": "--", "OmitWhenMissing": "files" }, "{files}"],
                  "Parameters": [
                    { "Name": "staged", "Flag": true },
                    { "Name": "files", "List": true }
                  ]
                }
              ]
            }
            """);

        Assert.False(loaded.IsError);
        Assert.Equal("git diff --staged? -- --? {files}", loaded.Value[0].Configuration);
        Assert.True(loaded.Value[0].Definition.Parameters[0].Flag);
        Assert.True(loaded.Value[0].Definition.Parameters[1].List);
    }

    [Fact(DisplayName = "加载接受带访问列表的路径参数")]
    public void Load_accepts_path_parameter_with_access_lists()
    {
        ErrorOr<IReadOnlyList<ConfiguredTool>> loaded = Load("""
            {
              "Tools": [
                {
                  "Name": "RunDiff",
                  "Template": ["git", "diff", "--", "{paths}"],
                  "Parameters": [
                    { "Name": "paths", "List": true, "Path": true, "Allow": ["src", "docs"], "Deny": ["src/secret"] }
                  ]
                }
              ]
            }
            """);

        Assert.False(loaded.IsError);
        Assert.True(loaded.Value[0].Definition.Parameters[0].List);

        ITool tool = loaded.Value[0].Create(ToolBinding.Root(Path.GetTempPath()));
        ErrorOr<string> denied = tool.Functions[0].Invoke(
            new ToolArguments(new Dictionary<string, object?> { ["paths"] = values }));

        Assert.True(denied.IsError);
        Assert.Contains("黑名单", denied.FirstError.Description, StringComparison.Ordinal);
    }

    [Fact(DisplayName = "加载接受不带访问列表的路径参数")]
    public void Load_accepts_path_parameter_without_access_lists()
    {
        ErrorOr<IReadOnlyList<ConfiguredTool>> loaded = Load("""
            {
              "Tools": [
                { "Name": "A", "Template": ["git", "diff", "{path}"], "Parameters": [ { "Name": "path", "Path": true } ] }
              ]
            }
            """);

        Assert.False(loaded.IsError);
    }

    [Fact(DisplayName = "加载把目录占位符加入路径变量")]
    public void Load_adds_directory_placeholders_to_the_path_variables()
    {
        ErrorOr<IReadOnlyList<ConfiguredTool>> loaded = Load("""
            {
              "Tools": [
                { "Name": "A", "Template": ["x"], "Directory": "{root}/src" }
              ]
            }
            """);

        Assert.False(loaded.IsError);
        Assert.Equal(["root"], loaded.Value[0].Definition.PathVariables);
    }

    [Fact(DisplayName = "加载接受声明的目录变量且只保留一次")]
    public void Load_accepts_a_declared_directory_variable_and_keeps_it_once()
    {
        ErrorOr<IReadOnlyList<ConfiguredTool>> loaded = Load("""
            {
              "Tools": [
                { "Name": "A", "Template": ["x"], "Variables": ["root"], "Directory": "{root}/src" }
              ]
            }
            """);

        Assert.False(loaded.IsError);
        Assert.Equal(["root"], loaded.Value[0].Definition.PathVariables);
    }

    [Fact(DisplayName = "加载接受执行目录位于工作目录内的路径参数")]
    public void Load_accepts_path_parameter_with_execution_directory_inside_work_directory()
    {
        ErrorOr<IReadOnlyList<ConfiguredTool>> loaded = Load("""
            {
              "Tools": [
                {
                  "Name": "A",
                  "Directory": "src",
                  "Template": ["git", "diff", "{path}"],
                  "Parameters": [ { "Name": "path", "Path": true } ]
                }
              ]
            }
            """);

        Assert.False(loaded.IsError);
    }

    [Theory]
    [InlineData("""{ "Tools": [ { "Name": "A", "Template": ["x"], "Parameters": [ { "Name": "p", "Flag": true, "Path": true } ] } ] }""", "不能同时是布尔与路径", Label = "不能同时是布尔与路径")]
    [InlineData("""{ "Tools": [ { "Name": "A", "Template": ["x", "{p}"], "Parameters": [ { "Name": "p", "Allow": ["src"] } ] } ] }""", "不是路径参数", Label = "不是路径参数")]
    [InlineData("""{ "Tools": [ { "Name": "A", "Template": ["x", "{p}"], "Parameters": [ { "Name": "p", "Path": true, "Allow": [""] } ] } ] }""", "Allow 条目不能为空", Label = "Allow 条目不能为空")]
    [InlineData("""{ "Tools": [ { "Name": "A", "Template": ["x", "{p}"], "Parameters": [ { "Name": "p", "Path": true, "Deny": ["../outside"] } ] } ] }""", "不在工作目录内", Label = "不在工作目录内")]
    [InlineData("""{ "Tools": [ { "Name": "A", "Template": ["x", "{p}"], "Parameters": [ { "Name": "p", "Path": true, "Allow": ["bad\u0000dir"] } ] } ] }""", "无法解析为路径", Label = "无法解析为路径")]
    [InlineData("""{ "Tools": [ { "Name": "A", "Directory": "..", "Template": ["x", "{p}"], "Parameters": [ { "Name": "p", "Path": true } ] } ] }""", "必须在工作目录内", Label = "必须在工作目录内")]
    [InlineData("""{ "Tools": [ { "Name": "A", "Directory": "..", "Template": ["x"] } ] }""", "必须在工作目录内", Label = "必须在工作目录内")]
    [InlineData("""{ "Tools": [ { "Name": "A", "Template": ["x", "{missing}"] } ] }""", "没有对应的参数或路径变量声明", Label = "没有对应的参数或路径变量声明")]
    [InlineData("""{ "Tools": [ { "Name": "A", "Template": ["x"], "Variables": ["extra"] } ] }""", "没有被模板或 Directory 引用", Label = "没有被模板或 Directory 引用")]
    [InlineData("""{ "Tools": [ { "Name": "A", "Template": ["x", "{p}"], "Directory": "{p}/src", "Parameters": [ { "Name": "p" } ] } ] }""", "路径变量 p 与参数同名", Label = "路径变量 p 与参数同名")]
    [InlineData("""{ "Tools": [ { "Name": "A", "Template": ["x"], "Parameters": [ { "Name": "p" } ] } ] }""", "没有出现在模板里", Label = "没有出现在模板里")]
    [InlineData("""{ "Tools": [ { "Name": "A", "Template": ["x", { "Text": "--flag", "OmitWhenMissing": "flag" }], "Parameters": [ { "Name": "flag", "Flag": true, "AllowLeadingDash": true } ] } ] }""", "不能允许 - 开头的实参", Label = "不能允许 - 开头的实参")]
    [InlineData("""{ "Tools": [ { "Name": "A", "Template": ["x", "{p}"], "Parameters": [ { "Name": "p", "Path": true, "AllowLeadingDash": true } ] } ] }""", "是路径参数", Label = "是路径参数")]
    [InlineData("""{ "Tools": [ { "Name": "A", "Template": [ { "Text": "x", "OmitWhenMissing": "p", "Positional": true } ], "Parameters": [ { "Name": "p" } ] } ] }""", "未知字段", Label = "未知字段")]
    [InlineData("""{ "Tools": [ { "Name": "", "Template": ["x"] } ] }""", "工具名不能为空", Label = "工具名不能为空")]
    [InlineData("""{ "Tools": [ { "Name": "A", "Template": ["x", "{p}"], "Parameters": [ { "Name": "p" }, { "Name": "p" } ] } ] }""", "参数名 p 重复", Label = "参数名 p 重复")]
    [InlineData("""{ "Tools": [ { "Name": "A", "Template": ["x"], "TimeoutSeconds": 0 } ] }""", "TimeoutSeconds 必须是正整数", Label = "TimeoutSeconds 必须是正整数")]
    [InlineData("""{ "Tools": [ { "Name": "A", "Template": ["x"], "TimeoutSeconds": 2147484 } ] }""", "TimeoutSeconds 不能超过", Label = "TimeoutSeconds 不能超过")]
    [InlineData("""{ "Tools": [ { "Name": "A", "Template": [] } ] }""", "模板不能为空", Label = "模板不能为空")]
    [InlineData("""{ "Tools": [ { "Name": "A", "Template": ["  "] } ] }""", "模板项不能为空字符串", Label = "模板项不能为空字符串")]
    [InlineData("""{ "Tools": [ { "Name": "A", "Template": ["x", "  "] } ] }""", "模板项不能为空字符串", Label = "模板项不能为空字符串")]
    [InlineData("""{ "Tools": [ { "Name": "A", "Template": ["x"], "Directory": "bad\u0000dir" } ] }""", "Directory bad\u0000dir 无法解析", Label = "Directory bad\u0000dir 无法解析")]
    [InlineData("""{ "Tools": [ { "Name": "A", "Template": ["x"] }, { "Name": "A", "Template": ["y"] } ] }""", "路径重复", Label = "路径重复")]
    [InlineData("""{ "Tools": [ { "Name": "A", "Template": ["x", null] } ] }""", "模板项不能是 null", Label = "模板项不能是 null")]
    [InlineData("""{ "Tools": [ { "Name": "A", "Template": ["x"], "Parameters": [null] } ] }""", "参数条目是 null", Label = "参数条目是 null")]
    [InlineData("""{ "Tools": [ null ] }""", "工具条目是 null", Label = "工具条目是 null")]
    [InlineData("""{ "Tools": [ { "Name": "A", "Template": [ { "Text": "x" } ] } ] }""", "OmitWhenMissing", Label = "OmitWhenMissing")]
    [InlineData("""{ "Tools": [ { "Name": "A", "Template": [ { "OmitWhenMissing": "p" } ], "Parameters": [ { "Name": "p" } ] } ] }""", "Text", Label = "Text")]
    [InlineData("""{ "Tools": [ { "Name": "A", "Template": [ { "Text": "x", "OmitWhenMissing": "p", "Extra": "y" } ], "Parameters": [ { "Name": "p" } ] } ] }""", "未知字段", Label = "未知字段")]
    [InlineData("""{ "Tools": [ { "Name": "A", "Template": [ { "Text": "x", "OmitWhenMissing": "missing" } ] } ] }""", "没有对应声明", Label = "没有对应声明")]
    [InlineData("""{ "Tools": [ { "Name": "A", "Template": [ { "Text": "{staged}", "OmitWhenMissing": "staged" } ], "Parameters": [ { "Name": "staged", "Flag": true } ] } ] }""", "不能出现在参数项的文字里", Label = "不能出现在参数项的文字里")]
    [InlineData("""{ "Tools": [ { "Name": "A", "Template": ["x", "{staged}"], "Parameters": [ { "Name": "staged", "Flag": true } ] } ] }""", "只能关联参数项", Label = "只能关联参数项")]
    [InlineData("""{ "Tools": [ { "Name": "A", "Template": ["x", { "Text": "--staged", "OmitWhenMissing": "staged" }], "Parameters": [ { "Name": "staged", "Flag": true, "List": true } ] } ] }""", "不能同时是布尔与列表", Label = "不能同时是布尔与列表")]
    [InlineData("""{ "Tools": [ { "Name": "A", "Template": ["x", { "Text": "--staged", "OmitWhenMissing": "staged" }], "Parameters": [ { "Name": "staged", "Flag": true, "Required": true } ] } ] }""", "不能是必填参数", Label = "不能是必填参数")]
    [InlineData("""{ "Tools": [ { "Name": "A", "Template": [ 3 ] } ] }""", "必须是字符串或", Label = "必须是字符串或")]
    [InlineData("""{ "Tools": [ { "Name": "A", "Group": "/git", "Template": ["x"] } ] }""", "不能以 / 开头或结尾", Label = "不能以 / 开头或结尾")]
    [InlineData("""{ "Tools": [ { "Name": "A", "Group": "git/", "Template": ["x"] } ] }""", "段不能为空", Label = "段不能为空")]
    [InlineData("""{ "Tools": [ { "Name": "A", "Group": "git//x", "Template": ["x"] } ] }""", "段不能为空", Label = "段不能为空")]
    [InlineData("""{ "Tools": [ { "Name": "A", "Group": "gi t", "Template": ["x"] } ] }""", "只能写字母、数字、下划线与连字符", Label = "只能写字母、数字、下划线与连字符")]
    [InlineData("""{ "Tools": [ { "Name": "A", "Group": "a{b}", "Template": ["x"] } ] }""", "只能写字母、数字、下划线与连字符", Label = "只能写字母、数字、下划线与连字符")]
    [InlineData("""{ "Tools": [ { "Name": "运行", "Template": ["x"] } ] }""", "只能写字母、数字、下划线与连字符", Label = "只能写字母、数字、下划线与连字符")]
    [InlineData("""{ "Tools": [ { "Name": "A", "Group": " ", "Template": ["x"] } ] }""", "只能写字母、数字、下划线与连字符", Label = "只能写字母、数字、下划线与连字符")]
    public void Load_rejects_invalid_definitions(string json, string expected)
    {
        ErrorOr<IReadOnlyList<ConfiguredTool>> loaded = Load(json);

        Assert.True(loaded.IsError);
        Assert.Contains(loaded.ErrorsOrEmptyList.Select(error => error.Description),
            description => description.Contains(expected, StringComparison.Ordinal));
    }

    [Fact(DisplayName = "加载拒绝空工具列表且不崩溃")]
    public void Load_rejects_null_tools_list_without_crashing()
    {
        ErrorOr<IReadOnlyList<ConfiguredTool>> loaded = Load("""{ "Tools": null }""");

        Assert.False(loaded.IsError);
        Assert.Empty(loaded.Value);
    }

    /// <summary>把 json 写成配置目录下的 tools.json 后装载，配置目录为新建路径。</summary>
    private ErrorOr<IReadOnlyList<ConfiguredTool>> Load(string json) =>
        _toolFile.Load(ConfigDirectory(json), Path.GetTempPath());

    /// <summary>新建配置目录路径，json 非空时写成其中的 tools.json。</summary>
    private string ConfigDirectory(string? json)
    {
        string directory = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N"));

        if (json is not null)
        {
            _files.AddFile(Path.Combine(directory, "tools.json"), json);
        }

        return directory;
    }
}