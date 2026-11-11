using System.Text;
using ErrorOr;
using Kuroe.TestSupport;
using Kuroe.Tool.FileAccess.Shared;
using Xunit;

namespace Kuroe.Tool.FileAccess.Tests;

/// <summary>文本文件内容：编码与 BOM 保持、换行保持、归一匹配与原文索引映射。</summary>
public sealed class TextFileContentTests
{
    private readonly string _root = Path.Combine(Path.GetTempPath(), "kuroe-tests", Guid.NewGuid().ToString("N"));
    private readonly InMemoryWorkspaceFiles _files = new();

    [Fact]
    public void Read_and_write_preserve_utf8_bom()
    {
        string file = Path.Combine(_root, "bom.txt");
        _files.AddFile(file, "你好", new UTF8Encoding(encoderShouldEmitUTF8Identifier: true));

        TextFileContent content = Read(file);
        TextFileReplacement change = content.Replace(Replacement("你", "我"));
        ErrorOr<Success> written = content.Write(_files, file, change.Text, null);

        Assert.False(written.IsError);
        Assert.Equal("你好", content.Original);
        Assert.Equal(1, change.Replacements);
        byte[] bytes = _files.ReadBytes(file);
        Assert.Equal(new byte[] { 0xEF, 0xBB, 0xBF }, bytes[..3]);
        Assert.Equal("我好", new UTF8Encoding(false).GetString(bytes, 3, bytes.Length - 3));
    }

    [Fact]
    public void Read_and_write_preserve_utf16_bom()
    {
        string file = Path.Combine(_root, "utf16.txt");
        _files.AddFile(file, "内容", Encoding.Unicode);

        TextFileContent content = Read(file);
        TextFileReplacement change = content.Replace(Replacement("内", "外"));
        ErrorOr<Success> written = content.Write(_files, file, change.Text, null);

        Assert.False(written.IsError);
        byte[] bytes = _files.ReadBytes(file);
        Assert.Equal(new byte[] { 0xFF, 0xFE }, bytes[..2]);
        Assert.Equal("外容", Encoding.Unicode.GetString(bytes, 2, bytes.Length - 2));
    }

    [Fact]
    public void Read_and_write_preserve_utf16be_bom()
    {
        string file = Path.Combine(_root, "utf16be.txt");
        _files.AddFile(file, "内容", Encoding.BigEndianUnicode);

        TextFileContent content = Read(file);
        TextFileReplacement change = content.Replace(Replacement("内", "外"));
        ErrorOr<Success> written = content.Write(_files, file, change.Text, null);

        Assert.False(written.IsError);
        byte[] bytes = _files.ReadBytes(file);
        Assert.Equal(new byte[] { 0xFE, 0xFF }, bytes[..2]);
        Assert.Equal("外容", Encoding.BigEndianUnicode.GetString(bytes, 2, bytes.Length - 2));
    }

    [Fact]
    public void Read_and_write_preserve_utf32_bom()
    {
        string file = Path.Combine(_root, "utf32.txt");
        _files.AddFile(file, "内容", Encoding.UTF32);

        TextFileContent content = Read(file);
        TextFileReplacement change = content.Replace(Replacement("内", "外"));
        ErrorOr<Success> written = content.Write(_files, file, change.Text, null);

        Assert.False(written.IsError);
        byte[] bytes = _files.ReadBytes(file);
        Assert.Equal(new byte[] { 0xFF, 0xFE, 0x00, 0x00 }, bytes[..4]);
        Assert.Equal("外容", Encoding.UTF32.GetString(bytes, 4, bytes.Length - 4));
    }

    [Fact]
    public void Replace_matches_multiline_text_across_crlf()
    {
        string file = Path.Combine(_root, "crlf.txt");
        _files.AddFile(file, "one\r\ntwo\r\nthree");

        TextFileContent content = Read(file);
        TextFileReplacement change = content.Replace(Replacement("one\ntwo", "ONE\nTWO"));

        Assert.Equal(1, change.Replacements);
        Assert.Equal("ONE\r\nTWO\r\nthree", change.Text);
    }

    [Fact]
    public void Replace_preserves_untouched_mixed_newlines()
    {
        string file = Path.Combine(_root, "mixed.txt");
        _files.AddFile(file, "a\r\nb\nc\r\n");

        TextFileContent content = Read(file);
        TextFileReplacement change = content.Replace(Replacement("b", "B"));

        Assert.Equal(NewLineStyle.CrLf, content.NewLine);
        Assert.Equal("a\r\nB\nc\r\n", change.Text);
    }

    [Fact]
    public void Replace_converts_replacement_newlines_to_file_style()
    {
        string file = Path.Combine(_root, "style.txt");
        _files.AddFile(file, "a\r\nb");

        TextFileContent content = Read(file);
        TextFileReplacement change = content.Replace(Replacement("a", "x\ny"));

        Assert.Equal("x\r\ny\r\nb", change.Text);
    }

    [Fact]
    public void Replace_converts_replacement_newlines_to_cr_for_cr_files()
    {
        string file = Path.Combine(_root, "cr.txt");
        _files.AddFile(file, "a\rb");

        TextFileContent content = Read(file);
        TextFileReplacement change = content.Replace(Replacement("a", "x\ny"));

        Assert.Equal(NewLineStyle.Cr, content.NewLine);
        Assert.Equal("x\ry\rb", change.Text);
    }

    [Fact]
    public void Replace_uses_lf_for_files_without_newlines()
    {
        string file = Path.Combine(_root, "flat.txt");
        _files.AddFile(file, "a");

        TextFileContent content = Read(file);
        TextFileReplacement change = content.Replace(Replacement("a", "x\ny"));

        Assert.Equal(NewLineStyle.Lf, content.NewLine);
        Assert.Equal("x\ny", change.Text);
    }

