using Kuroe.Flow.Shared.StaticGraph.PortModel;

namespace Kuroe.Flow.Shared.Execution;

/// <summary>执行输入的端口访问适配：端口取值与输出声明来自执行输入。</summary>
/// <param name="input">本次执行的输入。</param>
public sealed class ExecutionPortAccess(ExecutionInput input) : IPortAccess
{
    /// <inheritdoc/>
    public IReadOnlyList<Port> Outputs => input.OutputPorts;

    /// <inheritdoc/>
    public IReadOnlyDictionary<PortName, string> Texts => input.Texts;
}
