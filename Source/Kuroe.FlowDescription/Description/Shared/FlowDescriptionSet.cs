namespace Kuroe.FlowDescription.Description.Shared;

/// <summary>编译输入的流程描述：节点库定义与流程的组合，保留文件里写下的形态，不构造模型。
/// 节点库与流程在存储时各自落在独立文件，读取后合并为一份流程描述。输出端口在所有节点上使用同一形状，节点类别只决定来源作用域。</summary>
public sealed record FlowDescriptionSet(
    IReadOnlyList<NodeDefinitionDescription> Nodes,
    IReadOnlyList<FlowEntry> Flows);