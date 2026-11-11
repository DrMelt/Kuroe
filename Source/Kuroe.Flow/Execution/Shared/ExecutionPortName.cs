using ErrorOr;

namespace Kuroe.Flow.Execution;

/// <summary>执行单元的数据交换端口名。展示即原文。</summary>
public readonly record struct ExecutionPortName(string Value)
{
    /// <summary>解析执行端口名，拒绝空、全空白或含 @ 的文本。</summary>
    public static ErrorOr<ExecutionPortName> Create(string name) =>
        string.IsNullOrWhiteSpace(name) || name.Contains('@')
            ? [Error.Validation(ExecutionErrorCodes.Port, "执行端口名不能为空或含 @。")]
            : new ExecutionPortName(name.Trim());

    /// <summary>执行端口名的文本形式。</summary>
    public override string ToString() => Value;
}