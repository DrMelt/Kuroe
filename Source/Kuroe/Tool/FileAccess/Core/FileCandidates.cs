using ErrorOr;

namespace Kuroe.Tool.FileAccess;

/// <summary>把路径范围展开成文件集合：目录递归收集，显式文件直接加入，按完整路径去重并按序数排序。
/// 路径解析与边界沿用文件作用域，递归时跳过路径准入规则命中的目录与重解析点目录。</summary>
/// <param name="scope">候选文件的访问作用域。</param>
/// <param name="access">路径准入规则，null 表示不限制。</param>
public sealed class FileCandidates(FileScope scope, ToolPathAccess? access = null)
{
    private static readonly StringComparer PathComparer = OperatingSystem.IsWindows()
        ? StringComparer.OrdinalIgnoreCase
        : StringComparer.Ordinal;

    private readonly FileScope _scope = scope;
    private readonly ToolPathAccess? _access = access;

    /// <summary>展开路径范围并按完整路径去重，结果按序数排序。空集合表示路径基准目录。</summary>
    public ErrorOr<IReadOnlyList<string>> Expand(IReadOnlyList<string> paths)
    {
        IReadOnlyList<string> scopes = paths.Count == 0 ? [string.Empty] : paths;
        HashSet<string> files = new(PathComparer);

        foreach (string path in scopes)
        {
            ErrorOr<string> resolved = _scope.Resolve(path);
            if (resolved.IsError)
            {
                return resolved.ErrorsOrEmptyList;
            }

            if (_access is not null)
            {
                ErrorOr<Success> allowed = _access.Check(path, resolved.Value, _scope.Root);
                if (allowed.IsError)
                {
                    return allowed.ErrorsOrEmptyList;
                }
            }

            if (File.Exists(resolved.Value))
            {
                files.Add(resolved.Value);
                continue;
            }

            if (!Directory.Exists(resolved.Value))
            {
                return ToolErrors.Path($"{_scope.Display(path)} 不存在。");
            }

            Collect(resolved.Value, files);
        }

        return files.OrderBy(file => file, StringComparer.Ordinal).ToArray();
    }

    /// <summary>递归收集目录内的文件，跳过路径准入规则命中的目录与重解析点目录。</summary>
    private void Collect(string directory, HashSet<string> files)
    {
        Queue<string> pending = new();
        pending.Enqueue(directory);

        while (pending.Count > 0)
        {
            string current = pending.Dequeue();
            if (IsExcluded(current) || IsReparsePoint(current))
            {
                continue;
            }

            try
            {
                foreach (string file in Directory.EnumerateFiles(current))
                {
                    files.Add(file);
                }

                foreach (string child in Directory.EnumerateDirectories(current))
                {
                    pending.Enqueue(child);
                }
            }
            catch (IOException)
            {
                // 目录读取失败时跳过
            }
            catch (UnauthorizedAccessException)
            {
                // 目录不可访问时跳过
            }
        }
    }

    /// <summary>该目录是否被路径准入规则排除。</summary>
    private bool IsExcluded(string directory) =>
        _access is not null && !_access.Allows(directory, _scope.Root);

    /// <summary>该目录是否是重解析点，读取属性失败时按是处理。</summary>
    private static bool IsReparsePoint(string directory)
    {
        try
        {
            return (File.GetAttributes(directory) & FileAttributes.ReparsePoint) != 0;
        }
        catch (IOException)
        {
            return true;
        }
        catch (UnauthorizedAccessException)
        {
            return true;
        }
    }
}