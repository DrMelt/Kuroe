using Kuroe.Ability.Tool.FileAccess.Output;
using Xunit;

namespace Kuroe.Ability.Tool.FileAccess.Tests.Output;

/// <summary>带固定标记的文本：正文与标记的包装。</summary>
public sealed class MarkedTextTests
{
    [Fact(DisplayName = "标记文本用标记包裹内容")]
    public void ToString_wraps_content_with_the_markers()
    {
        MarkedText text = new("正文");

        Assert.Equal("<<<KUROE_TEXT_BEGIN>>>正文<<<KUROE_TEXT_END>>>", text.ToString());
    }
}