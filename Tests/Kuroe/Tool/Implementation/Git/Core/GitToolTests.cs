using ErrorOr;
using Kuroe.Tool.Command;
using Kuroe.Tool.Command.Shared;
using Xunit;

namespace Kuroe.Tool.Git.Tests;

/// <summary>内置 git 工具面：九条定义的路径、模板与参数，以及各自展开出的命令行。全程不起进程。</summary>
public sealed class GitToolTests
{
    /// <summary>路径实参：一个文件的列表。</summary>
    private static readonly string[] OnePath = ["a.txt"];

    /// <summary>路径实参：两个文件的列表。</summary>
    private static readonly string[] TwoPaths = ["a.txt", "b.txt"];

    [Fact]
    public void Definitions_cover_read_and_write_groups()
    {
        Assert.Equal(
            [
                "git/read/Status",
                "git/read/Diff",
                "git/read/Log",
                "git/read/Show",
                "git/read/Branches",
                "git/write/Add",
                "git/write/Commit",
                "git/write/Switch",
                "git/write/Restore",
            ],
            GitTools.Definitions.Select(definition => definition.Definition.Path.Value));
    }

    [Fact]
    public void Templates_declare_the_expected_command_lines()
    {
        Assert.Equal(
            [new("git", null), new("status", null), new("--short", null), new("--branch", null)],
            Template("git/read/Status"));

        Assert.Equal(
            [new("git", null), new("diff", null), new("--staged", "staged"), new("{revision}", null), new("--", "paths"), new("{paths}", null)],
            Template("git/read/Diff"));

        Assert.Equal(
            [
                new("git", null),
                new("log", null),
                new("--oneline", null),
                new("--decorate", null),
                new("--max-count={count}", "count"),
                new("--", "paths"),
                new("{paths}", null),
            ],
            Template("git/read/Log"));

        Assert.Equal(
            [new("git", null), new("show", null), new("{revision}", null)],
            Template("git/read/Show"));

        Assert.Equal(
            [new("git", null), new("branch", null), new("--all", null), new("--verbose", null)],
            Template("git/read/Branches"));

        Assert.Equal(
            [new("git", null), new("add", null), new("--", null), new("{paths}", null)],
            Template("git/write/Add"));

        Assert.Equal(
            [new("git", null), new("commit", null), new("-m", null), new("{message}", null), new("--", "paths"), new("{paths}", null)],
            Template("git/write/Commit"));

        Assert.Equal(
            [new("git", null), new("switch", null), new("-c", "create"), new("{branch}", null)],
            Template("git/write/Switch"));

        Assert.Equal(
            [new("git", null), new("restore", null), new("--staged", "staged"), new("--", null), new("{paths}", null)],
            Template("git/write/Restore"));
    }

    [Fact]
    public void Read_without_arguments_expands_to_fixed_command_lines()
    {
        Assert.Equal(["git", "status", "--short", "--branch"], Argv("git/read/Status", Given()));
        Assert.Equal(["git", "branch", "--all", "--verbose"], Argv("git/read/Branches", Given()));
    }

    [Fact]
    public void Diff_carries_switch_revision_and_paths()
    {
        Assert.Equal(["git", "diff"], Argv("git/read/Diff", Given()));
        Assert.Equal(["git", "diff", "--", "a.txt", "b.txt"], Argv("git/read/Diff", Given(("paths", TwoPaths))));
        Assert.Equal(
            ["git", "diff", "--staged", "HEAD~1", "--", "a.txt"],
            Argv("git/read/Diff", Given(("staged", true), ("revision", "HEAD~1"), ("paths", OnePath))));
        Assert.Equal(["git", "diff", "HEAD~1"], Argv("git/read/Diff", Given(("revision", "HEAD~1"))));
        Assert.Equal(["git", "diff"], Argv("git/read/Diff", Given(("staged", false))));
    }

    [Fact]
    public void Log_carries_count_and_paths()
    {
        Assert.Equal(["git", "log", "--oneline", "--decorate", "--max-count=20"], Argv("git/read/Log", Given(("count", "20"))));
        Assert.Equal(
            ["git", "log", "--oneline", "--decorate", "--max-count=20", "--", "a.txt"],
            Argv("git/read/Log", Given(("count", "20"), ("paths", OnePath))));
        Assert.Equal("缺少参数 count。", Rejected("git/read/Log", Given()));
    }

    [Fact]
    public void Show_carries_revision()
    {
        Assert.Equal(["git", "show", "HEAD"], Argv("git/read/Show", Given(("revision", "HEAD"))));
        Assert.Equal("缺少参数 revision。", Rejected("git/read/Show", Given()));
    }

