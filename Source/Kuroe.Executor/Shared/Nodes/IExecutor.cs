using Kuroe.Tasks.Shared;

namespace Kuroe.Executor.Shared.Nodes;

/// <summary>一个执行器条目的实例：种类名、静态形状与类型专属声明。容器条目只组织子节点，不实现执行能力。
/// 可执行条目另行实现执行运行器工厂与执行参数提供者，装配按所需能力取用。</summary>
public interface IExecutor
{
    /// <summary>种类名，与文件里 Kind 的取值对应。</summary>
    ExecutorKind Kind { get; }

    /// <summary>静态形状。</summary>
    ExecutorShape Shape { get; }

    /// <summary>条目用于取模型名的静态端口，没有时为 null。</summary>
    PortName? ModelPort => null;

    /// <summary>条目提供给信息查询的属性，按展示顺序。</summary>
    IReadOnlyList<ExecutorAttribute> Attributes => [];
}