using Kuroe.Flow.Shared.Model;

namespace Kuroe.Flow.Shared.StaticGraph.Nodes;

/// <summary>一个节点类型：类型名与静态形状，由实现方建出实例挂在节点上。
/// 引擎按形状解析声明与推进执行，类型专属声明封在实例里，引擎只透传不解释。
/// 需要自行建立运行期对象的类型另实现运行期工厂接口，需要校验规则的类型另实现编译期校验契约。</summary>
public interface INodeType
{
    /// <summary>类型名，与文件里 Kind 的取值对应。</summary>
    NodeKind Kind { get; }

    /// <summary>静态形状。</summary>
    NodeTypeShape Shape { get; }

    /// <summary>类型用于取模型名的静态端口，没有时为 null。</summary>
    PortName? ModelPort => null;

    /// <summary>类型提供给信息查询的属性，按展示顺序。</summary>
    IReadOnlyList<NodeAttribute> Attributes => [];
}
