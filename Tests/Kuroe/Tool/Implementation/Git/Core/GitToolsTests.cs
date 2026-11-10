using ErrorOr;
using Kuroe.Tool.Command;
using Kuroe.Tool.Command.Shared;
using Xunit;

namespace Kuroe.Tool.Git.Tests;

/// <summary>内置 git 工具面：九条定义的路径、模板与参数，以及各自展开出的命令行。全程不起进程。</summary>
public sealed class GitToolsTests
{
    /// <summary>路径实参：一个文件的列表。</summary>
    private static readonly string[] OnePath = ["a.txt"];

    /// <summary>路径实参：两个文件的列表。</summary>
    private static readonly string[] TwoPaths = ["a.txt", "b.txt"];

    /// <summary>排除路径实参：一个目录。</summary>
    private static readonly string[] DocsPath = ["docs"];

    /// <summary>排除路径实参：两个目录。</summary>
    private static readonly string[] TwoExcludes = ["docs", "bin"];
    private static readonly string[] stringArray = [":(exclude)docs"];
    private static readonly string[] stringArray0 = ["-a.txt"];

    [Fact]
    public void Definitions_cover_read_write_and_branch_groups()
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
                "git/write/Restore",
                "git/branch/Switch",
            ],
            GitTools.Definitions.Select(definition => definition.Definition.Path.Value));
    }

    [Fact]
    public void Templates_declare_the_expected_command_lines()
    {
        Assert.Equal(
            [
                new("git", null),
                new("status", null),
                new("--short", null),
                new("--branch", null),
                new("--untracked-files={untracked}", "untracked"),
                new("--", "paths"),
                new("{paths}", null),
            ],
            Template("git/read/Status"));

        Assert.Equal(
            [
                new("git", null),
                new("diff", null),
                new("--staged", "staged"),
                new("--stat", "stat"),
                new("--name-only", "nameOnly"),
                new("--name-status", "nameStatus"),
                new("--diff-filter={diffFilter}", "diffFilter"),
                new("{revision}", null),
                new("--", null),
                new("{paths}", null),
                new(":(exclude){excludePaths}", null),
            ],
            Template("git/read/Diff"));

        Assert.Equal(
            [
                new("git", null),
                new("log", null),
                new("--oneline", null),
                new("--decorate", null),
                new("--max-count={count}", "count"),
                new("--author={author}", "author"),
                new("--grep={grep}", "grep"),
                new("--since={since}", "since"),
                new("--until={until}", "until"),
                new("--stat", "stat"),
                new("--first-parent", "firstParent"),
                new("{revision}", null),
                new("--", null),
                new("{paths}", null),
                new(":(exclude){excludePaths}", null),
            ],
            Template("git/read/Log"));

        Assert.Equal(
            [
                new("git", null),
                new("show", null),
                new("--stat", "stat"),
                new("--first-parent", "firstParent"),
                new("{revision}", null),
                new("--", "paths"),
                new("{paths}", null),
            ],
            Template("git/read/Show"));

        Assert.Equal(
            [
                new("git", null),
                new("branch", null),
                new("--all", null),
                new("--verbose", null),
                new("--list", null),
                new("{pattern}", null),
            ],
            Template("git/read/Branches"));

        Assert.Equal(
            [
                new("git", null),
                new("add", null),
                new("--", null),
                new("{paths}", null),
                new(":(exclude){excludePaths}", null),
            ],
            Template("git/write/Add"));

        Assert.Equal(
            [
                new("git", null),
                new("commit", null),
                new("-m", "message"),
                new("{message}", null),
                new("--", "paths"),
                new("{paths}", null),
                new(":(exclude){excludePaths}", "paths"),
            ],
            Template("git/write/Commit"));

        Assert.Equal(
            [new("git", null), new("switch", null), new("-c", "create"), new("{branch}", null)],
            Template("git/branch/Switch"));

        Assert.Equal(
            [
                new("git", null),
                new("restore", null),
                new("--staged", "staged"),
                new("--", null),
                new("{paths}", null),
                new(":(exclude){excludePaths}", null),
            ],
            Template("git/write/Restore"));
    }

    [Fact]
    public void Read_without_arguments_expands_to_fixed_command_lines()
    {
        Assert.Equal(["git", "status", "--short", "--branch"], Argv("git/read/Status", Given()));
        Assert.Equal(["git", "branch", "--all", "--verbose", "--list"], Argv("git/read/Branches", Given()));
    }

    [Fact]
    public void Status_carries_untracked_and_paths()
    {
        Assert.Equal(
            ["git", "status", "--short", "--branch", "--untracked-files=all", "--", "a.txt"],
            Argv("git/read/Status", Given(("untracked", "all"), ("paths", OnePath))));
    }

    [Fact]
    public void Diff_carries_filters_revision_and_excluded_paths()
    {
        Assert.Equal(["git", "diff", "--"], Argv("git/read/Diff", Given()));
        Assert.Equal(["git", "diff", "--", "a.txt", "b.txt"], Argv("git/read/Diff", Given(("paths", TwoPaths))));
        Assert.Equal(
            ["git", "diff", "--staged", "HEAD~1", "--", "a.txt"],
            Argv("git/read/Diff", Given(("staged", true), ("revision", "HEAD~1"), ("paths", OnePath))));
        Assert.Equal(["git", "diff", "HEAD~1", "--"], Argv("git/read/Diff", Given(("revision", "HEAD~1"))));
        Assert.Equal(["git", "diff", "--"], Argv("git/read/Diff", Given(("staged", false))));
        Assert.Equal(
            ["git", "diff", "--stat", "--name-status", "--diff-filter=M", "HEAD", "--", "a.txt"],
            Argv("git/read/Diff", Given(
                ("stat", true),
                ("nameStatus", true),
                ("diffFilter", "M"),
                ("revision", "HEAD"),
                ("paths", OnePath))));
        Assert.Equal(
            ["git", "diff", "--", "a.txt", ":(exclude)docs", ":(exclude)bin"],
            Argv("git/read/Diff", Given(("paths", OnePath), ("excludePaths", TwoExcludes))));
    }

    [Fact]
    public void Diff_rejects_name_only_and_name_status_together()
    {
        CommandTool tool = new(Definition("git/read/Diff"), Path.GetTempPath());

        ErrorOr<string> result = tool.Functions[0].Invoke(
            new ToolArguments(Given(("nameOnly", true), ("nameStatus", true))));

        Assert.True(result.IsError);
        Assert.Contains("只能给出一个", result.FirstError.Description, StringComparison.Ordinal);
    }

    [Fact]
    public void Log_carries_filters_revision_and_excluded_paths()
    {
        Assert.Equal(
            ["git", "log", "--oneline", "--decorate", "--max-count=20", "--"],
            Argv("git/read/Log", Given(("count", "20"))));
        Assert.Equal(
            ["git", "log", "--oneline", "--decorate", "--max-count=20", "--", "a.txt"],
            Argv("git/read/Log", Given(("count", "20"), ("paths", OnePath))));
        Assert.Equal(
            [
                "git", "log", "--oneline", "--decorate", "--max-count=20",
                "--author=DrMelt", "--grep=工具", "--since=2026-01-01", "--until=2026-12-31",
                "--stat", "--first-parent", "main", "--", "a.txt", ":(exclude)docs", ":(exclude)bin",
            ],
            Argv("git/read/Log", Given(
                ("count", "20"),
                ("author", "DrMelt"),
                ("grep", "工具"),
                ("since", "2026-01-01"),
                ("until", "2026-12-31"),
                ("stat", true),
                ("firstParent", true),
                ("revision", "main"),
                ("paths", OnePath),
                ("excludePaths", TwoExcludes))));
        Assert.Equal("缺少参数 count。", Rejected("git/read/Log", Given()));
    }

    [Fact]
    public void Show_carries_revision_stat_and_paths()
    {
        Assert.Equal(["git", "show", "HEAD"], Argv("git/read/Show", Given(("revision", "HEAD"))));
        Assert.Equal(
            ["git", "show", "--stat", "--first-parent", "HEAD", "--", "a.txt"],
            Argv("git/read/Show", Given(("revision", "HEAD"), ("stat", true), ("firstParent", true), ("paths", OnePath))));
        Assert.Equal("缺少参数 revision。", Rejected("git/read/Show", Given()));
    }

    [Fact]
    public void Branches_carries_pattern()
    {
        Assert.Equal(
            ["git", "branch", "--all", "--verbose", "--list", "main*"],
            Argv("git/read/Branches", Given(("pattern", "main*"))));
    }

    [Fact]
    public void Add_and_restore_carry_required_paths_and_excluded_paths()
    {
        Assert.Equal(["git", "add", "--", "a.txt"], Argv("git/write/Add", Given(("paths", OnePath))));
        Assert.Equal(
            ["git", "add", "--", "a.txt", ":(exclude)docs", ":(exclude)bin"],
            Argv("git/write/Add", Given(("paths", OnePath), ("excludePaths", TwoExcludes))));
        Assert.Equal("缺少参数 paths。", Rejected("git/write/Add", Given()));

        Assert.Equal(["git", "restore", "--", "a.txt"], Argv("git/write/Restore", Given(("paths", OnePath))));
        Assert.Equal(
            ["git", "restore", "--staged", "--", "a.txt"],
            Argv("git/write/Restore", Given(("staged", true), ("paths", OnePath))));
        Assert.Equal(
            ["git", "restore", "--", "a.txt", ":(exclude)docs"],
            Argv("git/write/Restore", Given(("paths", OnePath), ("excludePaths", DocsPath))));
        Assert.Equal("缺少参数 paths。", Rejected("git/write/Restore", Given()));
    }

    [Fact]
    public void Commit_carries_message_optional_paths_and_excluded_paths()
    {
        // excludePaths 只在给出 paths 时生效，单独给出时排除项不进入命令
        Assert.Equal(
            ["git", "commit", "-m", "首次提交"],
            Argv("git/write/Commit", Given(("message", "首次提交"))));
        Assert.Equal(
            ["git", "commit", "-m", "首次提交"],
            Argv("git/write/Commit", Given(("message", "首次提交"), ("excludePaths", DocsPath))));
        Assert.Equal(
            ["git", "commit", "-m", "首次提交"],
            Argv("git/write/Commit", Given(("message", "首次提交"), ("paths", Array.Empty<string>()), ("excludePaths", DocsPath))));
        Assert.Equal(
            ["git", "commit", "-m", "首次提交", "--", "a.txt"],
            Argv("git/write/Commit", Given(("message", "首次提交"), ("paths", OnePath))));
        Assert.Equal(
            ["git", "commit", "-m", "首次提交", "--", "a.txt", ":(exclude)docs"],
            Argv("git/write/Commit", Given(("message", "首次提交"), ("paths", OnePath), ("excludePaths", DocsPath))));
        Assert.Equal("缺少参数 message。", Rejected("git/write/Commit", Given()));
    }

    [Fact]
    public void Bare_values_reject_leading_dash()
    {
        Assert.Equal(
            "参数 revision 的值不能以 - 开头。",
            Rejected("git/read/Show", Given(("revision", "--output=x"))));
        Assert.Equal(
            "参数 revision 的值不能以 - 开头。",
            Rejected("git/read/Diff", Given(("revision", "--output=x"))));
        Assert.Equal(
            "参数 branch 的值不能以 - 开头。",
            Rejected("git/branch/Switch", Given(("branch", "--orphan"))));
        Assert.Equal(
            "参数 pattern 的值不能以 - 开头。",
            Rejected("git/read/Branches", Given(("pattern", "--all"))));
    }

    [Fact]
    public void Parameters_declaring_leading_dash_accept_it()
    {
        Assert.Equal(
            ["git", "commit", "-m", "--amend"],
            Argv("git/write/Commit", Given(("message", "--amend"))));
        Assert.Equal(
            ["git", "show", "HEAD", "--", "-a.txt"],
            Argv("git/read/Show", Given(("revision", "HEAD"), ("paths", stringArray0))));
        Assert.True(Parameter(Definition("git/write/Commit"), "paths").AllowLeadingDash);
        Assert.True(Parameter(Definition("git/read/Log"), "excludePaths").AllowLeadingDash);
        Assert.False(Parameter(Definition("git/read/Diff"), "revision").AllowLeadingDash);
        Assert.False(Parameter(Definition("git/branch/Switch"), "branch").AllowLeadingDash);
    }

    [Fact]
    public void Path_magic_is_rejected_before_expanding()
    {
        CommandTool tool = new(Definition("git/read/Diff"), Path.GetTempPath());

        ErrorOr<string> result = tool.Functions[0].Invoke(
            new ToolArguments(Given(("paths", stringArray))));

        Assert.True(result.IsError);
        Assert.Equal("路径参数 paths 不能以 : 开头。", result.FirstError.Description);
    }

    [Fact]
    public void Switch_carries_branch_and_optional_create()
    {
        Assert.Equal(["git", "switch", "topic"], Argv("git/branch/Switch", Given(("branch", "topic"))));
        Assert.Equal(["git", "switch", "topic"], Argv("git/branch/Switch", Given(("branch", "topic"), ("create", false))));
        Assert.Equal(["git", "switch", "-c", "topic"], Argv("git/branch/Switch", Given(("branch", "topic"), ("create", true))));
        Assert.Equal("缺少参数 branch。", Rejected("git/branch/Switch", Given()));
    }

    [Fact]
    public void Parameters_declare_switches_lists_and_required_values()
    {
        CommandToolDefinition diff = Definition("git/read/Diff");

        Assert.True(Parameter(diff, "staged").Flag);
        Assert.True(Parameter(diff, "stat").Flag);
        Assert.True(Parameter(diff, "nameOnly").Flag);
        Assert.True(Parameter(diff, "nameStatus").Flag);
        Assert.False(Parameter(diff, "diffFilter").Required);
        Assert.True(Parameter(diff, "paths").List);
        Assert.True(Parameter(diff, "excludePaths").List);
        Assert.False(Parameter(diff, "revision").Required);

        CommandToolDefinition log = Definition("git/read/Log");

        Assert.True(Parameter(log, "count").Required);
        Assert.True(Parameter(log, "stat").Flag);
        Assert.True(Parameter(log, "firstParent").Flag);
        Assert.False(Parameter(log, "author").Required);
        Assert.False(Parameter(log, "grep").Required);
        Assert.False(Parameter(log, "since").Required);
        Assert.False(Parameter(log, "until").Required);
        Assert.True(Parameter(log, "paths").List);
        Assert.True(Parameter(log, "excludePaths").List);

        CommandToolDefinition show = Definition("git/read/Show");

        Assert.True(Parameter(show, "stat").Flag);
        Assert.True(Parameter(show, "firstParent").Flag);
        Assert.True(Parameter(show, "paths").List);

        CommandToolDefinition status = Definition("git/read/Status");

        Assert.False(Parameter(status, "untracked").Required);
        Assert.True(Parameter(status, "paths").List);

        Assert.False(Parameter(Definition("git/read/Branches"), "pattern").Required);

        CommandToolDefinition commit = Definition("git/write/Commit");

        Assert.True(Parameter(commit, "message").Required);
        Assert.True(Parameter(commit, "paths").List);
        Assert.False(Parameter(commit, "paths").Required);
        Assert.True(Parameter(commit, "excludePaths").List);

        Assert.True(Parameter(Definition("git/write/Add"), "paths").Required);
        Assert.True(Parameter(Definition("git/write/Add"), "excludePaths").List);
        Assert.True(Parameter(Definition("git/write/Restore"), "staged").Flag);
        Assert.True(Parameter(Definition("git/write/Restore"), "excludePaths").List);
        Assert.True(Parameter(Definition("git/branch/Switch"), "create").Flag);
        Assert.True(Parameter(Definition("git/branch/Switch"), "branch").Required);
    }

    [Fact]
    public void Path_values_are_declared_as_path_parameters()
    {
        Assert.All(
            [
                ("git/read/Diff", "paths"),
                ("git/read/Diff", "excludePaths"),
                ("git/read/Log", "paths"),
                ("git/read/Log", "excludePaths"),
                ("git/read/Show", "paths"),
                ("git/read/Status", "paths"),
                ("git/write/Add", "paths"),
                ("git/write/Add", "excludePaths"),
                ("git/write/Commit", "paths"),
                ("git/write/Commit", "excludePaths"),
                ("git/write/Restore", "paths"),
                ("git/write/Restore", "excludePaths"),
            ],
            entry => Assert.True(Parameter(Definition(entry.Item1), entry.Item2).Path));

        Assert.False(Parameter(Definition("git/read/Show"), "revision").Path);
        Assert.False(Parameter(Definition("git/read/Log"), "author").Path);
        Assert.False(Parameter(Definition("git/branch/Switch"), "branch").Path);
        Assert.False(Parameter(Definition("git/write/Commit"), "message").Path);
    }

    [Fact]
    public void Tools_carry_every_definition()
    {
        IReadOnlyList<ITool> tools =
        [
            .. GitTools.Definitions.Select(definition => new CommandTool(definition, Path.GetTempPath())),
        ];

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