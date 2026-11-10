using Kuroe.Flow.Shared.Model;

namespace Kuroe.Flow.Shared.StaticGraph.Graph;

/// <summary>绑定的来源：节点端口或流程开始端口。节点来源可以没有端口，表示按节点整体推进。</summary>
public readonly record struct BindingSource
{
    private BindingSource(NodePath? node, PortName? port)
    {
        Node = node;
        Port = port;
    }

    /// <summary>来源节点，开始端口为空。</summary>
    public NodePath? Node { get; }

    /// <summary>来源端口。开始来源必须有端口，节点来源可以没有端口。</summary>
    public PortName? Port { get; }

    /// <summary>是否为流程开始端口。开始来源必须有端口，没有节点也没有端口的默认值不是开始来源。</summary>
    public bool IsStart => Node is null && Port is not null;

    /// <summary>流程开始端口来源。</summary>
    public static BindingSource ForStart(PortName port) => new(null, port);

    /// <summary>节点来源，可以只有节点没有端口。</summary>
    public static BindingSource ForNode(NodePath node, PortName? port = null) => new(node, port);

    /// <summary>来源显示：节点写节点@端口，开始端口写 @端口。</summary>
    public string Display
    {
        get
        {
            if (Node is not { } node)
            {
                return $"@{Port?.Value}";
            }

            return Port is { } port ? $"{node}@{port}" : node.ToString();
        }
    }
}