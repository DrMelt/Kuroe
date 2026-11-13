using Kuroe.Flow.Presentation;
using Xunit;

namespace Kuroe.Flow.Presentation.Tests;

/// <summary>过程记录文本的换行归一与逻辑行切分。</summary>
public sealed class JournalTextTests
{
    [Fact]
    public void Normalize_turns_crlf_and_cr_into_lf()
    {
        Assert.Equal("一\n二\n三", JournalText.Normalize("一\r\n二\r三"));
    }

    [Fact]
    public void Lines_splits_crlf_and_cr_text()
    {
        Assert.Equal(["一", "二", "三"], JournalText.Lines("一\r\n二\r三"));
    }

    [Fact]
    public void Lines_drops_the_trailing_terminator()
    {
        Assert.Equal(["一", "二"], JournalText.Lines("一\n二\n"));
    }

    [Fact]
    public void Lines_keeps_inner_empty_lines()
    {
        Assert.Equal(["一", string.Empty, "二"], JournalText.Lines("一\r\n\r\n二"));
    }

    [Fact]
    public void Lines_drops_only_one_trailing_empty_line()
    {
        Assert.Equal(["一", string.Empty], JournalText.Lines("一\n\n"));
    }

    [Fact]
    public void Lines_of_empty_text_is_one_empty_line()
    {
        Assert.Equal([string.Empty], JournalText.Lines(string.Empty));
    }
}