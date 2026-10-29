using Kuroe.Flow.Shared.RuntimeGraph.Execution;
using Kuroe.Flow.Shared.StaticGraph.Ports;
using Kuroe.Node.Llm.Tools;

namespace Kuroe.Node.Llm.Executions;

/// <summary>本轮产出契约的补交要求：声明了输出端口却未交回时，给出写给模型的补交要求。</summary>
internal sealed class ContractReminder(IExecutionStatusReader status)
{
    /// <summary>还缺交回内容时给出补交要求，齐备时为空。</summary>
    public string? Text(ExecutionId execution)
    {
        if (status.Read(execution) is not { IsError: false } read)
        {
            return null;
        }

        ExecutionStatus round = read.Value;

        // 已上报无法完成的执行，缺项是它主动留下的，不再追问
        if (round.CannotCompleteReason is not null || round.OutstandingPorts.Count == 0)
        {
            return null;
        }

        string ports = string.Join('、', round.OutstandingPorts.Select(name => $"「{name}」"));

        return $"本轮还缺交回：输出端口{ports}，请调用 {ContractTools.Port} 提交。";
    }
}