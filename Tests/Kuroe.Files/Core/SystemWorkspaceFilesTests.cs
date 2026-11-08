using System.Text;
using Kuroe.Files.Shared;
using Xunit;

namespace Kuroe.Files.Tests;

/// <summary>系统文件访问的薄适配：编码与 BOM、目录枚举、文件事实与写入移动删除。</summary>
public sealed class SystemWorkspaceFilesTests : IDisposable
{
    private readonly string _root = Path.Combine(Path.GetTempPath(), "kuroe-tests", Guid.NewGuid().ToString("N"));

    public SystemWorkspaceFilesTests() => SystemWorkspaceFiles.Instance.CreateDirectory(_root);

    [Fact]
    public void Write_text_uses_the_encoding_preamble()
    {
        string path = Path.Combine(_root, "utf16.txt");

        SystemWorkspaceFiles.Instance.WriteText(path, "内容", Encoding.Unicode);
        byte[] bytes = SystemWorkspaceFiles.Instance.ReadBytes(path);

        Assert.Equal(new byte[] { 0xFF, 0xFE }, bytes[..2]);
        Assert.Equal("内容", Encoding.Unicode.GetString(bytes, 2, bytes.Length - 2));
    }

    [Fact]
    public void Write_text_with_utf8_bom_keeps_the_bom()
    {
        string path = Path.Combine(_root, "bom.txt");

        SystemWorkspaceFiles.Instance.WriteText(path, "内容", new UTF8Encoding(encoderShouldEmitUTF8Identifier: true));
        byte[] bytes = SystemWorkspaceFiles.Instance.ReadBytes(path);

        Assert.Equal(new byte[] { 0xEF, 0xBB, 0xBF }, bytes[..3]);
    }

    [Fact]
    public void Entries_split_files_and_directories()
    {
        string file = Path.Combine(_root, "a.txt");
        string directory = Path.Combine(_root, "b");
        SystemWorkspaceFiles.Instance.WriteText(file, "x", new UTF8Encoding(false));
        SystemWorkspaceFiles.Instance.CreateDirectory(directory);

        Assert.Equal([file], SystemWorkspaceFiles.Instance.Files(_root));
        Assert.Equal([directory], SystemWorkspaceFiles.Instance.Directories(_root));
        Assert.Equal(2, SystemWorkspaceFiles.Instance.Entries(_root).Count);
        Assert.True(SystemWorkspaceFiles.Instance.FileExists(file));
        Assert.True(SystemWorkspaceFiles.Instance.DirectoryExists(directory));
    }

    [Fact]
    public void Inspect_reports_size_and_last_write()
    {
        string path = Path.Combine(_root, "a.txt");
        DateTimeOffset lastWrite = new(2026, 1, 2, 3, 4, 5, TimeSpan.Zero);
        SystemWorkspaceFiles.Instance.WriteText(path, "abc", new UTF8Encoding(false));
        File.SetLastWriteTimeUtc(path, lastWrite.UtcDateTime);

        FileFacts facts = SystemWorkspaceFiles.Instance.Inspect(path);

        Assert.Equal(3, facts.Size);
        Assert.Equal(lastWrite, facts.LastWrite);
    }

    [Fact]
    public void Move_and_delete_change_the_tree()
    {
        string source = Path.Combine(_root, "a.txt");
        string target = Path.Combine(_root, "nested", "b.txt");
        SystemWorkspaceFiles.Instance.WriteText(source, "x", new UTF8Encoding(false));
        SystemWorkspaceFiles.Instance.CreateDirectory(Path.GetDirectoryName(target)
            ?? throw new InvalidOperationException($"目标 {target} 没有所在目录。"));

        SystemWorkspaceFiles.Instance.MoveFile(source, target);

        Assert.False(SystemWorkspaceFiles.Instance.FileExists(source));
        Assert.True(SystemWorkspaceFiles.Instance.FileExists(target));

        SystemWorkspaceFiles.Instance.DeleteDirectory(_root, recursive: true);

        Assert.False(SystemWorkspaceFiles.Instance.DirectoryExists(_root));
    }

    [Fact]
    public void Directory_is_not_a_reparse_point()
    {
        SystemWorkspaceFiles.Instance.CreateDirectory(_root);

        Assert.False(SystemWorkspaceFiles.Instance.IsReparsePoint(_root));
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