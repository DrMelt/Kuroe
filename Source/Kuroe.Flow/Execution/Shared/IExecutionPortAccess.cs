namespace Kuroe.Flow.Execution.Shared;

/// <summary>一次执行的端口访问：本次可读的端口文本与输出端口声明。工具按它读取运行期环境，不接触执行请求的具体形状。</summary>
public interface IExecutionPortAccess
{
    /// <summary>本次执行的输出端口声明。</summary>
    IReadOnlyList<ExecutionOutputPort> Outputs { get; }

    /// <summary>某个输入端口的文本，未装配时为空。</summary>
    string? Text(ExecutionPortName port);
}

/// <summary>按执行请求给出的端口访问。</summary>
public sealed class ExecutionPortAccess(ExecutionRequest request) : IExecutionPortAccess
{
    /// <inheritdoc/>
    public IReadOnlyList<ExecutionOutputPort> Outputs => request.OutputPorts;

    /// <inheritdoc/>
    public string? Text(ExecutionPortName port) => request.Texts.GetValueOrDefault(port);
}