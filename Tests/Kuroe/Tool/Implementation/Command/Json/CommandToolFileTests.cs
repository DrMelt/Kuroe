using ErrorOr;
using Kuroe.Tool.Json;
using Xunit;

namespace Kuroe.Tool.Command.Json.Tests;

/// <summary>命令工具文件：读入与装配校验，条目不合法时一次给出全部错误。</summary>
public sealed class CommandToolFileTests
{
    private static readonly CommandToolFile ToolFile = new();
    private static readonly string[] values = ["src/secret/token.txt"];

    [Fact]
    public void File_name_is_tools_json() => Assert.Equal("tools.json", ToolFile.FileName);

    [Fact]
    public void Load_returns_empty_when_file_missing()
    {
        ErrorOr<IReadOnlyList<ConfiguredTool>> loaded = ToolFile.Load(ConfigDirectory(null), Path.GetTempPath());

        Assert.False(loaded.IsError);
        Assert.Empty(loaded.Value);
    }

    [Fact]
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
        Assert.Single(diff.Definition.Parameters);
        Assert.True(diff.Definition.Parameters[0].Required);

        ConfiguredTool log = loaded.Value[1];
        Assert.Equal("git log --oneline -- {files}", log.Configuration);
        Assert.True(log.Definition.Parameters[0].List);
        Assert.False(log.Definition.Parameters[0].Required);
    }

    [Fact]
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

    [Fact]
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

    [Fact]
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

    [Fact]
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

    [Fact]
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

    [Fact]
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

    [Fact]
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

    [Fact]
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

    [Fact]
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

    [Fact]
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
    [InlineData("""{ "Tools": [ { "Name": "A", "Template": ["x"], "Parameters": [ { "Name": "p", "Flag": true, "Path": true } ] } ] }""", "不能同时是布尔与路径")]
    [InlineData("""{ "Tools": [ { "Name": "A", "Template": ["x", "{p}"], "Parameters": [ { "Name": "p", "Allow": ["src"] } ] } ] }""", "不是路径参数")]
    [InlineData("""{ "Tools": [ { "Name": "A", "Template": ["x", "{p}"], "Parameters": [ { "Name": "p", "Path": true, "Allow": [""] } ] } ] }""", "Allow 条目不能为空")]
    [InlineData("""{ "Tools": [ { "Name": "A", "Template": ["x", "{p}"], "Parameters": [ { "Name": "p", "Path": true, "Deny": ["../outside"] } ] } ] }""", "不在工作目录内")]
    [InlineData("""{ "Tools": [ { "Name": "A", "Template": ["x", "{p}"], "Parameters": [ { "Name": "p", "Path": true, "Allow": ["bad\u0000dir"] } ] } ] }""", "无法解析为路径")]
    [InlineData("""{ "Tools": [ { "Name": "A", "Directory": "..", "Template": ["x", "{p}"], "Parameters": [ { "Name": "p", "Path": true } ] } ] }""", "必须在工作目录内")]
    [InlineData("""{ "Tools": [ { "Name": "A", "Directory": "..", "Template": ["x"] } ] }""", "必须在工作目录内")]
    [InlineData("""{ "Tools": [ { "Name": "A", "Template": ["x", "{missing}"] } ] }""", "没有对应的参数或路径变量声明")]
    [InlineData("""{ "Tools": [ { "Name": "A", "Template": ["x"], "Variables": ["extra"] } ] }""", "没有被模板或 Directory 引用")]
    [InlineData("""{ "Tools": [ { "Name": "A", "Template": ["x", "{p}"], "Directory": "{p}/src", "Parameters": [ { "Name": "p" } ] } ] }""", "路径变量 p 与参数同名")]
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
    [InlineData("""{ "Tools": [ { "Name": "A", "Template": ["x"] }, { "Name": "A", "Template": ["y"] } ] }""", "路径重复")]
    [InlineData("""{ "Tools": [ { "Name": "A", "Template": ["x", null] } ] }""", "模板项不能是 null")]
    [InlineData("""{ "Tools": [ { "Name": "A", "Template": ["x"], "Parameters": [null] } ] }""", "参数条目是 null")]
    [InlineData("""{ "Tools": [ null ] }""", "工具条目是 null")]
    [InlineData("""{ "Tools": [ { "Name": "A", "Template": [ { "Text": "x" } ] } ] }""", "OmitWhenMissing")]
    [InlineData("""{ "Tools": [ { "Name": "A", "Template": [ { "OmitWhenMissing": "p" } ], "Parameters": [ { "Name": "p" } ] } ] }""", "Text")]
    [InlineData("""{ "Tools": [ { "Name": "A", "Template": [ { "Text": "x", "OmitWhenMissing": "p", "Extra": "y" } ], "Parameters": [ { "Name": "p" } ] } ] }""", "未知字段")]
    [InlineData("""{ "Tools": [ { "Name": "A", "Template": [ { "Text": "x", "OmitWhenMissing": "missing" } ] } ] }""", "没有对应声明")]
    [InlineData("""{ "Tools": [ { "Name": "A", "Template": [ { "Text": "{staged}", "OmitWhenMissing": "staged" } ], "Parameters": [ { "Name": "staged", "Flag": true } ] } ] }""", "不能出现在参数项的文字里")]
    [InlineData("""{ "Tools": [ { "Name": "A", "Template": ["x", "{staged}"], "Parameters": [ { "Name": "staged", "Flag": true } ] } ] }""", "只能关联参数项")]
    [InlineData("""{ "Tools": [ { "Name": "A", "Template": ["x", { "Text": "--staged", "OmitWhenMissing": "staged" }], "Parameters": [ { "Name": "staged", "Flag": true, "List": true } ] } ] }""", "不能同时是布尔与列表")]
    [InlineData("""{ "Tools": [ { "Name": "A", "Template": ["x", { "Text": "--staged", "OmitWhenMissing": "staged" }], "Parameters": [ { "Name": "staged", "Flag": true, "Required": true } ] } ] }""", "不能是必填参数")]
    [InlineData("""{ "Tools": [ { "Name": "A", "Template": [ "x", { "Text": "--flag={files}", "OmitWhenMissing": "files" } ], "Parameters": [ { "Name": "files", "List": true } ] } ] }""", "独立的")]
    [InlineData("""{ "Tools": [ { "Name": "A", "Template": [ { "Text": "--flag={files}", "OmitWhenMissing": "files" } ], "Parameters": [ { "Name": "files", "List": true } ] } ] }""", "独立的")]
    [InlineData("""{ "Tools": [ { "Name": "A", "Template": [ 3 ] } ] }""", "必须是字符串或")]
    [InlineData("""{ "Tools": [ { "Name": "A", "Group": "/git", "Template": ["x"] } ] }""", "不能以 / 开头或结尾")]
    [InlineData("""{ "Tools": [ { "Name": "A", "Group": "git/", "Template": ["x"] } ] }""", "段不能为空")]
    [InlineData("""{ "Tools": [ { "Name": "A", "Group": "git//x", "Template": ["x"] } ] }""", "段不能为空")]
    [InlineData("""{ "Tools": [ { "Name": "A", "Group": "gi t", "Template": ["x"] } ] }""", "只能写字母、数字、下划线与连字符")]
    [InlineData("""{ "Tools": [ { "Name": "A", "Group": "a{b}", "Template": ["x"] } ] }""", "只能写字母、数字、下划线与连字符")]
    [InlineData("""{ "Tools": [ { "Name": "运行", "Template": ["x"] } ] }""", "只能写字母、数字、下划线与连字符")]
    [InlineData("""{ "Tools": [ { "Name": "A", "Group": " ", "Template": ["x"] } ] }""", "只能写字母、数字、下划线与连字符")]
    public void Load_rejects_invalid_definitions(string json, string expected)
    {
        ErrorOr<IReadOnlyList<ConfiguredTool>> loaded = Load(json);

        Assert.True(loaded.IsError);
        Assert.Contains(loaded.ErrorsOrEmptyList.Select(error => error.Description),
            description => description.Contains(expected, StringComparison.Ordinal));
    }

    [Fact]
    public void Load_rejects_null_tools_list_without_crashing()
    {
        ErrorOr<IReadOnlyList<ConfiguredTool>> loaded = Load("""{ "Tools": null }""");

        Assert.False(loaded.IsError);
        Assert.Empty(loaded.Value);
    }

    /// <summary>把 json 写成配置目录下的 tools.json 后装载，配置目录为临时新建。</summary>
    private static ErrorOr<IReadOnlyList<ConfiguredTool>> Load(string json) =>
        ToolFile.Load(ConfigDirectory(json), Path.GetTempPath());

    /// <summary>新建临时配置目录，json 非空时写成其中的 tools.json。</summary>
    private static string ConfigDirectory(string? json)
    {
        string directory = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(directory);

        if (json is not null)
        {
            File.WriteAllText(Path.Combine(directory, "tools.json"), json);
        }

        return directory;
    }
}