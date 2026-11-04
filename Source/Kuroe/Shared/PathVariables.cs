using ErrorOr;

namespace Kuroe.Shared;

/// <summary>路径变量的规则入口与运行期解析：装载期校验变量与占位符，运行期把变量解析为工作区内的路径。</summary>
public static class PathVariables
{
    /// <summary>作为相对路径基准的保留变量名。</summary>
    public const string RootName = "root";

    /// <summary>运行期解析错误的错误码。</summary>
    private const string PathErrorCode = "Path";

    /// <summary>校验映射与占位符，返回全部错误。portIsText 对未声明端口返回 null。</summary>
    public static IReadOnlyList<string> Validate(
        IReadOnlyDictionary<string, string> variables,
        IReadOnlyList<string> placeholders,
        Func<string, bool?> portIsText) =>
        VariableMappings.Validate(variables, placeholders, portIsText, "路径", "Path");

    /// <summary>从映射端口读出变量值并解析为工作区内的绝对路径，root 变量给出相对路径基准。
    /// 端口未连接、取值为空或越出工作区时给出错误。</summary>
    public static ErrorOr<PathResolution> Resolve(
        IReadOnlyDictionary<string, string> variables,
        Func<string, string?> portText,
        string rootDirectory)
    {
        Dictionary<string, string> resolved = [];
        foreach ((string name, string port) in variables)
        {
            string? value = portText(port);
            if (string.IsNullOrWhiteSpace(value))
            {
                return Error.Validation(PathErrorCode, $"路径变量 {name} 未连接或为空。");
            }

            if (!WorkspacePath.TryResolve(rootDirectory, value, out string fullPath))
            {
                return Error.Validation(PathErrorCode, $"路径变量 {name} 的值 {value} 不是工作区内的合法路径。");
            }

            resolved[name] = fullPath;
        }

        string baseDirectory = resolved.TryGetValue(RootName, out string? root) ? root : rootDirectory;

        return new PathResolution(resolved, baseDirectory);
    }

    /// <summary>展开路径模板并解析为工作区内的绝对路径。模板含未映射变量或解析越界时给出错误，label 是错误说明里的字段名。</summary>
    public static ErrorOr<string> Expand(
        string template,
        IReadOnlyDictionary<string, string> variables,
        string baseDirectory,
        string rootDirectory,
        string label)
    {
        string expanded = PathTemplate.Expand(template, variables);
        if (PathTemplate.Placeholders(expanded).Count > 0)
        {
            return Error.Validation(PathErrorCode, $"{label} {template} 含未映射的路径变量。");
        }

        return WorkspacePath.TryResolveWithin(rootDirectory, baseDirectory, expanded, out string fullPath)
            ? fullPath
            : Error.Validation(PathErrorCode, $"{label} {template} 不是工作区内的合法路径。");
    }
}

/// <summary>解析后的路径变量：变量值按工作区内的绝对路径给出，root 变量作为相对路径基准。</summary>
/// <param name="Variables">变量名到绝对路径的映射。</param>
/// <param name="BaseDirectory">相对路径的解析基准，没有 root 变量时是工作区根。</param>
public sealed record PathResolution(IReadOnlyDictionary<string, string> Variables, string BaseDirectory);