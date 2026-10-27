using System.Text;
using ErrorOr;
using Kuroe.Shared;
using Kuroe.Tool.FileAccess;

namespace Kuroe.Tool.File;

/// <summary>文件访问工具面共用的写入上限、路径绑定与写回编码解析。</summary>
internal static class FileTools
{
    /// <summary>一次写入的字符上限，超出整段拒绝。</summary>
    internal const int ContentLimit = 40000;

    /// <summary>文件工具共用的路径准入：拒绝工作区根下的配置与仓库内部目录。</summary>
    internal static ToolPathAccess PathAccess { get; } = new([], [".kuroe", ".git"]);

    /// <summary>按执行绑定读取 root 变量，建立文件访问作用域。缺变量时作用域返回绑定错误。</summary>
    internal static FileScope Scope(ToolBinding binding)
    {
        string? root = binding.Variable(PathVariables.RootName);
        if (string.IsNullOrWhiteSpace(root))
        {
            return Failed(binding, $"路径变量 {PathVariables.RootName} 未映射或端口为空。");
        }

        return WorkspacePath.TryResolve(binding.WorkspaceDirectory, root, out string baseDirectory)
            ? new FileScope(baseDirectory, binding.WorkspaceDirectory, "路径基准", "工作区")
            : Failed(binding, $"路径变量 {PathVariables.RootName} 的值 {root} 不是工作区内的合法路径。");
    }

    /// <summary>建立带绑定错误的空作用域，根取工作区根。</summary>
    private static FileScope Failed(ToolBinding binding, string message) =>
        new(binding.WorkspaceDirectory, binding.WorkspaceDirectory, "路径基准", "工作区")
        {
            BindingError = ToolErrors.Path(message),
        };

    /// <summary>解析写回编码。缺省或空白为 UTF-8 无 BOM，只支持 UTF 族编码，未知值给出带实参的详细错误。</summary>
    internal static ErrorOr<Encoding> ParseEncoding(string? name) =>
        string.IsNullOrWhiteSpace(name)
            ? new UTF8Encoding(false)
            : name.Trim().ToLowerInvariant() switch
            {
                "utf-8" => new UTF8Encoding(false),
                "utf-8-bom" => new UTF8Encoding(true),
                "utf-16" => Encoding.Unicode,
                "utf-16be" => Encoding.BigEndianUnicode,
                _ => ToolErrors.Argument($"encoding 只支持 utf-8、utf-8-bom、utf-16、utf-16be，收到 {name}。"),
            };
}