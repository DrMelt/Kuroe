using ErrorOr;
using Kuroe.Tool.Command;
using Kuroe.Tool.Command.Model;
using Xunit;

namespace Kuroe.Tool.Dotnet.Tests;

/// <summary>内置 dotnet 工具面：十一条定义的路径、模板与参数，以及各自展开出的命令行。全程不起进程。</summary>
public sealed class DotnetToolTests
{
    /// <summary>应用参数实参：两个参数。</summary>
    private static readonly string[] TwoArguments = ["--input", "a.txt"];

    [Fact]
    public void Definitions_cover_read_and_write_groups()
    {
        Assert.Equal(
            [
                "dotnet/read/Info",
                "dotnet/read/ListSdks",
                "dotnet/read/ListPackages",
                "dotnet/read/NugetSources",
                "dotnet/write/Build",
                "dotnet/write/Test",
                "dotnet/write/Run",
                "dotnet/write/Restore",
                "dotnet/write/Format",
                "dotnet/write/Publish",
                "dotnet/write/AddPackage",
            ],
            DotnetTools.Definitions.Select(definition => definition.Definition.Path.Value));
    }

    [Fact]
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
            ],
            Template("dotnet/write/Build"));

        Assert.Equal(
            [new("dotnet", null), new("test", null), new("{project}", null), new("--filter", "filter"), new("{filter}", null)],
            Template("dotnet/write/Test"));

        Assert.Equal(
            [new("dotnet", null), new("run", null), new("{project}", null), new("--", "arguments"), new("{arguments}", null)],
            Template("dotnet/write/Run"));

        Assert.Equal(
            [new("dotnet", null), new("restore", null), new("{project}", null)],
            Template("dotnet/write/Restore"));

        Assert.Equal(
            [new("dotnet", null), new("format", null), new("{project}", null)],
            Template("dotnet/write/Format"));

        Assert.Equal(
            [
                new("dotnet", null),
                new("publish", null),
                new("{project}", null),
                new("--configuration", "configuration"),
                new("{configuration}", null),
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

    [Fact]
    public void Read_without_arguments_expands_to_fixed_command_lines()
    {
        Assert.Equal(["dotnet", "--info"], Argv("dotnet/read/Info", Given()));
        Assert.Equal(["dotnet", "--list-sdks"], Argv("dotnet/read/ListSdks", Given()));
        Assert.Equal(["dotnet", "nuget", "list", "source"], Argv("dotnet/read/NugetSources", Given()));
    }

    [Fact]
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

    [Fact]
    public void Build_carries_project_and_configuration()
    {
        Assert.Equal(["dotnet", "build"], Argv("dotnet/write/Build", Given()));
        Assert.Equal(["dotnet", "build", "src/App"], Argv("dotnet/write/Build", Given(("project", "src/App"))));
        Assert.Equal(
            ["dotnet", "build", "src/App", "--configuration", "Release"],
            Argv("dotnet/write/Build", Given(("project", "src/App"), ("configuration", "Release"))));
        Assert.Equal(
            ["dotnet", "build", "--configuration", "Release"],
            Argv("dotnet/write/Build", Given(("configuration", "Release"))));
    }

    [Fact]
    public void Test_carries_project_and_filter()
    {
        Assert.Equal(["dotnet", "test"], Argv("dotnet/write/Test", Given()));
        Assert.Equal(
            ["dotnet", "test", "Tests/Kuroe.Tests", "--filter", "FullyQualifiedName~Kuroe"],
            Argv("dotnet/write/Test", Given(("project", "Tests/Kuroe.Tests"), ("filter", "FullyQualifiedName~Kuroe"))));
    }

    [Fact]
    public void Run_carries_project_and_application_arguments()
    {
        Assert.Equal(["dotnet", "run"], Argv("dotnet/write/Run", Given()));
        Assert.Equal(["dotnet", "run", "Source/Kuroe.Cli"], Argv("dotnet/write/Run", Given(("project", "Source/Kuroe.Cli"))));
        Assert.Equal(
            ["dotnet", "run", "Source/Kuroe.Cli", "--", "--input", "a.txt"],
            Argv("dotnet/write/Run", Given(("project", "Source/Kuroe.Cli"), ("arguments", TwoArguments))));
        Assert.Equal(
            ["dotnet", "run", "--", "--input", "a.txt"],
            Argv("dotnet/write/Run", Given(("arguments", TwoArguments))));
    }

    [Fact]
    public void Restore_format_and_publish_carry_their_arguments()
    {
        Assert.Equal(["dotnet", "restore"], Argv("dotnet/write/Restore", Given()));
        Assert.Equal(["dotnet", "restore", "Kuroe.slnx"], Argv("dotnet/write/Restore", Given(("project", "Kuroe.slnx"))));

        Assert.Equal(["dotnet", "format"], Argv("dotnet/write/Format", Given()));
        Assert.Equal(["dotnet", "format", "Source/Kuroe"], Argv("dotnet/write/Format", Given(("project", "Source/Kuroe"))));

        Assert.Equal(["dotnet", "publish"], Argv("dotnet/write/Publish", Given()));
        Assert.Equal(
            ["dotnet", "publish", "Source/Kuroe.Cli", "--configuration", "Release"],
            Argv("dotnet/write/Publish", Given(("project", "Source/Kuroe.Cli"), ("configuration", "Release"))));
    }

    [Fact]
    public void Add_package_carries_package_and_optional_version()
    {
        Assert.Equal(
            ["dotnet", "add", "Source/Kuroe.Cli", "package", "Serilog"],
            Argv("dotnet/write/AddPackage", Given(("project", "Source/Kuroe.Cli"), ("package", "Serilog"))));
        Assert.Equal(
            ["dotnet", "add", "package", "Serilog", "--version", "4.0.0"],
            Argv("dotnet/write/AddPackage", Given(("package", "Serilog"), ("version", "4.0.0"))));
        Assert.Equal("缺少参数 package。", Rejected("dotnet/write/AddPackage", Given(("project", "Source/Kuroe.Cli"))));
    }

    [Fact]
    public void Parameters_declare_switches_lists_and_required_values()
    {
        CommandToolDefinition listPackages = Definition("dotnet/read/ListPackages");

        Assert.True(Parameter(listPackages, "outdated").Flag);
        Assert.False(Parameter(listPackages, "project").Required);

        CommandToolDefinition build = Definition("dotnet/write/Build");

        Assert.False(Parameter(build, "project").Required);
        Assert.False(Parameter(build, "configuration").Required);

        CommandToolDefinition run = Definition("dotnet/write/Run");

        Assert.True(Parameter(run, "arguments").List);
        Assert.False(Parameter(run, "arguments").Required);

        CommandToolDefinition addPackage = Definition("dotnet/write/AddPackage");

        Assert.True(Parameter(addPackage, "package").Required);
        Assert.False(Parameter(addPackage, "version").Required);
    }

    [Fact]
    public void Tools_carry_every_definition()
    {
        IReadOnlyList<ITool> tools = DotnetTools.Tools(Path.GetTempPath());

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
        definition.Definition.Parameters.Single(parameter => parameter.Name.Value == name);

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