using ErrorOr;
using Xunit;

namespace Kuroe.Tool.Tests;

/// <summary>路径参数的解析范围与准入规则。</summary>
public sealed class ToolPathScopeTests
{
    [Fact(DisplayName = "解析接受根目录下的路径")]
    public void Resolve_accepts_path_under_root()
    {
        string root = Root();
        ToolPathScope scope = new(root, root, "工作目录");

        ErrorOr<string> file = scope.Resolve(Path.Combine("src", "App.cs"));

        Assert.False(file.IsError);
        Assert.Equal(Path.Combine(root, "src", "App.cs"), file.Value);
    }

    [Fact(DisplayName = "解析拒绝根目录之外的路径")]
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

    [Fact(DisplayName = "解析保留以点开头的名称")]
    public void Resolve_keeps_names_starting_with_dots()
    {
        string root = Root();
        ToolPathScope scope = new(root, root, "工作目录");

        ErrorOr<string> file = scope.Resolve("..foo.cs");

        Assert.False(file.IsError);
        Assert.Equal(Path.Combine(root, "..foo.cs"), file.Value);
    }

    [Fact(DisplayName = "解析使用基准目录并接受条目本身与子树")]
    public void Resolve_uses_base_directory_and_accepts_entry_itself_and_subtree()
    {
        string root = Root();
        string working = Path.Combine(root, "src");
        ToolPathScope scope = new(working, root, "工作目录");
        ToolPathAccess access = new(["src"], []);

        Assert.False(scope.Resolve("App.cs", access).IsError);
        Assert.False(scope.Resolve(Path.Combine("..", "src", "dir", "App.cs"), access).IsError);
    }

    [Fact(DisplayName = "解析先应用禁止再应用允许")]
    public void Resolve_applies_deny_before_allow()
    {
        string root = Root();
        ToolPathScope scope = new(root, root, "工作目录");
        ToolPathAccess access = new(["src"], [Path.Combine("src", "secret")]);

        ErrorOr<string> denied = scope.Resolve(Path.Combine("src", "secret", "token.txt"), access);

        Assert.True(denied.IsError);
        Assert.Contains("黑名单", denied.FirstError.Description, StringComparison.Ordinal);
    }

    [Fact(DisplayName = "解析拒绝允许列表与名称前缀之外的路径")]
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

    [Fact(DisplayName = "解析在没有允许列表时应用禁止")]
    public void Resolve_applies_deny_without_allow()
    {
        string root = Root();
        ToolPathScope scope = new(root, root, "工作目录");
        ToolPathAccess access = new([], [Path.Combine("docs", "secret")]);

        Assert.False(scope.Resolve(Path.Combine("docs", "readme.md"), access).IsError);
        Assert.True(scope.Resolve(Path.Combine("docs", "secret", "token.txt"), access).IsError);
    }

    [Fact(DisplayName = "展示把空路径写作根")]
    public void Display_names_empty_path_as_root()
    {
        string root = Root();
        ToolPathScope scope = new(root, root, "工作目录");

        Assert.Equal("工作目录根", scope.Display(string.Empty));
        Assert.Equal("src", scope.Display("src"));
    }

    [Fact(DisplayName = "解析按访问比较匹配禁止条目")]
    public void Resolve_matches_deny_entries_with_the_access_comparison()
    {
        string root = Root();
        ToolPathScope scope = new(root, root, "工作目录");
        ToolPathAccess insensitive = new([], [".kuroe"]) { Comparison = StringComparison.OrdinalIgnoreCase };
        ToolPathAccess sensitive = new([], [".kuroe"]) { Comparison = StringComparison.Ordinal };

        ErrorOr<string> denied = scope.Resolve(Path.Combine(".KUROE", "catalog.json"), insensitive);
        ErrorOr<string> allowed = scope.Resolve(Path.Combine(".KUROE", "catalog.json"), sensitive);

        Assert.True(denied.IsError);
        Assert.Contains("黑名单", denied.FirstError.Description, StringComparison.Ordinal);
        Assert.False(allowed.IsError);
    }

    /// <summary>临时测试根目录，路径不需要实际存在。</summary>
    private static string Root() => Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N"));
}