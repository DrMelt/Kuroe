using Kuroe.Flow.Shared.Model;

namespace Kuroe.Flow.Shared.RuntimeGraph.Ports;

/// <summary>运行期常量的取值来源：由任务运行状态给出 Once 节点唯一一次执行的产出文本，未产出时为空。</summary>
public interface IRuntimeConstantSource
{
    /// <summary>节点某常量输出端口是否已产出。非文本常量没有文本，仍以该判定表示就绪。</summary>
    bool Available(NodePath node, PortName port);

    /// <summary>取节点某常量输出端口的文本，节点未放行、未产出或端口不承载文本时为空。</summary>
    string? Text(NodePath node, PortName port);
}
