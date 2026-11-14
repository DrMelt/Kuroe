namespace Kuroe.Execution.Shared;

/// <summary>执行目标类型提供的参数。流程控制按目标类型取出参数，执行单元不解释具体类型。</summary>
public interface IExecutionParameterProvider
{
    /// <summary>按本次执行的输入建立类型专属参数。</summary>
    IExecutionParameters Create(ExecutionParameterContext context);
}