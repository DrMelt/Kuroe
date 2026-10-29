using Kuroe.Flow.Shared.Model;
using Kuroe.Flow.Shared.StaticGraph.Nodes;
using Kuroe.Flow.Shared.StaticGraph.Ports;

namespace Kuroe.Flow.NodeLibrary;

/// <summary>容器作用域规则：成员的上游来源只落在容器子树或容器声明的传入端口，容器外的节点与端口不可见。</summary>
internal static class ContainerScope
{
    /// <summary>检查容器的直接成员：命名来源必须是子树标签，@端口 必须是容器声明的传入端口。</summary>
    public static void Check(
        NodeSpec container,
        IReadOnlyList<NodeSpec> members,
        Func<NodeName, NodeSpec?> find,
        Action<NodeSpec, string> fail)
    {
        HashSet<NodeLabel> labels = SubtreeLabels(members, find);
        HashSet<PortName> ports = [.. container.EffectiveInputs.Select(port => port.Name)];
        foreach (NodeSpec member in members)
        {
            foreach ((PortRef reference, string field) in References(member))
            {
                bool inScope = reference.Source is { } source
                    ? labels.Contains(source)
                    : ports.Contains(reference.Port);
                if (inScope)
                {
                    continue;
                }

                fail(member, reference.Source is null
                    ? $"端口 @{reference.Port.Value} 没有在容器 {container.Label} 上声明。"
                    : $"{field} 引用的节点 {reference.Source.Value} 不在容器 {container.Label} 的作用域里。");
            }
        }
    }

    /// <summary>成员在所属容器作用域里求值的引用：From 的连接、容器成员的 In 绑定与 Static 绑定。</summary>
    private static IEnumerable<(PortRef Reference, string Field)> References(NodeSpec member)
    {
        foreach (FromGroup group in member.From)
        {
            foreach (FromLink link in group.Links)
            {
                yield return (link.Ref, "From");
            }
        }

        foreach (PortRef reference in member.In?.Values ?? [])
        {
            yield return (reference, "In");
        }

        foreach (PortRef reference in member.Static?.Values ?? [])
        {
            yield return (reference, "Static");
        }
    }

    /// <summary>成员整棵子树的标签，引用容器时展开被引用定义的成员。</summary>
    public static HashSet<NodeLabel> SubtreeLabels(IReadOnlyList<NodeSpec> members, Func<NodeName, NodeSpec?> find)
    {
        HashSet<NodeLabel> labels = [];
        Collect(members, find, labels);
        return labels;
    }

    private static void Collect(IReadOnlyList<NodeSpec> nodes, Func<NodeName, NodeSpec?> find, HashSet<NodeLabel> labels)
    {
        foreach (NodeSpec node in nodes)
        {
            labels.Add(node.Label);
            if (node.Nodes is { Count: > 0 } children)
            {
                Collect(children, find, labels);
            }
            else if (node.Use is { } use && find(use) is { } referenced && referenced.Nodes is { Count: > 0 } referencedMembers)
            {
                Collect(referencedMembers, find, labels);
            }
        }
    }
}