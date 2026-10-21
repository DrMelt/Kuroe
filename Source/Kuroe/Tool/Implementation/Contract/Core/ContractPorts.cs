using Kuroe.Flow.Runtime.Tasks;
using Kuroe.Flow.Shared.Execution;
using Kuroe.Flow.Shared.StaticGraph.PortModel;

namespace Kuroe.Tool.Contract;

/// <summary>执行声明的需交回端口。契约工具换载体时经它取交回清单。</summary>
internal static class ContractPorts
{
    /// <summary>该执行需交回的输出端口，没有时为空。</summary>
    internal static IReadOnlyList<Port>? DeclaredPorts(TaskRegistry registry, ExecutionId execution) =>
        registry.FindExecution(execution) is { IsError: false } found && found.Value.Input.RequiredPorts.Count > 0
            ? found.Value.Input.RequiredPorts
            : null;
}