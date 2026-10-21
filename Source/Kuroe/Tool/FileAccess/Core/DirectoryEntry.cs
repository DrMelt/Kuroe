namespace Kuroe.Tool.FileAccess;

/// <summary>目录条目的名字与类型：是否为目录。不含路径与内容。</summary>
/// <param name="Name">条目名，不含目录部分。</param>
/// <param name="IsDirectory">条目是否为目录。</param>
public readonly record struct DirectoryEntry(string Name, bool IsDirectory);