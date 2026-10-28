using Kuroe.Flow.Shared.StaticGraph.PortModel;

namespace Kuroe.Flow.Shared.Execution;

/// <summary>执行输入端口与输出端口声明的只读视图。工具按它读取运行期环境，不接触执行输入的具体形状。</summary>
public interface IPortAccess : IPortTexts
{
    /// <summary>本次执行的输出端口声明。</summary>
    IReadOnlyList<Port> Outputs { get; }
}