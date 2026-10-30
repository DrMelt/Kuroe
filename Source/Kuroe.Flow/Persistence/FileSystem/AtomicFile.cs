using System.Text;

namespace Kuroe.Flow.Persistence.FileSystem;

/// <summary>写盘的原子性保证：先写临时文件再替换，中断不会留下半份文件。IO 异常由调用方处理。</summary>
public static class AtomicFile
{
    /// <summary>写入文本，目标目录不存在时创建。临时文件带进程号，多个进程写同一目标时不互相覆盖。</summary>
    public static void WriteText(string path, string text)
    {
        string target = Path.GetFullPath(path);
        string directory = Path.GetDirectoryName(target)!;
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
}
