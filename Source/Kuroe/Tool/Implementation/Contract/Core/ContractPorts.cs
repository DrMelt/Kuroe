using Kuroe.Execution.Shared;

namespace Kuroe.Tool.Contract;

/// <summary>执行声明的需交回端口。契约工具按执行输出声明取交回清单。</summary>
internal static class ContractPorts
{
    /// <summary>该执行需交回的输出端口，没有时为空。</summary>
    internal static IReadOnlyList<ExecutionOutputPort>? DeclaredPorts(IReadOnlyList<ExecutionOutputPort> outputs)
    {
        IReadOnlyList<ExecutionOutputPort> required = [.. outputs.Where(port => port.Required)];

        return required.Count > 0 ? required : null;
    }
}
