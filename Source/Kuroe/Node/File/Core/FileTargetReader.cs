using Kuroe.Flow.Compilation.Shared.NodeTypes;

namespace Kuroe.Node.File;

/// <summary>文件节点参数段的读取：Path 必填，路径变量映射与解析边界由运行期使用。</summary>
internal static class FileTargetReader
{
    /// <summary>读出文件路径、路径变量与字符上限，路径缺失或上限为负数时经 fail 报出。</summary>
    internal static FileParameters Read(
        IParameterReader values,
        Action<string> fail,
        string nodeName,
        string rootDirectory,
        int defaultMaxChars)
    {
        IReadOnlyDictionary<string, string> variables = values.Mappings(FileParameterNames.Variables);
        string? path = values.Text(FileParameterNames.Path);
        if (string.IsNullOrWhiteSpace(path))
        {
            fail($"{nodeName}必须声明 Path。");

            return new FileParameters
            {
                Path = string.Empty,
                Variables = variables,
                RootDirectory = rootDirectory,
                MaxChars = defaultMaxChars,
            };
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
            Variables = variables,
            RootDirectory = rootDirectory,
            MaxChars = maxChars ?? defaultMaxChars,
        };
    }
}

/// <summary>文件节点参数段的字段名。</summary>
internal static class FileParameterNames
{
    /// <summary>文件路径字段。</summary>
    internal const string Path = "Path";

    /// <summary>路径变量映射字段。</summary>
    internal const string Variables = "Variables";

    /// <summary>字符上限字段，0 表示不限制。</summary>
    internal const string MaxChars = "MaxChars";
}