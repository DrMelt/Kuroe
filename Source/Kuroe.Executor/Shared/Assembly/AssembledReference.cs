using Kuroe.Tasks.Shared;

namespace Kuroe.Executor.Shared.Assembly;

/// <summary>装配后的来源：流程开始端口或节点端口。节点来源可以没有端口，表示按节点整体推进。
/// 来源节点是已解析的路径，装配之后不再有标签引用。</summary>
public readonly record struct AssembledReference
{
    private AssembledReference(AssembledPath? node, PortName? port)
    {
        Node = node;
        Port = port;
    }

    /// <summary>来源节点，开始端口为空。</summary>
    public AssembledPath? Node { get; }

    /// <summary>来源端口。开始来源必须有端口，节点来源可以没有端口。</summary>
    public PortName? Port { get; }

    /// <summary>是否为流程开始端口。</summary>
    public bool IsStart => Node is null && Port is not null;

    /// <summary>流程开始端口来源。</summary>
    public static AssembledReference ForStart(PortName port) => new(null, port);

    /// <summary>节点来源，可以只有节点没有端口。</summary>
    public static AssembledReference ForNode(AssembledPath node, PortName? port = null) => new(node, port);

    /// <summary>来源显示：节点写 节点@端口，开始端口写 @端口。</summary>
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