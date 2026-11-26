using Kuroe.FlowDescription.Definition.Shared.Model;
using Kuroe.Kernel.Tasks.Shared;

namespace Kuroe.FlowDescription.Semantics.Shared;

/// <summary>端口引用的直接来源：开始端口或节点输出端口。取值终点由 <see cref="FlowSource"/> 表达。</summary>
public readonly record struct FlowBinding
{
    private FlowBinding(NodePath? node, PortName port)
    {
        Node = node;
        Port = port;
    }

    /// <summary>来源节点，开始端口为空。</summary>
    public NodePath? Node { get; }

    /// <summary>来源端口。</summary>
    public PortName Port { get; }

    /// <summary>是否为流程开始端口。</summary>
    public bool IsStart => Node is null;

    /// <summary>流程开始端口来源。</summary>
    public static FlowBinding Start(PortName port) => new(null, port);

    /// <summary>节点输出端口来源。</summary>
    public static FlowBinding ForNode(NodePath node, PortName port) => new(node, port);

    /// <summary>来源显示：节点写节点@端口，开始端口写 @端口。</summary>
    public string Display => Node is { } node ? $"{node}@{Port}" : $"@{Port}";
}