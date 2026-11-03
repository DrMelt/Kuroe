using Xunit;

namespace Kuroe.Shared.Tests;

/// <summary>工作区路径关系判断的纯函数行为。</summary>
public sealed class WorkspacePathTests
{
    private static readonly string Root = Path.GetFullPath(Path.Combine(Path.GetTempPath(), "kuroe-workspace"));

    [Fact]
    public void IsSame_ignores_trailing_directory_separators()
    {
        Assert.True(WorkspacePath.IsSame(Root, Root + Path.DirectorySeparatorChar));
        Assert.True(WorkspacePath.IsSame(Root + Path.DirectorySeparatorChar, Root));
    }

    [Fact]
    public void IsSame_rejects_a_sibling_with_a_common_prefix()
    {
        Assert.False(WorkspacePath.IsSame(Root, Root + "2"));
    }

    [Fact]
    public void IsSame_compares_case_by_platform_rules()
    {
        Assert.Equal(OperatingSystem.IsWindows(), WorkspacePath.IsSame(Root, Root.ToUpperInvariant()));
    }

    [Fact]
    public void IsWithin_excludes_the_directory_itself()
    {
        Assert.False(WorkspacePath.IsWithin(Root, Root));
        Assert.False(WorkspacePath.IsWithin(Root, Root + Path.DirectorySeparatorChar));
    }

    [Fact]
    public void IsWithin_accepts_a_descendant_and_ignores_trailing_separators()
    {
        string child = Path.Combine(Root, "child");

        Assert.True(WorkspacePath.IsWithin(Root, child));
        Assert.True(WorkspacePath.IsWithin(Root + Path.DirectorySeparatorChar, child));
        Assert.False(WorkspacePath.IsWithin(Root, Root + "2"));
    }
}