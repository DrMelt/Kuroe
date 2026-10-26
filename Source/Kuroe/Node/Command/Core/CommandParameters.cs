namespace Kuroe.Node.Command;

/// <summary>命令节点的类型专属声明：程序、参数、执行目录原文、边界根、超时与输出上限。</summary>
public sealed record CommandParameters
{
    /// <summary>要执行的程序，程序名或路径。</summary>
    public required string Program { get; init; }

    /// <summary>传给程序的参数，逐项传入，不经 shell 解释。</summary>
    public required IReadOnlyList<string> Arguments { get; init; }

    /// <summary>执行目录原文，相对基准路径解析；为空时执行目录即基准路径。</summary>
    public required string? Directory { get; init; }

    /// <summary>工作区根目录，解析后的路径必须落在它之内。</summary>
    public required string RootDirectory { get; init; }

    /// <summary>等待上限秒数。</summary>
    public required int TimeoutSeconds { get; init; }

    /// <summary>输出字符上限，0 表示不限制。</summary>
    public required int MaxChars { get; init; }
}