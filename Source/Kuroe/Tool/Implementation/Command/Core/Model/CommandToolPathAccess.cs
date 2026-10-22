using ErrorOr;

namespace Kuroe.Tool.Command.Model;

/// <summary>路径参数的准入规则：目标必须落在工作目录内，白名单与黑名单在此范围内收窄。
/// 实参按执行目录解析，条目按工作目录解析，匹配按目录段前缀，黑名单优先。不解析符号链接与目录联接点。</summary>
/// <param name="Allow">白名单条目，为空时不限制。</param>
/// <param name="Deny">黑名单条目，命中即拒绝。</param>
public sealed record CommandToolPathAccess(IReadOnlyList<string> Allow, IReadOnlyList<string> Deny)
{
    /// <summary>空策略：只执行工作目录边界。</summary>
    public static CommandToolPathAccess Empty { get; } = new([], []);

    /// <summary>校验一个路径实参。baseDirectory 是命令执行目录，rootDirectory 是工作目录。</summary>
    public ErrorOr<Success> Check(string value, string baseDirectory, string rootDirectory)
    {
        string full;
        try
        {
            full = Path.GetFullPath(value, baseDirectory);
        }
        catch (Exception ex) when (ex is ArgumentException or NotSupportedException or IOException)
        {
            return ToolErrors.Path($"路径 {value} 无法解析：{ex.Message}");
        }

        if (!IsUnder(rootDirectory, full))
        {
            return ToolErrors.Path($"路径 {value} 不在工作目录内。");
        }

        try
        {
            if (Deny.Any(entry => IsUnder(Path.GetFullPath(entry, rootDirectory), full)))
            {
                return ToolErrors.Path($"路径 {value} 在黑名单内。");
            }

            if (Allow.Count > 0 && !Allow.Any(entry => IsUnder(Path.GetFullPath(entry, rootDirectory), full)))
            {
                return ToolErrors.Path($"路径 {value} 不在白名单内。");
            }
        }
        catch (Exception ex) when (ex is ArgumentException or NotSupportedException or IOException)
        {
            return ToolErrors.Internal($"路径规则条目无法解析：{ex.Message}");
        }

        return Result.Success;
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