namespace Kuroe.Node.File;

/// <summary>文件节点的类型专属声明：文件路径的原文与按工作目录解析出的绝对路径。</summary>
public sealed record FileParameters
{
    /// <summary>文件里写下的路径，用于展示与错误说明。</summary>
    public required string Path { get; init; }

    /// <summary>按工作目录解析出的绝对路径，文件读写以它为准。</summary>
    public required string FullPath { get; init; }

    /// <summary>单次读写的字符上限，0 表示不限制。</summary>
    public required int MaxChars { get; init; }
}