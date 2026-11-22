using System.Text;
using Kuroe.Files.Shared;

namespace Kuroe.Files;

/// <summary>工作区文件访问的系统实现，全部操作交给 System.IO。</summary>
public sealed class SystemWorkspaceFiles : IWorkspaceFiles
{
    /// <inheritdoc/>
    public bool FileExists(string path) => File.Exists(path);

    /// <inheritdoc/>
    public bool DirectoryExists(string path) => Directory.Exists(path);

    /// <inheritdoc/>
    public IReadOnlyList<string> Entries(string directory) => Directory.GetFileSystemEntries(directory);

    /// <inheritdoc/>
    public IReadOnlyList<string> Files(string directory) => Directory.GetFiles(directory);

    /// <inheritdoc/>
    public IReadOnlyList<string> Directories(string directory) => Directory.GetDirectories(directory);

    /// <inheritdoc/>
    public FileFacts Inspect(string file)
    {
        FileInfo info = new(file);

        return new FileFacts(
            info.Length,
            new DateTimeOffset(info.CreationTime),
            new DateTimeOffset(info.LastWriteTime),
            new DateTimeOffset(info.LastAccessTime),
            info.Attributes);
    }

    /// <inheritdoc/>
    public Stream OpenRead(string file) =>
        new FileStream(file, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete);

    /// <inheritdoc/>
    public byte[] ReadBytes(string file) => File.ReadAllBytes(file);

    /// <inheritdoc/>
    public void WriteText(string path, string text, Encoding encoding) => File.WriteAllText(path, text, encoding);

    /// <inheritdoc/>
    /// <remarks>先写临时文件再替换。临时文件带进程号，多个进程写同一目标时不互相覆盖。</remarks>
    public void WriteTextAtomic(string path, string text)
    {
        string target = Path.GetFullPath(path);
        string directory = Path.GetDirectoryName(target)
            ?? throw new IOException($"目标路径 {target} 没有所在目录。");
        Directory.CreateDirectory(directory);

        string temporary = Path.Combine(directory, $"{Path.GetFileName(target)}.{Environment.ProcessId}.{Guid.NewGuid():N}.tmp");
        try
        {
            using (FileStream stream = new(temporary, FileMode.CreateNew, FileAccess.Write, FileShare.None))
            using (StreamWriter writer = new(stream, new UTF8Encoding(encoderShouldEmitUTF8Identifier: false)))
            {
                writer.Write(text);
                writer.Flush();
                stream.Flush(flushToDisk: true);
            }

            if (File.Exists(target))
            {
                File.Replace(temporary, target, destinationBackupFileName: null);
            }
            else
            {
                File.Move(temporary, target);
            }
        }
        finally
        {
            if (File.Exists(temporary))
            {
                File.Delete(temporary);
            }
        }
    }

    /// <inheritdoc/>
    public void CreateDirectory(string path) => Directory.CreateDirectory(path);

    /// <inheritdoc/>
    public void DeleteFile(string path) => File.Delete(path);

    /// <inheritdoc/>
    public void DeleteDirectory(string path, bool recursive) => Directory.Delete(path, recursive);

    /// <inheritdoc/>
    public void MoveFile(string source, string target) => File.Move(source, target);

    /// <inheritdoc/>
    public void MoveDirectory(string source, string target) => Directory.Move(source, target);

    /// <inheritdoc/>
    public bool IsReparsePoint(string directory)
    {
        try
        {
            return (File.GetAttributes(directory) & FileAttributes.ReparsePoint) != 0;
        }
        catch (IOException)
        {
            return true;
        }
        catch (UnauthorizedAccessException)
        {
            return true;
        }
    }
}