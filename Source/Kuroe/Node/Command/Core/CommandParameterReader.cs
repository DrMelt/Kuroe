using Kuroe.Flow.Shared.Descriptions;

namespace Kuroe.Node.Command;

/// <summary>命令节点参数段的读取：Program 必填，执行目录保留原文，路径变量与解析边界由运行期使用。</summary>
internal static class CommandParameterReader
{
    /// <summary>读出程序、参数、执行目录、路径变量、超时与输出上限，缺失或非法时经 fail 报出。</summary>
    internal static CommandParameters Read(
        IParameterReader values,
        Action<string> fail,
        string rootDirectory)
    {
        IReadOnlyDictionary<string, string> variables = values.Mappings(CommandParameterNames.Variables);
        string? program = values.Text(CommandParameterNames.Program);
        if (string.IsNullOrWhiteSpace(program))
        {
            fail("命令执行节点必须声明 Program。");

            program = string.Empty;
        }

        IReadOnlyList<string> arguments = values.Texts(CommandParameterNames.Arguments);

        string? directory = values.Text(CommandParameterNames.Directory);

        int? timeout = values.Integer(CommandParameterNames.TimeoutSeconds);
        if (timeout is <= 0)
        {
            fail("TimeoutSeconds 必须是正整数。");

            timeout = null;
        }

        int? maxChars = values.Integer(CommandParameterNames.MaxChars);
        if (maxChars is < 0)
        {
            fail("MaxChars 不能是负数。");

            maxChars = null;
        }

        return new CommandParameters
        {
            Program = program,
            Arguments = arguments,
            Directory = string.IsNullOrWhiteSpace(directory) ? null : directory,
            Variables = variables,
            RootDirectory = rootDirectory,
            TimeoutSeconds = timeout ?? CommandNodeTypes.DefaultTimeoutSeconds,
            MaxChars = maxChars ?? CommandNodeTypes.DefaultMaxChars,
        };
    }
}

/// <summary>命令节点参数段的字段名。</summary>
internal static class CommandParameterNames
{
    /// <summary>程序字段。</summary>
    internal const string Program = "Program";

    /// <summary>参数数组字段。</summary>
    internal const string Arguments = "Arguments";

    /// <summary>执行目录字段。</summary>
    internal const string Directory = "Directory";

    /// <summary>路径变量映射字段。</summary>
    internal const string Variables = "Variables";

    /// <summary>等待上限秒数字段。</summary>
    internal const string TimeoutSeconds = "TimeoutSeconds";

    /// <summary>输出字符上限字段。</summary>
    internal const string MaxChars = "MaxChars";
}