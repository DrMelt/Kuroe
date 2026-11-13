using Kuroe.Flow.Presentation.Shared;
using Spectre.Console;
using Spectre.Console.Rendering;

namespace Kuroe.Console.Views;

/// <summary>流程模板节点树的构建：分组与步骤按父路径递归，节点内容由调用方给出。</summary>
internal static class NodeTree
{
    /// <summary>建树。根节点之外的分支来自模板分组，步骤按父路径归入所属分支。</summary>
    public static Tree Build(
        FlowTemplateView flow,
        IRenderable root,
        Func<FlowGroupView, IRenderable> groupLine,
        Func<FlowStepView, IRenderable> stepLine,
        Func<FlowGroupView, IEnumerable<IRenderable>>? details = null)
    {
        Tree tree = new(root) { Guide = TreeGuide.Line, Style = Styles.Border };
        Add(tree, null);

        return tree;

        void Add(IHasTreeNodes parent, string? parentPath)
        {
            foreach (FlowGroupView group in flow.Groups.Where(group => group.ParentPath == parentPath))
            {
                TreeNode branch = parent.AddNode(groupLine(group));
                foreach (IRenderable detail in details?.Invoke(group) ?? [])
                {
                    branch.AddNode(detail);
                }

                Add(branch, group.Path);
            }

            foreach (FlowStepView step in flow.Steps.Where(step => step.ParentPath == parentPath))
            {
                parent.AddNode(stepLine(step));
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