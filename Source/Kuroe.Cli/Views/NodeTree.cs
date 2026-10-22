using Kuroe.Flow.Shared.StaticGraph.Graph;
using Spectre.Console;
using Spectre.Console.Rendering;

namespace Kuroe.Cli.Views;

/// <summary>流程节点树的构建：根级节点与容器成员按图递归，节点内容由调用方给出。</summary>
internal static class NodeTree
{
    /// <summary>建树。根节点之外的分支来自图，容器子节点先加子行再加成员。</summary>
    public static Tree Build(
        INodeGraph graph,
        IRenderable root,
        Func<GraphNode, IRenderable> line,
        Func<GraphNode, IEnumerable<IRenderable>>? details = null)
    {
        Tree tree = new(root) { Guide = TreeGuide.Line, Style = Styles.Border };
        foreach (GraphNode node in graph.ChildrenOf(null))
        {
            Add(tree, node);
        }

        return tree;

        void Add(IHasTreeNodes parent, GraphNode node)
        {
            TreeNode branch = parent.AddNode(line(node));
            foreach (IRenderable detail in details?.Invoke(node) ?? [])
            {
                branch.AddNode(detail);
            }

            if (node is ContainerNode container)
            {
                foreach (GraphNode child in container.Children)
                {
                    Add(branch, child);
                }
            }
        }
    }
}

/// <summary>一段可样式化的单行文本。片段之间自动插入分隔符，空片段不参与。</summary>
internal sealed class ViewLine
{
    private readonly List<(string Text, Style Style)> _parts = [];

    public ViewLine Add(string text, Style style)
    {
        if (text.Length > 0)
        {
            _parts.Add((text, style));
        }

        return this;
    }

    public Paragraph Build()
    {
        Paragraph paragraph = new();
        for (int i = 0; i < _parts.Count; i++)
        {
            if (i > 0)
            {
                paragraph.Append(" · ", Styles.Hint);
            }

            paragraph.Append(_parts[i].Text, _parts[i].Style);
        }

        return paragraph;
    }
}