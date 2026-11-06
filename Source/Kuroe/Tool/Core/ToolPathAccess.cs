using ErrorOr;
using Kuroe.Shared;

namespace Kuroe.Tool;

/// <summary>路径参数的准入规则：在路径已确认落在根目录内后，按白名单与黑名单收窄。
/// 条目按根目录解析，匹配按目录段前缀，黑名单优先。不解析符号链接、目录联接点与硬链接。</summary>
/// <param name="Allow">白名单条目，为空时不限制。</param>
/// <param name="Deny">黑名单条目，命中即拒绝。</param>
public sealed record ToolPathAccess(IReadOnlyList<string> Allow, IReadOnlyList<string> Deny)
{
    /// <summary>校验一个已解析的路径。value 是模型给出的原文，fullPath 是绝对路径，rootDirectory 是根目录。</summary>
    public ErrorOr<Success> Check(string value, string fullPath, string rootDirectory)
    {
        try
        {
            if (IsDenied(fullPath, rootDirectory))
            {
                return ToolErrors.Path($"路径 {value} 在黑名单内。");
            }

            if (!IsAllowed(fullPath, rootDirectory))
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

    /// <summary>该路径是否通过准入规则：不在黑名单内，且白名单为空或命中。规则条目无法解析时按拒绝处理。</summary>
    public bool Allows(string fullPath, string rootDirectory)
    {
        try
        {
            return !IsDenied(fullPath, rootDirectory) && IsAllowed(fullPath, rootDirectory);
        }
        catch (Exception ex) when (ex is ArgumentException or NotSupportedException or IOException)
        {
            return false;
        }
    }

    /// <summary>该路径是否命中黑名单。</summary>
    private bool IsDenied(string fullPath, string rootDirectory) =>
        Deny.Any(entry => IsUnder(Path.GetFullPath(entry, rootDirectory), fullPath));

    /// <summary>该路径是否落在白名单内，白名单为空时视为通过。</summary>
    private bool IsAllowed(string fullPath, string rootDirectory) =>
        Allow.Count == 0 || Allow.Any(entry => IsUnder(Path.GetFullPath(entry, rootDirectory), fullPath));

    /// <summary>目标路径是否落在根目录内：目标等于根目录或位于其下。两个参数都是绝对路径。</summary>
    public static bool IsUnder(string rootDirectory, string path) => WorkspacePath.IsUnder(rootDirectory, path);
}