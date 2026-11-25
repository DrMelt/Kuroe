using ErrorOr;
using Kuroe.Host.Ability.Tool.Implementation.Command;
using Kuroe.Host.Ability.Tool.Implementation.Command.Shared;
using Xunit;

namespace Kuroe.Host.Ability.Tool.Implementation.Dotnet.Tests;

/// <summary>内置 dotnet 工具面：十三条定义的路径、模板与参数，以及各自展开出的命令行。全程不起进程。</summary>
public sealed class DotnetToolsTests
{
    [Fact(DisplayName = "定义覆盖读取与写入两组")]
    public void Definitions_cover_read_and_write_groups()
    {
        Assert.Equal(
            [
                "dotnet/read/Info",
                "dotnet/read/ListSdks",
                "dotnet/read/ListPackages",
                "dotnet/read/NugetSources",
                "dotnet/read/VerifyFormat",
                "dotnet/write/Build",
                "dotnet/write/Test",
                "dotnet/write/TestMtp",
                "dotnet/write/Run",
                "dotnet/write/Restore",
                "dotnet/write/Format",
                "dotnet/write/Publish",
                "dotnet/write/AddPackage",
            ],
            DotnetTools.Definitions.Select(definition => definition.Definition.Path.Value));
    }

    [Fact(DisplayName = "dotnet 模板声明预期的命令行")]
    public void Templates_declare_the_expected_command_lines()
    {
        Assert.Equal(
            [new("dotnet", null), new("--info", null)],
            Template("dotnet/read/Info"));

        Assert.Equal(
            [new("dotnet", null), new("--list-sdks", null)],
            Template("dotnet/read/ListSdks"));

        Assert.Equal(
            [new("dotnet", null), new("list", null), new("{project}", null), new("package", null), new("--outdated", "outdated")],
            Template("dotnet/read/ListPackages"));

        Assert.Equal(
            [new("dotnet", null), new("nuget", null), new("list", null), new("source", null)],
            Template("dotnet/read/NugetSources"));

        Assert.Equal(
            [
                new("dotnet", null),
                new("build", null),
                new("{project}", null),
                new("--configuration", "configuration"),
                new("{configuration}", null),
                new("--verbosity", null),
                new("quiet", null),
                new("--nologo", null),
            ],
            Template("dotnet/write/Build"));

        Assert.Equal(
            [
                new("dotnet", null),
                new("test", null),
                new("{project}", null),
                new("--filter", "filter"),
                new("{filter}", null),
                new("--verbosity", null),
                new("quiet", null),
            ],
            Template("dotnet/write/Test"));

        Assert.Equal(
            [
                new("dotnet", null),
                new("test", null),
                new("--project", "project"),
                new("{project}", null),
                new("--solution", "solution"),
                new("{solution}", null),
                new("--ignore-exit-code", null),
                new("8", null),
                new("--verbosity", null),
                new("quiet", null),
                new("--", null),
                new("--report-trx", null),
            ],
            Template("dotnet/write/TestMtp"));

        Assert.Equal(
            [new("dotnet", null), new("run", null), new("{project}", null)],
            Template("dotnet/write/Run"));

        Assert.Equal(
            [new("dotnet", null), new("restore", null), new("{project}", null), new("--nologo", null)],
            Template("dotnet/write/Restore"));

        Assert.Equal(
            [
                new("dotnet", null),
                new("format", null),
                new("{project}", null),
                new("--severity", "severity"),
                new("{severity}", null),
            ],
            Template("dotnet/write/Format"));

        Assert.Equal(
            [
                new("dotnet", null),
                new("format", null),
                new("{project}", null),
                new("--severity", "severity"),
                new("{severity}", null),
                new("--verify-no-changes", null),
            ],
            Template("dotnet/read/VerifyFormat"));

        Assert.Equal(
            [
                new("dotnet", null),
                new("publish", null),
                new("{project}", null),
                new("--configuration", "configuration"),
                new("{configuration}", null),
                new("--nologo", null),
            ],
            Template("dotnet/write/Publish"));

        Assert.Equal(
            [
                new("dotnet", null),
                new("add", null),
                new("{project}", null),
                new("package", null),
                new("{package}", null),
                new("--version", "version"),
                new("{version}", null),
            ],
            Template("dotnet/write/AddPackage"));
    }

    [Fact(DisplayName = "每个函数把返回文本限制在八千字符")]
    public void Every_function_caps_returned_text_at_8000_characters()
    {
        Assert.All(DotnetTools.Definitions, definition => Assert.Equal(8000, definition.OutputLimit));
    }

