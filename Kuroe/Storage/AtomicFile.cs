namespace Kuroe.Storage;

/// <summary>写盘的原子性保证：先写临时文件再替换，中断不会留下半份文件。IO 异常由调用方处理。</summary>
public static class AtomicFile
{
    /// <summary>写入文本，目标目录不存在时创建。</summary>
    public static void WriteText(string path, string text)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(path))!);

        string temporary = path + ".tmp";
        File.WriteAllText(temporary, text);
        File.Move(temporary, path, overwrite: true);
    }
}