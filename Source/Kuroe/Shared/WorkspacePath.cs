namespace Kuroe.Shared;

/// <summary>工作区根目录内的路径边界：相对路径按根目录或基准目录解析，解析结果必须落在根目录内。
/// 只做字符串解析与归一，不解析符号链接、目录联接点与硬链接。</summary>
public static class WorkspacePath
{
    /// <summary>路径比较的平台规则：Windows 文件系统不区分大小写，其他平台区分。</summary>
    public static StringComparison Comparison { get; } =
        OperatingSystem.IsWindows() ? StringComparison.OrdinalIgnoreCase : StringComparison.Ordinal;

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
    public static bool IsUnder(string rootDirectory, string path) =>
        IsUnder(rootDirectory, path, Comparison);

    /// <summary>目标路径是否落在根目录内，按给定比较规则。两个参数都是绝对路径。</summary>
    public static bool IsUnder(string rootDirectory, string path, StringComparison comparison)
    {
        string root = Path.TrimEndingDirectorySeparator(Path.GetFullPath(rootDirectory));
        string target = Path.TrimEndingDirectorySeparator(Path.GetFullPath(path));

        if (string.Equals(root, target, comparison))
        {
            return true;
        }

        string prefix = root.EndsWith(Path.DirectorySeparatorChar) ? root : root + Path.DirectorySeparatorChar;

        return target.StartsWith(prefix, comparison);
    }

    /// <summary>两个路径是否指向同一位置：忽略目录的尾分隔符，按平台的文件系统大小写规则比较。两个参数都是绝对路径。</summary>
    public static bool IsSame(string left, string right) =>
        IsSame(left, right, Comparison);

    /// <summary>两个路径是否指向同一位置，按给定比较规则。两个参数都是绝对路径。</summary>
    public static bool IsSame(string left, string right, StringComparison comparison) =>
        IsUnder(left, right, comparison) && IsUnder(right, left, comparison);

    /// <summary>目标路径是否严格位于目录之内：不含目录本身，忽略目录的尾分隔符。两个参数都是绝对路径。</summary>
    public static bool IsWithin(string directory, string path) =>
        IsWithin(directory, path, Comparison);

    /// <summary>目标路径是否严格位于目录之内，按给定比较规则。两个参数都是绝对路径。</summary>
    public static bool IsWithin(string directory, string path, StringComparison comparison) =>
        IsUnder(directory, path, comparison) && !IsSame(directory, path, comparison);
}