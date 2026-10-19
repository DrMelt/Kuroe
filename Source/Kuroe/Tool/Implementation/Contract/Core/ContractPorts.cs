using Kuroe.Flow.Runtime.Tasks;
using Kuroe.Flow.Shared.Execution;
using Kuroe.Flow.Shared.StaticGraph;

namespace Kuroe.Tool.Contract;

/// <summary>执行声明的命名端口。契约工具换载体时经它取交回清单。</summary>
internal static class ContractPorts
{
    /// <summary>该执行声明的 Text 输出端口，没有声明时为空。</summary>
    internal static IReadOnlyList<Port>? DeclaredTextPorts(TaskRegistry registry, ExecutionId execution) =>
        registry.FindExecution(execution) is { IsError: false } found && found.Value.Input.TextPorts.Count > 0
            ? found.Value.Input.TextPorts
            : null;
}