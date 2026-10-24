namespace Kuroe.Node.Command;

/// <summary>命令节点的类型专属声明：程序、参数、执行目录、超时与输出上限。</summary>
public sealed record CommandParameters
{
    /// <summary>要执行的程序，程序名或路径。</summary>
    public required string Program { get; init; }

    /// <summary>传给程序的参数，逐项传入，不经 shell 解释。</summary>
    public required IReadOnlyList<string> Arguments { get; init; }

    /// <summary>按工作目录解析出的执行目录绝对路径。</summary>
    public required string FullDirectory { get; init; }

    /// <summary>等待上限秒数。</summary>
    public required int TimeoutSeconds { get; init; }

    /// <summary>输出字符上限，0 表示不限制。</summary>
    public required int MaxChars { get; init; }
}