    [Fact]
    public void Replace_returns_non_overlapping_replacements()
    {
        string file = Path.Combine(_root, "overlap.txt");
        _files.AddFile(file, "aaaa");

        TextFileContent content = Read(file);
        TextFileReplacement change = content.Replace(Replacement("aa", "b"));

        Assert.Equal(2, change.Replacements);
        Assert.Equal("bb", change.Text);
    }

    [Fact]
    public void Replace_is_case_sensitive_and_honors_inline_regex_case()
    {
        string file = Path.Combine(_root, "case.txt");
        _files.AddFile(file, "Needle");

        TextFileContent content = Read(file);

        Assert.Equal(0, content.Replace(Replacement("needle", "x")).Replacements);
        Assert.Equal(1, content.Replace(Replacement("(?i)needle", "x", regex: true)).Replacements);
    }

    [Fact]
    public void Replace_returns_match_start_lines()
    {
        string file = Path.Combine(_root, "lines.txt");
        _files.AddFile(file, "a\nb\na");

        TextFileContent content = Read(file);
        TextFileReplacement change = content.Replace(Replacement("a", "x"));

        Assert.Equal([1, 3], change.Lines);
    }

    [Fact]
    public void Replace_keeps_one_line_for_multiple_matches_on_the_same_line()
    {
        string file = Path.Combine(_root, "same-line.txt");
        _files.AddFile(file, "a a");

        TextFileContent content = Read(file);
        TextFileReplacement change = content.Replace(Replacement("a", "x"));

        Assert.Equal(2, change.Replacements);
        Assert.Equal([1], change.Lines);
    }

    [Fact]
    public void Replace_expands_regex_group_references()
    {
        string file = Path.Combine(_root, "regex.txt");
        _files.AddFile(file, "abc123");

        TextFileContent content = Read(file);
        TextFileReplacement change = content.Replace(Replacement("([a-z]+)(\\d+)", "$2-$1", regex: true));

        Assert.Equal("123-abc", change.Text);
        Assert.Equal(1, change.Replacements);
        Assert.Equal([1], change.Lines);
    }

    [Fact]
    public void Replace_converts_newlines_in_expanded_match_text()
    {
        string file = Path.Combine(_root, "crlf-regex.txt");
        _files.AddFile(file, "one a\r\nb two");

        TextFileContent content = Read(file);
        TextFileReplacement change = content.Replace(Replacement("a(\n)b", "X$1Y", regex: true));

        Assert.Equal("one X\r\nY two", change.Text);
        Assert.Equal(1, change.Replacements);
        Assert.Equal([1], change.Lines);
    }

    [Fact]
    public void Replace_inserts_on_a_zero_width_regex_match()
    {
        string file = Path.Combine(_root, "anchor.txt");
        _files.AddFile(file, "ab");

        TextFileContent content = Read(file);
        TextFileReplacement change = content.Replace(Replacement("$", "X", regex: true));

        Assert.Equal("abX", change.Text);
        Assert.Equal(1, change.Replacements);
        Assert.Equal([1], change.Lines);
    }

    [Fact]
    public void Replace_inserts_at_both_dollar_positions_of_a_trailing_newline()
    {
        string file = Path.Combine(_root, "trailing.txt");
        _files.AddFile(file, "a\r\n");

        TextFileContent content = Read(file);
        TextFileReplacement change = content.Replace(Replacement("$", "X", regex: true));

        Assert.Equal("aX\r\nX", change.Text);
        Assert.Equal(2, change.Replacements);
        Assert.Equal([1, 2], change.Lines);
    }

    [Fact]
    public void Replace_returns_non_overlapping_regex_replacements()
    {
        string file = Path.Combine(_root, "overlap-regex.txt");
        _files.AddFile(file, "aaaa");

        TextFileContent content = Read(file);
        TextFileReplacement change = content.Replace(Replacement("aa", "b", regex: true));

        Assert.Equal("bb", change.Text);
        Assert.Equal(2, change.Replacements);
    }

    [Fact]
    public void Read_rejects_binary_file()
    {
        string file = Path.Combine(_root, "binary.bin");
        _files.AddBytes(file, [0x00, 0x01, 0x02]);

        ErrorOr<TextFileContent> result = TextFileContent.Read(_files, file);

        Assert.True(result.IsError);
        Assert.Contains("二进制", result.FirstError.Description);
    }

    [Fact]
    public void Read_rejects_invalid_utf8()
    {
        string file = Path.Combine(_root, "invalid.txt");
        _files.AddBytes(file, [0x61, 0xFF, 0x62]);

        ErrorOr<TextFileContent> result = TextFileContent.Read(_files, file);

        Assert.True(result.IsError);
        Assert.Contains("读取失败", result.FirstError.Description);
    }

    [Fact]
    public void Read_rejects_missing_file()
    {
        ErrorOr<TextFileContent> result = TextFileContent.Read(_files, Path.Combine(_root, "missing.txt"));

        Assert.True(result.IsError);
        Assert.Contains("读取失败", result.FirstError.Description);
    }

    private TextFileContent Read(string file)
    {
        ErrorOr<TextFileContent> result = TextFileContent.Read(_files, file);
        Assert.False(result.IsError);

        return result.Value;
    }

    /// <summary>按替换条件建立替换，regex 为真时按正则解释 pattern。</summary>
    private static TextReplacement Replacement(
        string pattern,
        string replacement,
        bool regex = false)
    {
        ErrorOr<TextQuery> query = TextQuery.Create(pattern, null, regex);
        Assert.False(query.IsError);
        ErrorOr<TextReplacement> result = TextReplacement.Create(query.Value, replacement);
        Assert.False(result.IsError);

        return result.Value;
    }
}