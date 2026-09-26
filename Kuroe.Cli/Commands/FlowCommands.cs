using ErrorOr;
using Kuroe.Agent;
using Kuroe.Cli.Views;
using Kuroe.Configuration;
using Kuroe.Shared.Agent;
using Kuroe.Shared.Workflows.Flows;
using Kuroe.Workflows.Flows;
using Spectre.Console;

namespace Kuroe.Cli.Commands;

/// <summary>/flow 子命令的解析与执行。流程模板的查看、导入与默认流程选择。</summary>
internal sealed class FlowCommands(
    WorkflowService flows,
    SettingsProvider settings,
    Terminal terminal,
    ResultPrinter results)
{
    /// <summary>该命令族的帮助行。</summary>
    public static IReadOnlyList<(string Command, string Description)> Help { get; } =
    [
        ("/flow list", "列出流程模板"),
        ("/flow show <流程>", "打印流程的节点"),
        ("/flow add <文件>", "导入流程，同名覆盖"),
        ("/flow default <流程>", "设为提交任务的默认流程"),
    ];

    /// <summary>默认流程名在用户层中的路径。</summary>
    private const string DefaultFlowPath = "Agent:DefaultFlow";

    public void Run(string[] parts)
    {
        const string usage = "用法：/flow list | show <流程> | add <文件> | default <流程>";
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

            case ("default", 3):
                SetDefault(parts[2]);
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

        foreach (Workflow flow in flows.All())
        {
            bool standard = flow.Name == flows.DefaultName;
            grid.AddRow(
                new Text(standard ? $">{flow.Name}" : flow.Name, standard ? Styles.Success : Styles.Key),
                new Text(string.Join(" → ", FlowCommands.Flatten(flow.Nodes).Select(node => node.Name))),
                new Text(flow.Description ?? string.Empty, Styles.Hint));
        }

        terminal.NewLine();
        terminal.Write(grid);
        terminal.Hint($"默认流程 {flows.DefaultName}，用 /flow default <流程> 改。");
    }

    private void Show(string name)
    {
        ErrorOr<Workflow> found = flows.Find(name);
        if (found.IsError)
        {
            results.Reject(found.ErrorsOrEmptyList);
            return;
        }

        Grid grid = Terminal.Columns(7, wrapColumns: 6);
        grid.AddRow(
            new Text("序号", Styles.Hint),
            new Text("叶子", Styles.Hint),
            new Text("执行", Styles.Hint),
            new Text("契约", Styles.Hint),
            new Text("展开", Styles.Hint),
            new Text("产出后", Styles.Hint),
            new Text("上游与要求", Styles.Hint));

        int index = 0;
        foreach ((NodeSpec node, int depth) in FlowCommands.Walk(found.Value.Nodes))
        {
            if (node is AgentNode leaf)
            {
                grid.AddRow(
                    new Text($"{index + 1}", Styles.Key),
                    new Text(new string(' ', depth * 2) + leaf.Name),
                    new Text(leaf.Agent),
                    new Text(leaf.Output.Label()),
                    new Text(Labels.Of(leaf.Mode)),
                    new Text(Labels.Of(leaf.Gate)),
                    new Text(Requirement(leaf), Styles.Hint));
                index++;
            }
            else if (node is FlowNode flow)
            {
                grid.AddRow(
                    new Text(string.Empty),
                    new Text(new string(' ', depth * 2) + flow.Name),
                    new Text("容器", Styles.Hint),
                    new Text(string.Empty),
                    new Text(string.Empty),
                    new Text(string.Empty),
                    new Text(string.Empty));
            }
        }

        terminal.NewLine();
        terminal.Write(grid);
    }

    private void Add(string file)
    {
        ErrorOr<WorkflowImport> imported = flows.Import(file);
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

    private void SetDefault(string name)
    {
        ErrorOr<Workflow> found = flows.Find(name);
        if (found.IsError)
        {
            results.Reject(found.ErrorsOrEmptyList);
            return;
        }

        results.Apply(settings.SetText(DefaultFlowPath, name));
    }

    private static string Requirement(AgentNode leaf)
    {
        List<string> parts = [];
        if (leaf.From.Count > 0)
        {
            parts.Add($"取自 {string.Join("、", leaf.From)}");
        }

        if (leaf.Output == NodeOutput.Review)
        {
            RejectAction action = leaf.OnReject ?? RejectAction.Retry;
            int limit = Math.Max(1, leaf.MaxAttempts ?? 2);
            parts.Add($"不通过则 {Labels.Of(action)}，至多 {limit} 轮");
        }

        if (leaf.Split is { } split)
        {
            if (split.Items is { Count: > 0 } items)
            {
                parts.Add($"固定 {items.Count} 条");
            }

            if (split.ExtrasMax is { } limit)
            {
                parts.Add($"补充至多 {limit} 条");
            }

            if (split.Acceptance is { Length: > 0 })
            {
                parts.Add("统一验收");
            }
        }

        return parts.Count == 0 ? "—" : string.Join("；", parts);
    }

    /// <summary>递归收集流程里的全部叶子。</summary>
    private static IEnumerable<NodeSpec> Flatten(IReadOnlyList<NodeSpec> nodes)
    {
        foreach (NodeSpec node in nodes)
        {
            if (node is FlowNode flow)
            {
                foreach (NodeSpec child in Flatten(flow.Nodes))
                {
                    yield return child;
                }
            }
            else
            {
                yield return node;
            }
        }
    }

    /// <summary>先根序遍历节点树，附带层级深度供缩进。</summary>
    private static IEnumerable<(NodeSpec Node, int Depth)> Walk(IReadOnlyList<NodeSpec> nodes, int depth = 0)
    {
        foreach (NodeSpec node in nodes)
        {
            yield return (node, depth);
            if (node is FlowNode flow)
            {
                foreach ((NodeSpec child, int childDepth) in Walk(flow.Nodes, depth + 1))
                {
                    yield return (child, childDepth);
                }
            }
        }
    }
}
