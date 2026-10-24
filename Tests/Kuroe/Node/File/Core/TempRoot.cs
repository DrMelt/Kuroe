namespace Kuroe.Node.File.Tests;

/// <summary>测试用的临时目录，释放时递归删除。</summary>
internal sealed class TempRoot : IDisposable
{
    public TempRoot() => Directory.CreateDirectory(DirectoryPath);

    /// <summary>临时目录的绝对路径。</summary>
    public string DirectoryPath { get; } =
        Path.Combine(Path.GetTempPath(), "kuroe-file-node-tests", Guid.NewGuid().ToString("N"));

    /// <summary>把相对路径解析到临时目录内。</summary>
    public string Resolve(string relative) => Path.Combine(DirectoryPath, relative);

    public void Dispose()
    {
        try
        {
            Directory.Delete(DirectoryPath, recursive: true);
        }
        catch (IOException)
        {
            // 临时目录可能已被清理或占用，收尾不再上报
        }
    }
}