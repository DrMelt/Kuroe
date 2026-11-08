using System.Text;

namespace Kuroe.Files.Shared;

/// <summary>工作区文件访问端口。只表达文件与目录操作，路径边界与准入由调用方负责。</summary>
public interface IWorkspaceFiles
{
    /// <summary>文件是否存在。</summary>
    bool FileExists(string path);

    /// <summary>目录是否存在。</summary>
    bool DirectoryExists(string path);

    /// <summary>目录的直接子项完整路径，文件与目录都在其中。目录不存在或不可访问时失败。</summary>
    IReadOnlyList<string> Entries(string directory);

    /// <summary>目录的直接子文件完整路径。目录不存在或不可访问时失败。</summary>
    IReadOnlyList<string> Files(string directory);

    /// <summary>目录的直接子目录完整路径。目录不存在或不可访问时失败。</summary>
    IReadOnlyList<string> Directories(string directory);

    /// <summary>取文件的文件系统事实。文件不存在或不可访问时失败。</summary>
    FileFacts Inspect(string file);

    /// <summary>打开文件读取流，调用方负责释放。文件不存在或不可访问时失败。</summary>
    Stream OpenRead(string file);

    /// <summary>读取文件全部字节。文件不存在或不可访问时失败。</summary>
    byte[] ReadBytes(string file);

    /// <summary>按编码写入文本，编码的前导字节一并写入。父目录必须存在。</summary>
    void WriteText(string path, string text, Encoding encoding);

    /// <summary>建立目录，父目录缺失时一并建立。</summary>
    void CreateDirectory(string path);

    /// <summary>删除文件，文件不存在时视为成功。</summary>
    void DeleteFile(string path);

    /// <summary>删除目录，目录不存在或不可访问时失败。</summary>
    void DeleteDirectory(string path, bool recursive);

    /// <summary>移动文件，目标已存在或目标父目录缺失时失败。</summary>
    void MoveFile(string source, string target);

    /// <summary>移动目录，目标已存在或目标父目录缺失时失败。</summary>
    void MoveDirectory(string source, string target);

    /// <summary>目录是否是重解析点，读取属性失败时按是处理。</summary>
    bool IsReparsePoint(string directory);
}