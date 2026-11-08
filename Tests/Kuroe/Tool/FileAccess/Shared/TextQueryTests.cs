using ErrorOr;
using Xunit;

namespace Kuroe.Tool.FileAccess.Shared.Tests;

/// <summary>文本查找条件：后缀归一与非法条目拒绝。</summary>
public sealed class TextQueryTests
{
    [Fact]
    public void Create_normalizes_extensions_and_rejects_invalid_entries()
    {
        ErrorOr<TextQuery> query = TextQuery.Create("x", ["CS", ".md", "cs"], ignoreCase: false);

        Assert.False(query.IsError);
        Assert.Equal([".cs", ".md"], query.Value.Extensions);
        Assert.True(TextQuery.Create(" ", null, false).IsError);
        Assert.True(TextQuery.Create("x", [""], false).IsError);
        Assert.True(TextQuery.Create("x", ["*.cs"], false).IsError);
        Assert.True(TextQuery.Create("x", ["c?.cs"], false).IsError);
        Assert.True(TextQuery.Create("x", ["Source/cs"], false).IsError);
    }
}