using Xunit;

namespace Kuroe.Presentation.Shared.Tests;

/// <summary>过程记录文本的换行归一与逻辑行切分。</summary>
public sealed class JournalTextTests
{
    [Fact(DisplayName = "归一化把 CRLF 与 CR 转成 LF")]
    public void Normalize_turns_crlf_and_cr_into_lf()
    {
        Assert.Equal("一\n二\n三", JournalText.Normalize("一\r\n二\r三"));
    }

    [Fact(DisplayName = "行切分处理 CRLF 与 CR 文本")]
    public void Lines_splits_crlf_and_cr_text()
    {
        Assert.Equal(["一", "二", "三"], JournalText.Lines("一\r\n二\r三"));
    }

    [Fact(DisplayName = "行切分丢弃末尾终止符")]
    public void Lines_drops_the_trailing_terminator()
    {
        Assert.Equal(["一", "二"], JournalText.Lines("一\n二\n"));
    }

    [Fact(DisplayName = "行切分保留内部空行")]
    public void Lines_keeps_inner_empty_lines()
    {
        Assert.Equal(["一", string.Empty, "二"], JournalText.Lines("一\r\n\r\n二"));
    }

    [Fact(DisplayName = "行切分只丢弃一个末尾空行")]
    public void Lines_drops_only_one_trailing_empty_line()
    {
        Assert.Equal(["一", string.Empty], JournalText.Lines("一\n\n"));
    }

    [Fact(DisplayName = "空文本切分为一个空行")]
    public void Lines_of_empty_text_is_one_empty_line()
    {
        Assert.Equal([string.Empty], JournalText.Lines(string.Empty));
    }
}