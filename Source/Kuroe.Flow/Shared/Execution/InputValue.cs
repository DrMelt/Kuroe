using Kuroe.Flow.Shared.StaticGraph.PortModel;

namespace Kuroe.Flow.Shared.Execution;

/// <summary>进入某个输入端口的一段取值。端口名保留目标侧身份，取值保留出处与文本。</summary>
/// <param name="Port">目标输入端口名。</param>
/// <param name="Value">取到的内容与它的出处。</param>
public sealed record InputValue(PortName Port, ContextValue Value)
{
    /// <summary>取值文本。</summary>
    public string Text => Value.Text;

    /// <summary>取值出处。</summary>
    public ContextSource Origin => Value.Origin;
}