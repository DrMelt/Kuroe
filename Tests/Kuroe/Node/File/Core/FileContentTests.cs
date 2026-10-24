using Xunit;

namespace Kuroe.Node.File.Tests;

/// <summary>文件节点的读写边界：读取截断与写入长度上限。</summary>
public sealed class FileContentTests
{
    private const int ReadLimit = 8000;

    private const int WriteLimit = 40000;

    [Fact]
    public void Read_truncates_at_the_limit()
    {
        using TempRoot root = new();
        System.IO.File.WriteAllText(root.Resolve("big.txt"), new string('a', ReadLimit + 10));

        string content = FileContent.Read(root.Resolve("big.txt"), ReadLimit);

        Assert.Equal(ReadLimit + "…内容已截断".Length, content.Length);
        Assert.EndsWith("…内容已截断", content);
    }

    [Fact]
    public void Read_without_a_limit_returns_the_whole_file()
    {
        using TempRoot root = new();
        string expected = new('a', ReadLimit + 10);
        System.IO.File.WriteAllText(root.Resolve("big.txt"), expected);

        string content = FileContent.Read(root.Resolve("big.txt"), 0);

        Assert.Equal(expected, content);
    }

    [Fact]
    public void Write_rejects_content_over_the_limit()
    {
        using TempRoot root = new();

        string? failure = FileContent.Write(root.Resolve("out.txt"), new string('a', WriteLimit + 1), WriteLimit);

        Assert.Contains($"写入内容超过上限 {WriteLimit} 字符", failure);
        Assert.False(System.IO.File.Exists(root.Resolve("out.txt")));
    }

    [Fact]
    public void Write_keeps_content_at_the_limit()
    {
        using TempRoot root = new();
        string content = new('a', WriteLimit);

        string? failure = FileContent.Write(root.Resolve("out.txt"), content, WriteLimit);

        Assert.Null(failure);
        Assert.Equal(content, System.IO.File.ReadAllText(root.Resolve("out.txt")));
    }

    [Fact]
    public void Write_without_a_limit_keeps_long_content()
    {
        using TempRoot root = new();
        string content = new('a', WriteLimit + 10);

        string? failure = FileContent.Write(root.Resolve("out.txt"), content, 0);

        Assert.Null(failure);
        Assert.Equal(content, System.IO.File.ReadAllText(root.Resolve("out.txt")));
    }
}