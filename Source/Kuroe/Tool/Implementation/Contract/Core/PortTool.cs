using ErrorOr;
using Kuroe.Flow.Shared.RuntimeGraph.Execution;
using Kuroe.Flow.Shared.RuntimeGraph.Ports;
using Kuroe.Flow.Shared.StaticGraph.Ports;

namespace Kuroe.Tool.Contract;

/// <summary>需交回输出端口的执行节点交回命名端口产出的通道。载体按执行建立，提交时才知道是哪个执行在交。</summary>
/// <remarks>端口清单来自该执行的输出声明。</remarks>
public sealed class PortTool(IPortSubmitter intake, ExecutionId execution, IReadOnlyList<Port>? ports) : ITool
{
    /// <summary>本载体的函数声明。</summary>
    public IReadOnlyList<ToolFunction> Functions { get; } = Declare(intake, execution, ports);

    /// <summary>把契约定义绑上提交该执行端口产出的调用体，有端口清单时写进说明。</summary>
    private static IReadOnlyList<ToolFunction> Declare(IPortSubmitter intake, ExecutionId execution, IReadOnlyList<Port>? ports) =>
    [
        ContractToolDefinitions.Port(ports).Bind(arguments => Submit(intake, execution, ports, arguments)),
    ];

    /// <summary>提交端口产出并把提交后的本轮契约状态写成回执，错误原样交回模型。</summary>
    private static ErrorOr<string> Submit(IPortSubmitter intake, ExecutionId execution, IReadOnlyList<Port>? ports, ToolArguments arguments)
    {
        ErrorOr<IReadOnlyDictionary<string, PortValue>> parsed =
            PortValues.Parse(arguments.Text(new ToolName("valuesJson")) ?? string.Empty, ports);
        if (parsed.IsError)
        {
            return parsed.ErrorsOrEmptyList;
        }

        ErrorOr<ExecutionStatus> outcome = intake.SubmitValues(execution, parsed.Value);
        if (outcome.IsError)
        {
            return outcome.ErrorsOrEmptyList;
        }

        int silent = parsed.Value.Values.Count(value => !value.Fires);
        string note = silent > 0 ? $"，其中 {silent} 个端口不触发下游" : string.Empty;

        return $"已记录 {parsed.Value.Count} 个端口的产出{note}。{ContractReceipts.Needs(outcome.Value)}";
    }
}