    [Fact]
    public void Add_and_restore_carry_required_paths()
    {
        Assert.Equal(["git", "add", "--", "a.txt"], Argv("git/write/Add", Given(("paths", OnePath))));
        Assert.Equal("缺少参数 paths。", Rejected("git/write/Add", Given()));

        Assert.Equal(["git", "restore", "--", "a.txt"], Argv("git/write/Restore", Given(("paths", OnePath))));
        Assert.Equal(
            ["git", "restore", "--staged", "--", "a.txt"],
            Argv("git/write/Restore", Given(("staged", true), ("paths", OnePath))));
        Assert.Equal("缺少参数 paths。", Rejected("git/write/Restore", Given()));
    }

    [Fact]
    public void Commit_carries_message_and_optional_paths()
    {
        Assert.Equal(["git", "commit", "-m", "首次提交"], Argv("git/write/Commit", Given(("message", "首次提交"))));
        Assert.Equal(
            ["git", "commit", "-m", "首次提交", "--", "a.txt"],
            Argv("git/write/Commit", Given(("message", "首次提交"), ("paths", OnePath))));
        Assert.Equal("缺少参数 message。", Rejected("git/write/Commit", Given()));
    }

    [Fact]
    public void Switch_carries_branch_and_optional_create()
    {
        Assert.Equal(["git", "switch", "topic"], Argv("git/write/Switch", Given(("branch", "topic"))));
        Assert.Equal(["git", "switch", "topic"], Argv("git/write/Switch", Given(("branch", "topic"), ("create", false))));
        Assert.Equal(["git", "switch", "-c", "topic"], Argv("git/write/Switch", Given(("branch", "topic"), ("create", true))));
        Assert.Equal("缺少参数 branch。", Rejected("git/write/Switch", Given()));
    }

    [Fact]
    public void Parameters_declare_switches_lists_and_required_values()
    {
        CommandToolDefinition diff = Definition("git/read/Diff");

        Assert.True(Parameter(diff, "staged").Flag);
        Assert.True(Parameter(diff, "paths").List);
        Assert.False(Parameter(diff, "revision").Required);

        CommandToolDefinition log = Definition("git/read/Log");

        Assert.True(Parameter(log, "count").Required);
        Assert.True(Parameter(log, "paths").List);

        CommandToolDefinition commit = Definition("git/write/Commit");

        Assert.True(Parameter(commit, "message").Required);
        Assert.True(Parameter(commit, "paths").List);
        Assert.False(Parameter(commit, "paths").Required);

        Assert.True(Parameter(Definition("git/write/Add"), "paths").Required);
        Assert.True(Parameter(Definition("git/write/Restore"), "staged").Flag);
        Assert.True(Parameter(Definition("git/write/Switch"), "create").Flag);
        Assert.True(Parameter(Definition("git/write/Switch"), "branch").Required);
    }

    [Fact]
    public void Path_values_are_declared_as_path_parameters()
    {
        Assert.All(
            [
                ("git/read/Diff", "paths"),
                ("git/read/Log", "paths"),
                ("git/write/Add", "paths"),
                ("git/write/Commit", "paths"),
                ("git/write/Restore", "paths"),
            ],
            entry => Assert.True(CommandParameter(Definition(entry.Item1), entry.Item2).Path));

        Assert.False(CommandParameter(Definition("git/read/Show"), "revision").Path);
        Assert.False(CommandParameter(Definition("git/write/Switch"), "branch").Path);
        Assert.False(CommandParameter(Definition("git/write/Commit"), "message").Path);
    }

    [Fact]
    public void Tools_carry_every_definition()
    {
        IReadOnlyList<ITool> tools = GitTools.Tools(Path.GetTempPath());

        Assert.Equal(GitTools.Definitions.Count, tools.Count);
        Assert.All(
            tools.Zip(GitTools.Definitions),
            pair => Assert.Equal(pair.Second.Definition.Path, Assert.Single(pair.First.Functions).Definition.Path));
    }

    /// <summary>按完整路径取命令定义。</summary>
    private static CommandToolDefinition Definition(string path) =>
        GitTools.Definitions.Single(definition => definition.Definition.Path.Value == path);

    /// <summary>按名字取参数声明。</summary>
    private static ToolParameter Parameter(CommandToolDefinition definition, string name) =>
        definition.Definition.Parameters.Single(parameter => parameter.Name.Value == name);

    /// <summary>按名字取命令参数声明。</summary>
    private static CommandToolParameter CommandParameter(CommandToolDefinition definition, string name) =>
        definition.Parameters.Single(parameter => parameter.Name == name);

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