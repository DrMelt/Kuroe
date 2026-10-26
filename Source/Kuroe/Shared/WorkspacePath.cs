namespace Kuroe.Shared;

/// <summary>工作区根目录内的路径边界：相对路径按根目录或基准目录解析，解析结果必须落在根目录内。
/// 只做字符串解析与归一，不解析符号链接与目录联接点。</summary>
public static class WorkspacePath
{
    /// <summary>把路径解析为根目录内的绝对路径，越界或不可解析时返回 false。</summary>
    public static bool TryResolve(string rootDirectory, string path, out string fullPath) =>
        TryResolveWithin(rootDirectory, rootDirectory, path, out fullPath);

    /// <summary>把路径按基准目录解析为绝对路径，解析结果必须落在根目录内，越界或不可解析时返回 false。</summary>
    public static bool TryResolveWithin(
        string rootDirectory,
        string baseDirectory,
        string path,
        out string fullPath)
    {
        try
        {
            fullPath = Path.GetFullPath(path, baseDirectory);
        }
        catch (Exception ex) when (ex is ArgumentException or NotSupportedException or IOException)
        {
            fullPath = string.Empty;

            return false;
        }

        return IsUnder(rootDirectory, fullPath);
    }

    /// <summary>目标路径是否落在根目录内：目标等于根目录或位于其下。两个参数都是绝对路径。</summary>
    public static bool IsUnder(string rootDirectory, string path)
    {
        string relative = Path.GetRelativePath(rootDirectory, path);

        return !Path.IsPathRooted(relative)
            && relative != ".."
            && !relative.StartsWith($"..{Path.DirectorySeparatorChar}", StringComparison.Ordinal);
    }
}