    [Fact(DisplayName = "dotnet 读取在无参数时展开为固定命令行")]
    public void Read_without_arguments_expands_to_fixed_command_lines()
    {
        Assert.Equal(["dotnet", "--info"], Argv("dotnet/read/Info", Given()));
        Assert.Equal(["dotnet", "--list-sdks"], Argv("dotnet/read/ListSdks", Given()));
        Assert.Equal(["dotnet", "nuget", "list", "source"], Argv("dotnet/read/NugetSources", Given()));
    }

    [Fact(DisplayName = "列出包携带项目与过期选项")]
    public void List_packages_carries_project_and_outdated()
    {
        Assert.Equal(["dotnet", "list", "package"], Argv("dotnet/read/ListPackages", Given()));
        Assert.Equal(["dotnet", "list", "src/App", "package"], Argv("dotnet/read/ListPackages", Given(("project", "src/App"))));
        Assert.Equal(
            ["dotnet", "list", "package", "--outdated"],
            Argv("dotnet/read/ListPackages", Given(("outdated", true))));
        Assert.Equal(
            ["dotnet", "list", "src/App", "package", "--outdated"],
            Argv("dotnet/read/ListPackages", Given(("project", "src/App"), ("outdated", true))));
        Assert.Equal(
            ["dotnet", "list", "src/App", "package"],
            Argv("dotnet/read/ListPackages", Given(("project", "src/App"), ("outdated", false))));
    }

    [Fact(DisplayName = "构建携带项目与配置")]
    public void Build_carries_project_and_configuration()
    {
        Assert.Equal(["dotnet", "build", "--verbosity", "quiet", "--nologo"], Argv("dotnet/write/Build", Given()));
        Assert.Equal(
            ["dotnet", "build", "src/App", "--verbosity", "quiet", "--nologo"],
            Argv("dotnet/write/Build", Given(("project", "src/App"))));
        Assert.Equal(
            ["dotnet", "build", "src/App", "--configuration", "Release", "--verbosity", "quiet", "--nologo"],
            Argv("dotnet/write/Build", Given(("project", "src/App"), ("configuration", "Release"))));
        Assert.Equal(
            ["dotnet", "build", "--configuration", "Release", "--verbosity", "quiet", "--nologo"],
            Argv("dotnet/write/Build", Given(("configuration", "Release"))));
    }

    [Fact(DisplayName = "测试携带项目与过滤条件")]
    public void Test_carries_project_and_filter()
    {
        Assert.Equal(["dotnet", "test", "--verbosity", "quiet"], Argv("dotnet/write/Test", Given()));
        Assert.Equal(
            ["dotnet", "test", "Tests/Kuroe.Tests", "--filter", "FullyQualifiedName~Kuroe", "--verbosity", "quiet"],
            Argv("dotnet/write/Test", Given(("project", "Tests/Kuroe.Tests"), ("filter", "FullyQualifiedName~Kuroe"))));
    }

    [Fact(DisplayName = "MTP 测试携带项目或解决方案并报告 TRX")]
    public void TestMtp_carries_project_or_solution_and_reports_trx()
    {
        Assert.Equal(
            ["dotnet", "test", "--ignore-exit-code", "8", "--verbosity", "quiet", "--", "--report-trx"],
            Argv("dotnet/write/TestMtp", Given()));
        Assert.Equal(
            ["dotnet", "test", "--project", "Tests/Kuroe.Tests", "--ignore-exit-code", "8", "--verbosity", "quiet", "--", "--report-trx"],
            Argv("dotnet/write/TestMtp", Given(("project", "Tests/Kuroe.Tests"))));
        Assert.Equal(
            ["dotnet", "test", "--solution", "Kuroe.slnx", "--ignore-exit-code", "8", "--verbosity", "quiet", "--", "--report-trx"],
            Argv("dotnet/write/TestMtp", Given(("solution", "Kuroe.slnx"))));
    }

    [Fact(DisplayName = "MTP 测试拒绝项目与解决方案同时使用")]
    public void TestMtp_rejects_project_and_solution_together()
    {
        ToolDefinition definition = Definition("dotnet/write/TestMtp").Definition;
        ToolFunction function = definition.Bind(_ => "已运行", new ToolPathScope(Path.GetTempPath(), Path.GetTempPath(), "工作区"));

        ErrorOr<string> both = function.Invoke(
            new ToolArguments(Given(("project", "Tests/Kuroe.Tests"), ("solution", "Kuroe.slnx"))));

        Assert.True(both.IsError);
        Assert.Equal("参数 project、solution 只能给出一个。", both.FirstError.Description);
        Assert.Equal(
            [new ToolName("project"), new ToolName("solution")],
            Assert.Single(definition.ParameterGroups).Parameters);
    }

