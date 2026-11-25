using Xunit;

namespace Kuroe.Host.Platform.Tests;

/// <summary>工作区路径关系判断的纯函数行为。</summary>
public sealed class WorkspacePathTests
{
    private static readonly string Root = Path.GetFullPath(Path.Combine(Path.GetTempPath(), "kuroe-workspace"));

    [Fact(DisplayName = "相同判定忽略末尾目录分隔符")]
    public void IsSame_ignores_trailing_directory_separators()
    {
        Assert.True(WorkspacePath.IsSame(Root, Root + Path.DirectorySeparatorChar));
        Assert.True(WorkspacePath.IsSame(Root + Path.DirectorySeparatorChar, Root));
    }

    [Fact(DisplayName = "相同判定拒绝共有前缀的兄弟路径")]
    public void IsSame_rejects_a_sibling_with_a_common_prefix()
    {
        Assert.False(WorkspacePath.IsSame(Root, Root + "2"));
    }

    [Fact(DisplayName = "相同判定按给定规则比较")]
    public void IsSame_compares_with_the_given_rule()
    {
        string upper = Root.ToUpperInvariant();

        Assert.True(WorkspacePath.IsSame(Root, upper, StringComparison.OrdinalIgnoreCase));
        Assert.False(WorkspacePath.IsSame(Root, upper, StringComparison.Ordinal));
    }

    [Fact(DisplayName = "位于判定排除目录本身")]
    public void IsWithin_excludes_the_directory_itself()
    {
        Assert.False(WorkspacePath.IsWithin(Root, Root));
        Assert.False(WorkspacePath.IsWithin(Root, Root + Path.DirectorySeparatorChar));
    }

    [Fact(DisplayName = "位于判定接受后代并忽略末尾分隔符")]
    public void IsWithin_accepts_a_descendant_and_ignores_trailing_separators()
    {
        string child = Path.Combine(Root, "child");

        Assert.True(WorkspacePath.IsWithin(Root, child));
        Assert.True(WorkspacePath.IsWithin(Root + Path.DirectorySeparatorChar, child));
        Assert.False(WorkspacePath.IsWithin(Root, Root + "2"));
    }
}