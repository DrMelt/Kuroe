namespace Kuroe.Execution.Shared;

/// <summary>执行契约的写入口：运行器经工具交回端口产出或上报无法完成。提交者认定与执行归属由执行网关与契约工具完成。</summary>
public interface IExecutionContractWriter
{
    /// <summary>记下执行交回的端口值。</summary>
    void SubmitOutputs(IExecution execution, IReadOnlyDictionary<ExecutionPortName, ExecutionOutputValue> outputs);

    /// <summary>记下执行上报的无法完成原因。</summary>
    void ReportCannotComplete(IExecution execution, string reason);
}