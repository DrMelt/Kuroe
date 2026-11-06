using System.Text;
using ErrorOr;
using Xunit;

namespace Kuroe.Tool.FileAccess.Tests;

using File = System.IO.File;

/// <summary>批量文本替换：范围展开、后缀过滤、二进制跳过、预览、编码与换行保持。</summary>
public sealed class FileReplacerTests : IDisposable
{
    private readonly string _root = Path.Combine(Path.GetTempPath(), "kuroe-tests", Guid.NewGuid().ToString("N"));

    public FileReplacerTests()
    {
        Directory.CreateDirectory(_root);
    }

    [Fact]
    public void Replace_changes_every_matching_file()
    {
        Directory.CreateDirectory(Path.Combine(_root, "sub"));
        string first = Path.Combine(_root, "a.txt");
        string second = Path.Combine(_root, "sub", "b.txt");
        File.WriteAllText(first, "needle\nneedle");
        File.WriteAllText(second, "needle");

        TextReplaceResult result = Run(["."], Replacement("needle", "x", [".txt"]));

        Assert.Equal(2, result.ChangedFiles);
        Assert.Equal(3, result.Replacements);
        Assert.Equal("x\nx", File.ReadAllText(first));
        Assert.Equal("x", File.ReadAllText(second));
        Assert.Equal([1, 2], result.Files.Single(file => Path.GetFileName(file.File) == "a.txt").Lines);
    }

    [Fact]
    public void Replace_filters_by_extension()
    {
        string code = Path.Combine(_root, "a.cs");
        string text = Path.Combine(_root, "a.md");
        File.WriteAllText(code, "needle");
        File.WriteAllText(text, "needle");

        TextReplaceResult result = Run(["."], Replacement("needle", "x", ["cs"]));

        Assert.Equal(1, result.ChangedFiles);
        Assert.Equal("x", File.ReadAllText(code));
        Assert.Equal("needle", File.ReadAllText(text));
    }

    [Fact]
    public void Replace_skips_binary_files()
    {
        string text = Path.Combine(_root, "a.txt");
        string binary = Path.Combine(_root, "b.bin");
        File.WriteAllText(text, "needle");
        File.WriteAllBytes(binary, [0x00, 0x6E, 0x65, 0x65, 0x64, 0x6C, 0x65]);

        TextReplaceResult result = Run(["."], Replacement("needle", "x"));

        Assert.Equal(1, result.ChangedFiles);
        Assert.Equal("x", File.ReadAllText(text));
    }

    [Fact]
    public void Replace_dry_run_does_not_write()
    {
        string file = Path.Combine(_root, "a.txt");
        File.WriteAllText(file, "needle");

        TextReplaceResult result = Run(["."], Replacement("needle", "x"), dryRun: true);

        Assert.Equal(1, result.ChangedFiles);
        Assert.Equal("needle", File.ReadAllText(file));
    }

    [Fact]
    public void Replace_reports_no_match_without_touching_files()
    {
        string file = Path.Combine(_root, "a.txt");
        File.WriteAllText(file, "other");

        TextReplaceResult result = Run(["."], Replacement("needle", "x"));

        Assert.Empty(result.Files);
        Assert.Equal(1, result.ScannedFiles);
        Assert.Equal("other", File.ReadAllText(file));
    }

    [Fact]
    public void Replace_rejects_path_escaping_working_directory()
    {
        ErrorOr<TextReplaceResult> result = new FileReplacer(Scope())
            .Replace([".."], Replacement("needle", "x"), dryRun: false, encodingOverride: null);

        Assert.True(result.IsError);
        Assert.Contains("不在工作目录内", result.FirstError.Description);
    }

    [Fact]
    public void Replace_skips_denied_directories()
    {
        Directory.CreateDirectory(Path.Combine(_root, ".kuroe"));
        string file = Path.Combine(_root, ".kuroe", "catalog.json");
        File.WriteAllText(file, "needle");

        TextReplaceResult result = Run(["."], Replacement("needle", "x", [".json"]), access: DenyAccess());

        Assert.Empty(result.Files);
        Assert.Equal("needle", File.ReadAllText(file));
    }

    [Fact]
    public void Replace_ignores_case_when_configured()
    {
        string file = Path.Combine(_root, "a.txt");
        File.WriteAllText(file, "Needle");

        TextReplaceResult result = Run(["."], Replacement("needle", "x", ignoreCase: true));

        Assert.Equal(1, result.ChangedFiles);
        Assert.Equal("x", File.ReadAllText(file));
    }

    [Fact]
    public void Replace_normalizes_case_when_replacement_equals_pattern()
    {
        string file = Path.Combine(_root, "a.txt");
        File.WriteAllText(file, "NEEDLE");

        TextReplaceResult result = Run(["."], Replacement("needle", "needle", ignoreCase: true));

        Assert.Equal(1, result.ChangedFiles);
        Assert.Equal(1, result.Replacements);
        Assert.Equal("needle", File.ReadAllText(file));
    }

    [Fact]
    public void Replace_preserves_encoding_and_newline()
    {
        string file = Path.Combine(_root, "utf16.txt");
        File.WriteAllText(file, "a\r\nb", Encoding.Unicode);

        TextReplaceResult result = Run(["."], Replacement("a", "x\ny"));

        byte[] bytes = File.ReadAllBytes(file);
        Assert.Equal(1, result.ChangedFiles);
        Assert.Equal(new byte[] { 0xFF, 0xFE }, bytes[..2]);
        Assert.Equal("x\r\ny\r\nb", Encoding.Unicode.GetString(bytes, 2, bytes.Length - 2));
    }

    [Fact]
    public void Replace_honors_encoding_override()
    {
        string file = Path.Combine(_root, "a.txt");
        File.WriteAllText(file, "a");

        ErrorOr<TextReplaceResult> result = new FileReplacer(Scope())
            .Replace(["."], Replacement("a", "x"), dryRun: false, encodingOverride: Encoding.Unicode);

        Assert.False(result.IsError);
        byte[] bytes = File.ReadAllBytes(file);
        Assert.Equal(new byte[] { 0xFF, 0xFE }, bytes[..2]);
        Assert.Equal("x", Encoding.Unicode.GetString(bytes, 2, bytes.Length - 2));
    }

    [Fact]
    public void Replace_does_not_recurse_replacement_text()
    {
        string file = Path.Combine(_root, "a.txt");
        File.WriteAllText(file, "a");

        TextReplaceResult result = Run(["."], Replacement("a", "aa"));

        Assert.Equal(1, result.Replacements);
        Assert.Equal("aa", File.ReadAllText(file));
    }

    private FileScope Scope() => new(_root, "工作目录");

    private static ToolPathAccess DenyAccess() => new([], [".kuroe", ".git"]);

    private TextReplaceResult Run(
        IReadOnlyList<string> paths,
        TextReplacement replacement,
        bool dryRun = false,
        ToolPathAccess? access = null)
    {
        ErrorOr<TextReplaceResult> result = new FileReplacer(Scope(), access)
            .Replace(paths, replacement, dryRun, encodingOverride: null);
        Assert.False(result.IsError);

        return result.Value;
    }

    private static TextReplacement Replacement(
        string pattern,
        string replacement,
        IReadOnlyList<string>? extensions = null,
        bool ignoreCase = false)
    {
        ErrorOr<TextQuery> query = TextQuery.Create(pattern, extensions, ignoreCase);
        Assert.False(query.IsError);
        ErrorOr<TextReplacement> text = TextReplacement.Create(query.Value, replacement);
        Assert.False(text.IsError);

        return text.Value;
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