using System.Text;
using ErrorOr;
using Xunit;

namespace Kuroe.Tool.FileAccess.Tests;

using File = System.IO.File;

/// <summary>文本文件内容：编码与 BOM 保持、换行保持、归一匹配与原文索引映射。</summary>
public sealed class TextFileContentTests : IDisposable
{
    private readonly string _root = Path.Combine(Path.GetTempPath(), "kuroe-tests", Guid.NewGuid().ToString("N"));

    public TextFileContentTests()
    {
        Directory.CreateDirectory(_root);
    }

    [Fact]
    public void Read_and_write_preserve_utf8_bom()
    {
        string file = Path.Combine(_root, "bom.txt");
        File.WriteAllText(file, "你好", new UTF8Encoding(encoderShouldEmitUTF8Identifier: true));

        TextFileContent content = Read(file);
        TextFileReplacement change = content.Replace(Replacement("你", "我"));
        ErrorOr<Success> written = content.Write(file, change.Text, null);

        Assert.False(written.IsError);
        Assert.Equal("你好", content.Original);
        Assert.Equal(1, change.Replacements);
        byte[] bytes = File.ReadAllBytes(file);
        Assert.Equal(new byte[] { 0xEF, 0xBB, 0xBF }, bytes[..3]);
        Assert.Equal("我好", new UTF8Encoding(false).GetString(bytes, 3, bytes.Length - 3));
    }

    [Fact]
    public void Read_and_write_preserve_utf16_bom()
    {
        string file = Path.Combine(_root, "utf16.txt");
        File.WriteAllText(file, "内容", Encoding.Unicode);

        TextFileContent content = Read(file);
        TextFileReplacement change = content.Replace(Replacement("内", "外"));
        ErrorOr<Success> written = content.Write(file, change.Text, null);

        Assert.False(written.IsError);
        byte[] bytes = File.ReadAllBytes(file);
        Assert.Equal(new byte[] { 0xFF, 0xFE }, bytes[..2]);
        Assert.Equal("外容", Encoding.Unicode.GetString(bytes, 2, bytes.Length - 2));
    }

    [Fact]
    public void Read_and_write_preserve_utf16be_bom()
    {
        string file = Path.Combine(_root, "utf16be.txt");
        File.WriteAllText(file, "内容", Encoding.BigEndianUnicode);

        TextFileContent content = Read(file);
        TextFileReplacement change = content.Replace(Replacement("内", "外"));
        ErrorOr<Success> written = content.Write(file, change.Text, null);

        Assert.False(written.IsError);
        byte[] bytes = File.ReadAllBytes(file);
        Assert.Equal(new byte[] { 0xFE, 0xFF }, bytes[..2]);
        Assert.Equal("外容", Encoding.BigEndianUnicode.GetString(bytes, 2, bytes.Length - 2));
    }

    [Fact]
    public void Read_and_write_preserve_utf32_bom()
    {
        string file = Path.Combine(_root, "utf32.txt");
        File.WriteAllText(file, "内容", Encoding.UTF32);

        TextFileContent content = Read(file);
        TextFileReplacement change = content.Replace(Replacement("内", "外"));
        ErrorOr<Success> written = content.Write(file, change.Text, null);

        Assert.False(written.IsError);
        byte[] bytes = File.ReadAllBytes(file);
        Assert.Equal(new byte[] { 0xFF, 0xFE, 0x00, 0x00 }, bytes[..4]);
        Assert.Equal("外容", Encoding.UTF32.GetString(bytes, 4, bytes.Length - 4));
    }

    [Fact]
    public void Replace_matches_multiline_text_across_crlf()
    {
        string file = Path.Combine(_root, "crlf.txt");
        File.WriteAllText(file, "one\r\ntwo\r\nthree");

        TextFileContent content = Read(file);
        TextFileReplacement change = content.Replace(Replacement("one\ntwo", "ONE\nTWO"));

        Assert.Equal(1, change.Replacements);
        Assert.Equal("ONE\r\nTWO\r\nthree", change.Text);
    }

    [Fact]
    public void Replace_preserves_untouched_mixed_newlines()
    {
        string file = Path.Combine(_root, "mixed.txt");
        File.WriteAllText(file, "a\r\nb\nc\r\n");

        TextFileContent content = Read(file);
        TextFileReplacement change = content.Replace(Replacement("b", "B"));

        Assert.Equal("\r\n", content.NewLine);
        Assert.Equal("a\r\nB\nc\r\n", change.Text);
    }

