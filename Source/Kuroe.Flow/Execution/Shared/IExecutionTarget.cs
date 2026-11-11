namespace Kuroe.Flow.Execution;

/// <summary>一次执行的目标：标识、种类与类型专属参数。执行单元只读取这些信息。</summary>
public interface IExecutionTarget
{
    /// <summary>执行目标标识。</summary>
    ExecutionTargetId Id { get; }

    /// <summary>执行种类，用于选择运行器。</summary>
    string Kind { get; }

    /// <summary>类型专属参数。</summary>
    IExecutionParameters Parameters { get; }
}