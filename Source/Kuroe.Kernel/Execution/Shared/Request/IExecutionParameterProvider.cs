namespace Kuroe.Execution.Shared.Request;

/// <summary>执行参数的提供者。流程控制按具体提供者取出参数，执行单元不解释具体类型。</summary>
public interface IExecutionParameterProvider
{
    /// <summary>按本次执行的输入建立类型专属参数。</summary>
    IExecutionParameters Create(ExecutionParameterContext context);
}