    [Fact]
    public void Replace_converts_replacement_newlines_to_file_style()
    {
        string file = Path.Combine(_root, "style.txt");
        File.WriteAllText(file, "a\r\nb");

        TextFileContent content = Read(file);
        TextFileReplacement change = content.Replace(Replacement("a", "x\ny"));

        Assert.Equal("x\r\ny\r\nb", change.Text);
    }

    [Fact]
    public void Replace_converts_replacement_newlines_to_cr_for_cr_files()
    {
        string file = Path.Combine(_root, "cr.txt");
        File.WriteAllText(file, "a\rb");

        TextFileContent content = Read(file);
        TextFileReplacement change = content.Replace(Replacement("a", "x\ny"));

        Assert.Equal("\r", content.NewLine);
        Assert.Equal("x\ry\rb", change.Text);
    }

    [Fact]
    public void Replace_uses_lf_for_files_without_newlines()
    {
        string file = Path.Combine(_root, "flat.txt");
        File.WriteAllText(file, "a");

        TextFileContent content = Read(file);
        TextFileReplacement change = content.Replace(Replacement("a", "x\ny"));

        Assert.Equal("\n", content.NewLine);
        Assert.Equal("x\ny", change.Text);
    }

    [Fact]
    public void Replace_returns_non_overlapping_replacements()
    {
        string file = Path.Combine(_root, "overlap.txt");
        File.WriteAllText(file, "aaaa");

        TextFileContent content = Read(file);
        TextFileReplacement change = content.Replace(Replacement("aa", "b"));

        Assert.Equal(2, change.Replacements);
        Assert.Equal("bb", change.Text);
    }

    [Fact]
    public void Replace_honors_case_comparison()
    {
        string file = Path.Combine(_root, "case.txt");
        File.WriteAllText(file, "Needle");

        TextFileContent content = Read(file);

        Assert.Equal(0, content.Replace(Replacement("needle", "x")).Replacements);
        Assert.Equal(1, content.Replace(Replacement("needle", "x", ignoreCase: true)).Replacements);
    }

    [Fact]
    public void Replace_returns_match_start_lines()
    {
        string file = Path.Combine(_root, "lines.txt");
        File.WriteAllText(file, "a\nb\na");

        TextFileContent content = Read(file);
        TextFileReplacement change = content.Replace(Replacement("a", "x"));

        Assert.Equal([1, 3], change.Lines);
    }

    [Fact]
    public void Replace_keeps_one_line_for_multiple_matches_on_the_same_line()
    {
        string file = Path.Combine(_root, "same-line.txt");
        File.WriteAllText(file, "a a");

        TextFileContent content = Read(file);
        TextFileReplacement change = content.Replace(Replacement("a", "x"));

        Assert.Equal(2, change.Replacements);
        Assert.Equal([1], change.Lines);
    }

    [Fact]
    public void Read_rejects_binary_file()
    {
        string file = Path.Combine(_root, "binary.bin");
        File.WriteAllBytes(file, [0x00, 0x01, 0x02]);

        ErrorOr<TextFileContent> result = TextFileContent.Read(file);

        Assert.True(result.IsError);
        Assert.Contains("二进制", result.FirstError.Description);
    }

    [Fact]
    public void Read_rejects_invalid_utf8()
    {
        string file = Path.Combine(_root, "invalid.txt");
        File.WriteAllBytes(file, [0x61, 0xFF, 0x62]);

        ErrorOr<TextFileContent> result = TextFileContent.Read(file);

        Assert.True(result.IsError);
        Assert.Contains("读取失败", result.FirstError.Description);
    }

    [Fact]
    public void Read_rejects_missing_file()
    {
        ErrorOr<TextFileContent> result = TextFileContent.Read(Path.Combine(_root, "missing.txt"));

        Assert.True(result.IsError);
        Assert.Contains("读取失败", result.FirstError.Description);
    }

    private static TextFileContent Read(string file)
    {
        ErrorOr<TextFileContent> result = TextFileContent.Read(file);
        Assert.False(result.IsError);

        return result.Value;
    }

    /// <summary>按字面文本建立替换条件。</summary>
    private static TextReplacement Replacement(string pattern, string replacement, bool ignoreCase = false)
    {
        ErrorOr<TextQuery> query = TextQuery.Create(pattern, null, ignoreCase);
        Assert.False(query.IsError);
        ErrorOr<TextReplacement> result = TextReplacement.Create(query.Value, replacement);
        Assert.False(result.IsError);

        return result.Value;
    }

    public void Dispose()
    {
        try
        {
            Directory.Delete(_root, recursive: true);
        }
        catch (IOException)
        {
            // 临时目录可能已被清理或占用，收尾不再上报
        }
    }
}