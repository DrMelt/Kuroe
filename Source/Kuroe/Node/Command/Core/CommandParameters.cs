namespace Kuroe.Node.Command;

/// <summary>命令节点的类型专属声明：程序、参数、执行目录原文、路径变量映射、边界根、超时、输出上限与退出码策略。</summary>
public sealed record CommandParameters : Kuroe.Execution.Shared.IExecutionParameters
{
    /// <summary>要执行的程序，程序名或路径。</summary>
    public required string Program { get; init; }

    /// <summary>传给程序的参数，逐项传入，不经 shell 解释，可含 `{变量}` 占位符。</summary>
    public required IReadOnlyList<string> Arguments { get; init; }

    /// <summary>执行目录原文，可含路径变量；为空时执行目录即变量基准。</summary>
    public required string? Directory { get; init; }

    /// <summary>命令变量名到输入端口名的映射，变量用于 Directory 与 Arguments 的占位符。</summary>
    public IReadOnlyDictionary<string, string> Variables { get; init; } = new Dictionary<string, string>();

    /// <summary>工作区根目录，解析后的路径必须落在它之内。</summary>
    public required string RootDirectory { get; init; }

    /// <summary>等待上限秒数。</summary>
    public required int TimeoutSeconds { get; init; }

    /// <summary>输出字符上限，0 表示不限制。</summary>
    public required int MaxChars { get; init; }

    /// <summary>退出码非零时是否停在阻塞，未声明时不阻塞。</summary>
    public bool FailOnNonZeroExitCode { get; init; }
}