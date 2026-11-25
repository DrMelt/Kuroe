using ErrorOr;
using Kuroe.Kernel.Execution.Shared.Contract;
using Kuroe.Kernel.Execution.Shared.Ports;
using Kuroe.Kernel.Execution.Shared.Runtime;

namespace Kuroe.Ability.Tool.Implementation.Contract;

/// <summary>需交回输出端口的执行节点交回命名端口产出的通道。载体按执行建立，提交时才知道是哪个执行在交。</summary>
/// <remarks>端口清单来自该执行的输出声明。</remarks>
public sealed class PortTool(
    IExecutionRequestEntry requests,
    IReadOnlyList<ExecutionOutputPort>? ports) : ITool
{
    /// <summary>本载体的函数声明。</summary>
    public IReadOnlyList<ToolFunction> Functions { get; } = Declare(requests, ports);

    /// <summary>把契约定义绑上提交该执行端口产出的调用体，有端口清单时写进参数 schema。</summary>
    private static IReadOnlyList<ToolFunction> Declare(
        IExecutionRequestEntry requests,
        IReadOnlyList<ExecutionOutputPort>? ports) =>
    [
        ContractToolDefinitions.Port(ports).Bind(arguments => Submit(requests, ports, arguments)),
    ];

    /// <summary>提交端口产出并把提交后的本轮契约状态写成回执，错误原样交回模型。</summary>
    private static ErrorOr<string> Submit(
        IExecutionRequestEntry requests,
        IReadOnlyList<ExecutionOutputPort>? ports,
        ToolArguments arguments)
    {
        ErrorOr<IReadOnlyList<ExecutionOutputValue>> parsed =
            PortValues.Parse(arguments.Json(new ToolName("valuesJson")) ?? string.Empty, ports);
        if (parsed.IsError)
        {
            return parsed.ErrorsOrEmptyList;
        }

        ErrorOr<ExecutionStatus> outcome = requests.SubmitOutputs(parsed.Value);
        if (outcome.IsError)
        {
            return outcome.ErrorsOrEmptyList;
        }

        HashSet<ExecutionPortName> triggerPorts = [.. (ports ?? []).Where(port => port.CarriesTrigger).Select(port => port.Name)];
        int silent = parsed.Value.Count(value => triggerPorts.Contains(value.Port) && !value.Fires);
        string silentNote = silent > 0 ? $"，其中 {silent} 个端口不触发下游" : string.Empty;

        return $"已记录 {parsed.Value.Count} 个端口的产出{silentNote}。{ContractReceipts.Needs(outcome.Value)}";
    }
}