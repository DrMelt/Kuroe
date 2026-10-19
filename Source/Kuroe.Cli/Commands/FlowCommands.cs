using ErrorOr;
using Kuroe.Cli.Views;
using Kuroe.Flow.Shared.StaticGraph;
using Kuroe.Node.Llm;
using Kuroe.Workflows.Flows;
using Kuroe.Presentation;
using Spectre.Console;

namespace Kuroe.Cli.Commands;

/// <summary>/flow 子命令的解析与执行。流程模板的查看与导入。</summary>
internal sealed class FlowCommands(
    FlowService flows,
    Terminal terminal,
    ResultView results)
{
    /// <summary>该命令族的帮助行。</summary>
    public static IReadOnlyList<(string Command, string Description)> Help { get; } =
    [
        ("/flow list", "列出流程与节点库"),
        ("/flow show <流程>", "打印流程的节点"),
        ("/flow add <文件>", "导入流程与节点库，同名覆盖"),
    ];

    public void Run(string[] parts)
    {
        const string usage = "用法：/flow list | show <流程> | add <文件>";
        string subcommand = parts.Length > 1 ? parts[1].ToLowerInvariant() : string.Empty;

        switch ((subcommand, parts.Length))
        {
            case ("list", 2):
                List();
                break;

            case ("show", 3):
                Show(parts[2]);
                break;

            case ("add", 3):
                Add(parts[2]);
                break;

            default:
                terminal.Hint(usage);
                break;
        }
    }

    private void List()
    {
        Grid grid = Terminal.Columns(3, wrapColumns: 2);
        grid.AddRow(
            new Text("流程", Styles.Hint),
            new Text("节点", Styles.Hint),
            new Text("说明", Styles.Hint));

        foreach (FlowDefinition flow in flows.All())
        {
            grid.AddRow(
                new Text(flow.Name.Value, Styles.Key),
                new Text(string.Join(" → ", FlowCommands.Flatten(flow.RootNode).Select(node => node.Label.Value))),
                new Text(flow.Description ?? string.Empty, Styles.Hint));
        }

        terminal.NewLine();
        terminal.Write(grid);
    }

    private void Show(string name)
    {
        ErrorOr<FlowName> flowName = FlowName.Create(name);
        if (flowName.IsError)
        {
            results.Reject(flowName.ErrorsOrEmptyList);
            return;
        }

        ErrorOr<FlowDefinition> found = flows.Find(flowName.Value);
        if (found.IsError)
        {
            results.Reject(found.ErrorsOrEmptyList);
            return;
        }

        Grid grid = Terminal.Columns(5, wrapColumns: 5);
        grid.AddRow(
            new Text("序号", Styles.Hint),
            new Text("执行节点", Styles.Hint),
            new Text("模型", Styles.Hint),
            new Text("类型", Styles.Hint),
            new Text("上游与要求", Styles.Hint));

        int index = 0;
        foreach ((NodeSpec node, int depth) in FlowCommands.Walk(found.Value.RootNode))
        {
            if (node.Spec is not null)
            {
                grid.AddRow(
                    new Text($"{index + 1}", Styles.Key),
                    new Text(new string(' ', depth * 2) + node.Label.Value),
                    new Text(node.Model?.Value ?? "—"),
                    new Text(node.EffectiveKind.Value),
                    new Text(Requirement(node), Styles.Hint));
                index++;
            }
            else if (node.Nodes is { Count: > 0 })
            {
                grid.AddRow(
                    new Text(string.Empty),
                    new Text(new string(' ', depth * 2) + node.Label),
                    new Text("容器", Styles.Hint),
                    new Text(string.Empty),
                    new Text(string.Empty));
            }
        }

        terminal.NewLine();
        terminal.Write(grid);
        terminal.Hint($"开始 {string.Join("、", found.Value.StartOutputs.Select(StartPort))}");
    }

    private void Add(string file)
    {
        ErrorOr<FlowImport> imported = flows.Import(file);
        if (imported.IsError)
        {
            results.Reject(imported.ErrorsOrEmptyList);
            return;
        }

        terminal.Ok($"已从 {imported.Value.Source} 导入 {imported.Value.Names.Count} 条流程。");
        foreach (string note in imported.Value.Notes)
        {
            terminal.Hint($"  {note}");
        }
    }

    /// <summary>开始端口的展示：带固定值的端口写出值。</summary>
    private static string StartPort(Port port) =>
        port.Value is { Length: > 0 } value ? $"{port.Name}＝{value}" : port.Name.Value;

    private static string Requirement(NodeSpec node)
    {
        LlmParameters? parameters = NodeParameterView.Llm(node.Type);
        List<string> parts = [];
        if (parameters?.Tools is { Count: > 0 } tools)
        {
            parts.Add($"工具 {string.Join("、", tools)}");
        }

        if (node.Inputs.Count > 0)
        {
            parts.Add($"输入 {string.Join("、", node.Inputs.Select(port => port.Label()))}");
        }

        if (node.From.Count > 0)
        {
            parts.Add($"取自 {string.Join("；", node.From.Select(group => group.Label()))}");
        }

        if (NodeParameterView.Question(node.Type) is { Length: > 0 } question)
        {
            parts.Add($"提问 {question}");
        }

        if (node.Outputs.Count > 0)
        {
            parts.Add($"端口 {string.Join("、", node.Outputs.Select(port => port.Label()))}");
        }

        if (parameters?.SystemPrompt is { Count: > 0 } blocks)
        {
            parts.Add($"系统指令 {blocks.Count} 块");
        }

        return parts.Count == 0 ? "—" : string.Join("；", parts);
    }

    /// <summary>递归收集流程里的全部执行节点。</summary>
    private static IEnumerable<NodeSpec> Flatten(NodeSpec node)
    {
        if (node.Nodes is { Count: > 0 } children)
        {
            foreach (NodeSpec child in children)
            {
                foreach (NodeSpec descendant in Flatten(child))
                {
                    yield return descendant;
                }
            }
        }
        else
        {
            yield return node;
        }
    }

    /// <summary>先根序遍历节点树，附带层级深度供缩进。</summary>
    private static IEnumerable<(NodeSpec Node, int Depth)> Walk(NodeSpec node, int depth = 0)
    {
        yield return (node, depth);
        if (node.Nodes is { Count: > 0 } children)
        {
            foreach (NodeSpec child in children)
            {
                foreach ((NodeSpec descendant, int childDepth) in Walk(child, depth + 1))
                {
                    yield return (descendant, childDepth);
                }
            }
        }
    }
}
