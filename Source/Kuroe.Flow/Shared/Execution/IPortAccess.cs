using Kuroe.Flow.Shared.StaticGraph.PortModel;

namespace Kuroe.Flow.Shared.Execution;

/// <summary>执行输入端口与输出端口声明的只读视图。工具按它读取运行期环境，不接触执行输入的具体形状。</summary>
public interface IPortAccess
{
    /// <summary>本次执行的输出端口声明。</summary>
    IReadOnlyList<Port> Outputs { get; }

    /// <summary>某个输入端口的文本，多段按绑定顺序以空行连接，没有内容时为空。</summary>
    string? Text(PortName port);
}