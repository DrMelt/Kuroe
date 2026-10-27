namespace Kuroe.Node.File;

/// <summary>文件节点的类型专属声明：文件路径的原文、路径变量映射、路径解析的边界根与字符上限。</summary>
public sealed record FileParameters
{
    /// <summary>文件里写下的路径，可含路径变量，用于展示与错误说明。</summary>
    public required string Path { get; init; }

    /// <summary>路径变量名到输入端口名的映射。</summary>
    public IReadOnlyDictionary<string, string> Variables { get; init; } = new Dictionary<string, string>();

    /// <summary>工作区根目录，解析后的路径必须落在它之内。</summary>
    public required string RootDirectory { get; init; }

    /// <summary>单次读写的字符上限，0 表示不限制。</summary>
    public required int MaxChars { get; init; }
}