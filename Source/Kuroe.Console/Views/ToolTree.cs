using Kuroe.Tool;
using Kuroe.Tool.Json;
using Spectre.Console;

namespace Kuroe.Console.Views;

/// <summary>全部工具的路径树：路径按 / 分段成层，同一前缀合为一个分支，叶行给出函数名、说明、模板与参数。</summary>
internal static class ToolTree
{
    /// <summary>建树。分段按定义的出现顺序加入，同一前缀只建一个分支，路径同时是函数与分组时复用该节点。</summary>
    public static Tree Build(IReadOnlyList<ToolDefinition> definitions, IReadOnlyList<ConfiguredTool> configuredTools)
    {
        Dictionary<ToolPath, string> configurations = [];
        foreach (ConfiguredTool tool in configuredTools)
        {
            configurations[tool.Definition.Path] = tool.Configuration;
        }

        Tree tree = new(new ViewLine().Add("工具", Styles.Hint).Build()) { Guide = TreeGuide.Line, Style = Styles.Border };
        Dictionary<string, TreeNode> nodes = new(StringComparer.Ordinal);

        foreach (ToolDefinition definition in definitions)
        {
            string[] segments = definition.Path.Value.Split('/');
            IHasTreeNodes parent = tree;
            string prefix = string.Empty;

            for (int i = 0; i < segments.Length - 1; i++)
            {
                prefix = prefix.Length == 0 ? segments[i] : $"{prefix}/{segments[i]}";
                if (!nodes.TryGetValue(prefix, out TreeNode? branch))
                {
                    branch = parent.AddNode(new Text(segments[i], Styles.Hint));
                    nodes[prefix] = branch;
                }

                parent = branch;
            }

            string path = definition.Path.Value;
            configurations.TryGetValue(definition.Path, out string? configuration);
            Paragraph line = Line(definition, configuration);
            if (nodes.TryGetValue(path, out TreeNode? existing))
            {
                existing.AddNode(line);
            }
            else
            {
                nodes[path] = parent.AddNode(line);
            }
        }

        return tree;
    }

    /// <summary>叶行：函数名、说明、模板与参数，空片段不参与。</summary>
    private static Paragraph Line(ToolDefinition definition, string? configuration) =>
        new ViewLine()
            .Add(definition.Path.Name.Value, Styles.Key)
            .Add(definition.Description, Style.Plain)
            .Add(configuration is null ? string.Empty : $"模板 {configuration}", Styles.Hint)
            .Add($"参数 {definition.Parameters.Labels()}", Styles.Hint)
            .Build();
}