    [Fact(DisplayName = "运行携带项目")]
    public void Run_carries_project()
    {
        Assert.Equal(["dotnet", "run"], Argv("dotnet/write/Run", Given()));
        Assert.Equal(["dotnet", "run", "Source/Kuroe.Console"], Argv("dotnet/write/Run", Given(("project", "Source/Kuroe.Console"))));
    }

    [Fact(DisplayName = "还原格式化与发布携带各自实参")]
    public void Restore_format_and_publish_carry_their_arguments()
    {
        Assert.Equal(["dotnet", "restore", "--nologo"], Argv("dotnet/write/Restore", Given()));
        Assert.Equal(["dotnet", "restore", "Kuroe.slnx", "--nologo"], Argv("dotnet/write/Restore", Given(("project", "Kuroe.slnx"))));

        Assert.Equal(["dotnet", "format"], Argv("dotnet/write/Format", Given()));
        Assert.Equal(["dotnet", "format", "Source/Kuroe"], Argv("dotnet/write/Format", Given(("project", "Source/Kuroe"))));
        Assert.Equal(
            ["dotnet", "format", "--severity", "info"],
            Argv("dotnet/write/Format", Given(("severity", "info"))));
        Assert.Equal(
            ["dotnet", "format", "Source/Kuroe", "--severity", "warn"],
            Argv("dotnet/write/Format", Given(("project", "Source/Kuroe"), ("severity", "warn"))));

        Assert.Equal(["dotnet", "publish", "--nologo"], Argv("dotnet/write/Publish", Given()));
        Assert.Equal(
            ["dotnet", "publish", "Source/Kuroe.Console", "--configuration", "Release", "--nologo"],
            Argv("dotnet/write/Publish", Given(("project", "Source/Kuroe.Console"), ("configuration", "Release"))));
    }

    [Fact(DisplayName = "验证格式化只检查不修改")]
    public void Verify_format_checks_without_changes()
    {
        Assert.Equal(
            ["dotnet", "format", "--verify-no-changes"],
            Argv("dotnet/read/VerifyFormat", Given()));
        Assert.Equal(
            ["dotnet", "format", "Source/Kuroe", "--verify-no-changes"],
            Argv("dotnet/read/VerifyFormat", Given(("project", "Source/Kuroe"))));
        Assert.Equal(
            ["dotnet", "format", "--severity", "info", "--verify-no-changes"],
            Argv("dotnet/read/VerifyFormat", Given(("severity", "info"))));
        Assert.Equal(
            ["dotnet", "format", "Source/Kuroe", "--severity", "error", "--verify-no-changes"],
            Argv("dotnet/read/VerifyFormat", Given(("project", "Source/Kuroe"), ("severity", "error"))));
    }

    [Fact(DisplayName = "添加包携带包名与可选版本")]
    public void Add_package_carries_package_and_optional_version()
    {
        Assert.Equal(
            ["dotnet", "add", "Source/Kuroe.Console", "package", "Serilog"],
            Argv("dotnet/write/AddPackage", Given(("project", "Source/Kuroe.Console"), ("package", "Serilog"))));
        Assert.Equal(
            ["dotnet", "add", "package", "Serilog", "--version", "4.0.0"],
            Argv("dotnet/write/AddPackage", Given(("package", "Serilog"), ("version", "4.0.0"))));
        Assert.Equal("缺少参数 package。", Rejected("dotnet/write/AddPackage", Given(("project", "Source/Kuroe.Console"))));
    }

    [Fact(DisplayName = "以短横线开头的值被拒绝")]
    public void Values_starting_with_dash_are_rejected()
    {
        Assert.Equal(
            "参数 project 的值不能以 - 开头。",
            Rejected("dotnet/write/Build", Given(("project", "-x"))));
        Assert.Equal(
            "参数 package 的值不能以 - 开头。",
            Rejected("dotnet/write/AddPackage", Given(("package", "-x"))));
        Assert.Equal(
            "参数 configuration 的值不能以 - 开头。",
            Rejected("dotnet/write/Build", Given(("configuration", "-x"))));
        Assert.Equal(
            "参数 project 的值不能以 - 开头。",
            Rejected("dotnet/write/TestMtp", Given(("project", "-x"))));
        Assert.All(
            DotnetTools.Definitions.SelectMany(definition => definition.Parameters),
            parameter => Assert.False(parameter.AllowLeadingDash));
    }

