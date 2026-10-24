using ErrorOr;

namespace Kuroe.Tool;

/// <summary>路径参数的解析范围：实参按基准目录解析，解析后必须落在根目录内。
/// 与工具层级路径无关；白名单与黑名单在根目录范围内收窄。</summary>
/// <param name="baseDirectory">实参的解析基准目录。</param>
/// <param name="rootDirectory">允许的根目录，两个目录都是绝对路径。</param>
/// <param name="rootName">根目录名，错误文案与空路径展示都用它。</param>
public sealed class ToolPathScope(string baseDirectory, string rootDirectory, string rootName)
{
    private readonly string _baseDirectory = baseDirectory;
    private readonly string _rootDirectory = rootDirectory;
    private readonly string _rootName = rootName;

    /// <summary>把路径解析为根目录内的绝对路径。越界、不可解析或不符合准入规则时给出拒绝错误。</summary>
    public ErrorOr<string> Resolve(string path, ToolPathAccess? access = null)
    {
        string full;
        try
        {
            full = Path.GetFullPath(path, _baseDirectory);
        }
        catch (Exception ex) when (ex is ArgumentException or NotSupportedException or IOException)
        {
            return ToolErrors.Path($"路径 {Display(path)} 无法解析：{ex.Message}");
        }

        if (!ToolPathAccess.IsUnder(_rootDirectory, full))
        {
            return ToolErrors.Path($"路径 {Display(path)} 不在{_rootName}内。");
        }

        if (access is not null)
        {
            ErrorOr<Success> checkedValue = access.Check(path, full, _rootDirectory);
            if (checkedValue.IsError)
            {
                return checkedValue.ErrorsOrEmptyList;
            }
        }

        return full;
    }

    /// <summary>路径的展示。空路径即根目录。</summary>
    public string Display(string path) => path.Length == 0 ? $"{_rootName}根" : path;
}