using ErrorOr;
using Xunit;

namespace Kuroe.Tool.Tests;

/// <summary>路径参数的解析范围与准入规则。</summary>
public sealed class ToolPathScopeTests
{
    [Fact]
    public void Resolve_accepts_path_under_root()
    {
        string root = Root();
        ToolPathScope scope = new(root, root, "工作目录");

        ErrorOr<string> file = scope.Resolve(Path.Combine("src", "App.cs"));

        Assert.False(file.IsError);
        Assert.Equal(Path.Combine(root, "src", "App.cs"), file.Value);
    }

    [Fact]
    public void Resolve_rejects_path_outside_root()
    {
        string root = Root();
        ToolPathScope scope = new(root, root, "工作目录");

        ErrorOr<string> escaped = scope.Resolve(Path.Combine("..", "escape.txt"));
        ErrorOr<string> absolute = scope.Resolve(Path.Combine(root, "..", "escape.txt"));

        Assert.True(escaped.IsError);
        Assert.Contains("不在工作目录内", escaped.FirstError.Description, StringComparison.Ordinal);
        Assert.True(absolute.IsError);
    }

    [Fact]
    public void Resolve_keeps_names_starting_with_dots()
    {
        string root = Root();
        ToolPathScope scope = new(root, root, "工作目录");

        ErrorOr<string> file = scope.Resolve("..foo.cs");

        Assert.False(file.IsError);
        Assert.Equal(Path.Combine(root, "..foo.cs"), file.Value);
    }

    [Fact]
    public void Resolve_uses_base_directory_and_accepts_entry_itself_and_subtree()
    {
        string root = Root();
        string working = Path.Combine(root, "src");
        ToolPathScope scope = new(working, root, "工作目录");
        ToolPathAccess access = new(["src"], []);

        Assert.False(scope.Resolve("App.cs", access).IsError);
        Assert.False(scope.Resolve(Path.Combine("..", "src", "dir", "App.cs"), access).IsError);
    }

    [Fact]
    public void Resolve_applies_deny_before_allow()
    {
        string root = Root();
        ToolPathScope scope = new(root, root, "工作目录");
        ToolPathAccess access = new(["src"], [Path.Combine("src", "secret")]);

        ErrorOr<string> denied = scope.Resolve(Path.Combine("src", "secret", "token.txt"), access);

        Assert.True(denied.IsError);
        Assert.Contains("黑名单", denied.FirstError.Description, StringComparison.Ordinal);
    }

    [Fact]
    public void Resolve_rejects_path_outside_allow_list_and_name_prefix()
    {
        string root = Root();
        ToolPathScope scope = new(root, root, "工作目录");
        ToolPathAccess access = new(["src"], []);

        ErrorOr<string> outside = scope.Resolve(Path.Combine("docs", "readme.md"), access);
        ErrorOr<string> prefix = scope.Resolve(Path.Combine("src2", "readme.md"), access);

        Assert.True(outside.IsError);
        Assert.Contains("白名单", outside.FirstError.Description, StringComparison.Ordinal);
        Assert.True(prefix.IsError);
    }

    [Fact]
    public void Resolve_applies_deny_without_allow()
    {
        string root = Root();
        ToolPathScope scope = new(root, root, "工作目录");
        ToolPathAccess access = new([], [Path.Combine("docs", "secret")]);

        Assert.False(scope.Resolve(Path.Combine("docs", "readme.md"), access).IsError);
        Assert.True(scope.Resolve(Path.Combine("docs", "secret", "token.txt"), access).IsError);
    }

    [Fact]
    public void Display_names_empty_path_as_root()
    {
        string root = Root();
        ToolPathScope scope = new(root, root, "工作目录");

        Assert.Equal("工作目录根", scope.Display(string.Empty));
        Assert.Equal("src", scope.Display("src"));
    }

    /// <summary>临时测试根目录，路径不需要实际存在。</summary>
    private static string Root() => Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N"));
}