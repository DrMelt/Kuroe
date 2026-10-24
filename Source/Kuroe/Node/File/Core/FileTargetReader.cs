using Kuroe.Flow.Shared.Descriptions;
using Kuroe.Shared;

namespace Kuroe.Node.File;

/// <summary>文件节点参数段的读取：Path 必填，按工作目录解析并校验边界。</summary>
internal static class FileTargetReader
{
    /// <summary>读出文件路径与字符上限，路径缺失或越界、上限为负数时经 fail 报出。</summary>
    internal static FileParameters Read(
        IParameterReader values,
        Action<string> fail,
        string nodeName,
        string workingDirectory,
        int defaultMaxChars)
    {
        string? path = values.Text(FileParameterNames.Path);
        if (string.IsNullOrWhiteSpace(path))
        {
            fail($"{nodeName}必须声明 Path。");

            return new FileParameters { Path = string.Empty, FullPath = string.Empty, MaxChars = defaultMaxChars };
        }

        if (!WorkspacePath.TryResolve(workingDirectory, path, out string fullPath))
        {
            fail($"Path {path} 不是工作目录内的合法路径。");

            return new FileParameters { Path = path, FullPath = string.Empty, MaxChars = defaultMaxChars };
        }

        int? maxChars = values.Integer(FileParameterNames.MaxChars);
        if (maxChars is < 0)
        {
            fail("MaxChars 不能是负数。");
            maxChars = null;
        }

        return new FileParameters
        {
            Path = path,
            FullPath = fullPath,
            MaxChars = maxChars ?? defaultMaxChars,
        };
    }
}

/// <summary>文件节点参数段的字段名。</summary>
internal static class FileParameterNames
{
    /// <summary>文件路径字段。</summary>
    internal const string Path = "Path";

    /// <summary>字符上限字段，0 表示不限制。</summary>
    internal const string MaxChars = "MaxChars";
}