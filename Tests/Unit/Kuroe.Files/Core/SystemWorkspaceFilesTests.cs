using System.Text;
using Kuroe.Files.Shared;
using Xunit;

namespace Kuroe.Files.Tests;

/// <summary>系统文件访问的薄适配：编码与 BOM、目录枚举、文件事实与写入移动删除。</summary>
public sealed class SystemWorkspaceFilesTests : IDisposable
{
    private readonly SystemWorkspaceFiles _files = new();
    private readonly string _root = Path.Combine(Path.GetTempPath(), "kuroe-tests", Guid.NewGuid().ToString("N"));

    public SystemWorkspaceFilesTests() => _files.CreateDirectory(_root);

    [Fact(DisplayName = "写入文本使用编码前导")]
    public void Write_text_uses_the_encoding_preamble()
    {
        string path = Path.Combine(_root, "utf16.txt");

        _files.WriteText(path, "内容", Encoding.Unicode);
        byte[] bytes = _files.ReadBytes(path);

        Assert.Equal(new byte[] { 0xFF, 0xFE }, bytes[..2]);
        Assert.Equal("内容", Encoding.Unicode.GetString(bytes, 2, bytes.Length - 2));
    }

    [Fact(DisplayName = "带 UTF-8 BOM 的写入保留 BOM")]
    public void Write_text_with_utf8_bom_keeps_the_bom()
    {
        string path = Path.Combine(_root, "bom.txt");

        _files.WriteText(path, "内容", new UTF8Encoding(encoderShouldEmitUTF8Identifier: true));
        byte[] bytes = _files.ReadBytes(path);

        Assert.Equal(new byte[] { 0xEF, 0xBB, 0xBF }, bytes[..3]);
    }

    [Fact(DisplayName = "条目区分文件与目录")]
    public void Entries_split_files_and_directories()
    {
        string file = Path.Combine(_root, "a.txt");
        string directory = Path.Combine(_root, "b");
        _files.WriteText(file, "x", new UTF8Encoding(false));
        _files.CreateDirectory(directory);

        Assert.Equal([file], _files.Files(_root));
        Assert.Equal([directory], _files.Directories(_root));
        Assert.Equal(2, _files.Entries(_root).Count);
        Assert.True(_files.FileExists(file));
        Assert.True(_files.DirectoryExists(directory));
    }

    [Fact(DisplayName = "检查报告大小与最后写入时间")]
    public void Inspect_reports_size_and_last_write()
    {
        string path = Path.Combine(_root, "a.txt");
        DateTimeOffset lastWrite = new(2026, 1, 2, 3, 4, 5, TimeSpan.Zero);
        _files.WriteText(path, "abc", new UTF8Encoding(false));
        File.SetLastWriteTimeUtc(path, lastWrite.UtcDateTime);

        FileFacts facts = _files.Inspect(path);

        Assert.Equal(3, facts.Size);
        Assert.Equal(lastWrite, facts.LastWrite);
    }

    [Fact(DisplayName = "移动与删除改变目录树")]
    public void Move_and_delete_change_the_tree()
    {
        string source = Path.Combine(_root, "a.txt");
        string target = Path.Combine(_root, "nested", "b.txt");
        _files.WriteText(source, "x", new UTF8Encoding(false));
        _files.CreateDirectory(Path.GetDirectoryName(target)
            ?? throw new InvalidOperationException($"目标 {target} 没有所在目录。"));

        _files.MoveFile(source, target);

        Assert.False(_files.FileExists(source));
        Assert.True(_files.FileExists(target));

        _files.DeleteDirectory(_root, recursive: true);

        Assert.False(_files.DirectoryExists(_root));
    }

    [Fact(DisplayName = "目录不是重解析点")]
    public void Directory_is_not_a_reparse_point()
    {
        _files.CreateDirectory(_root);

        Assert.False(_files.IsReparsePoint(_root));
    }

    [Fact(DisplayName = "原子写入建立缺失的父目录且不带 BOM")]
    public void Atomic_write_creates_missing_parents_without_a_bom()
    {
        string path = Path.Combine(_root, "nested", "deep", "a.json");

        _files.WriteTextAtomic(path, "内容");

        byte[] bytes = _files.ReadBytes(path);
        Assert.Equal("内容", Encoding.UTF8.GetString(bytes));
    }

    [Fact(DisplayName = "原子写入替换已有目标且不残留临时文件")]
    public void Atomic_write_replaces_an_existing_target_without_leftovers()
    {
        string path = Path.Combine(_root, "a.json");
        _files.WriteTextAtomic(path, "旧");

        _files.WriteTextAtomic(path, "新");

        Assert.Equal("新", Encoding.UTF8.GetString(_files.ReadBytes(path)));
        Assert.Equal([path], _files.Files(_root));
    }

    public void Dispose()
    {
        try
        {
            if (Directory.Exists(_root))
            {
                Directory.Delete(_root, recursive: true);
            }
        }
        catch (IOException)
        {
            // 临时目录可能已被清理或占用，收尾不再上报
        }
    }
}