    [Fact(DisplayName = "dotnet 参数声明开关列表与必填值")]
    public void Parameters_declare_switches_lists_and_required_values()
    {
        CommandToolDefinition listPackages = Definition("dotnet/read/ListPackages");

        Assert.True(Parameter(listPackages, "outdated").Flag);
        Assert.False(Parameter(listPackages, "project").Required);

        CommandToolDefinition build = Definition("dotnet/write/Build");

        Assert.False(Parameter(build, "project").Required);
        Assert.False(Parameter(build, "configuration").Required);

        CommandToolDefinition run = Definition("dotnet/write/Run");

        Assert.Equal([new ToolName("project")], run.Parameters.Select(parameter => parameter.Name));

        CommandToolDefinition addPackage = Definition("dotnet/write/AddPackage");

        Assert.True(Parameter(addPackage, "package").Required);
        Assert.False(Parameter(addPackage, "version").Required);
    }

    [Fact(DisplayName = "项目声明为路径参数")]
    public void Project_is_declared_as_a_path_parameter()
    {
        Assert.All(
            [
                "dotnet/read/ListPackages",
                "dotnet/read/VerifyFormat",
                "dotnet/write/Build",
                "dotnet/write/Test",
                "dotnet/write/TestMtp",
                "dotnet/write/Run",
                "dotnet/write/Restore",
                "dotnet/write/Format",
                "dotnet/write/Publish",
                "dotnet/write/AddPackage",
            ],
            path => Assert.True(Parameter(Definition(path), "project").Path));

        Assert.True(Parameter(Definition("dotnet/write/TestMtp"), "solution").Path);

        Assert.False(Parameter(Definition("dotnet/write/Build"), "configuration").Path);
        Assert.False(Parameter(Definition("dotnet/write/Test"), "filter").Path);
        Assert.False(Parameter(Definition("dotnet/write/AddPackage"), "package").Path);
        Assert.False(Parameter(Definition("dotnet/write/Format"), "severity").Required);
        Assert.False(Parameter(Definition("dotnet/read/VerifyFormat"), "severity").Required);
    }

    [Fact(DisplayName = "dotnet 工具携带全部定义")]
    public void Tools_carry_every_definition()
    {
        IReadOnlyList<ITool> tools =
        [
            .. DotnetTools.Definitions.Select(definition => new CommandTool(definition, Path.GetTempPath())),
        ];

        Assert.Equal(DotnetTools.Definitions.Count, tools.Count);
        Assert.All(
            tools.Zip(DotnetTools.Definitions),
            pair => Assert.Equal(pair.Second.Definition.Path, Assert.Single(pair.First.Functions).Definition.Path));
    }

    /// <summary>按完整路径取命令定义。</summary>
    private static CommandToolDefinition Definition(string path) =>
        DotnetTools.Definitions.Single(definition => definition.Definition.Path.Value == path);

    /// <summary>按名字取参数声明。</summary>
    private static ToolParameter Parameter(CommandToolDefinition definition, string name) =>
        definition.Parameters.Single(parameter => parameter.Name.Value == name);

    /// <summary>按完整路径取模板。</summary>
    private static IReadOnlyList<CommandToolTemplateItem> Template(string path) => Definition(path).Template;

    /// <summary>展开得到命令行，被拒时用例失败。</summary>
    private static IReadOnlyList<string> Argv(string path, Dictionary<string, object?> values)
    {
        ErrorOr<IReadOnlyList<string>> argv = Definition(path).CommandLine(new ToolArguments(values));

        Assert.False(argv.IsError);

        return argv.Value;
    }

    /// <summary>展开被拒的错误文本，未拒绝时用例失败。</summary>
    private static string Rejected(string path, Dictionary<string, object?> values)
    {
        ErrorOr<IReadOnlyList<string>> argv = Definition(path).CommandLine(new ToolArguments(values));

        Assert.True(argv.IsError);

        return argv.FirstError.Description;
    }

    /// <summary>一次调用的实参。</summary>
    private static Dictionary<string, object?> Given(params (string Name, object? Value)[] values) =>
        values.ToDictionary(value => value.Name, value => value.Value);
}