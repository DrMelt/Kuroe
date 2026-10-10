using ErrorOr;
using Kuroe.Executions;
using Kuroe.Cli.Views;
using Kuroe.Configuration;
using Kuroe.Shared.Executions;
using Kuroe.Shared.Workflows.Flows;
using Kuroe.Workflows.FlowAssembly;
using Spectre.Console;

namespace Kuroe.Cli.Commands;

/// <summary>/flow 子命令的解析与执行。流程模板的查看、导入与默认流程选择。</summary>
internal sealed class FlowCommands(
    FlowService flows,
    SettingsProvider settings,
    Terminal terminal,
    ResultView results)
{
    /// <summary>该命令族的帮助行。</summary>
    public static IReadOnlyList<(string Command, string Description)> Help { get; } =
    [
        ("/flow list", "列出流程与节点库"),
        ("/flow show <流程>", "打印流程的节点"),
        ("/flow add <文件>", "导入流程与节点库，同名覆盖"),
        ("/flow default <流程>", "设为提交任务的默认流程"),
    ];

    /// <summary>默认流程名在用户层中的路径。</summary>
    private const string DefaultFlowPath = "Runtime:DefaultFlow";

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

        foreach (FlowDefinition flow in flows.All())
        {
            bool standard = flow.Name == flows.DefaultName;
            grid.AddRow(
                new Text(standard ? $">{flow.Name}" : flow.Name.Value, standard ? Styles.Success : Styles.Key),
                new Text(string.Join(" → ", FlowCommands.Flatten(flow.RootNode).Select(node => node.Name.Value))),
                new Text(flow.Description ?? string.Empty, Styles.Hint));
        }

        terminal.NewLine();
        terminal.Write(grid);
        terminal.Hint($"默认流程 {flows.DefaultName}，用 /flow default <流程> 改。");
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

        Grid grid = Terminal.Columns(7, wrapColumns: 6);
        grid.AddRow(
            new Text("序号", Styles.Hint),
            new Text("执行节点", Styles.Hint),
            new Text("模型", Styles.Hint),
            new Text("契约", Styles.Hint),
            new Text("展开", Styles.Hint),
            new Text("产出后", Styles.Hint),
            new Text("上游与要求", Styles.Hint));

        int index = 0;
        foreach ((NodeSpec node, int depth) in FlowCommands.Walk(found.Value.RootNode))
        {
            if (node.Execution is { } executable)
            {
                grid.AddRow(
                    new Text($"{index + 1}", Styles.Key),
                    new Text(new string(' ', depth * 2) + node.Name.Value),
                    new Text(node.Model?.Value ?? "—"),
                    new Text(executable.Output.Label()),
                    new Text(ViewLabels.Of(executable.Mode)),
                    new Text(ViewLabels.Of(node.Gate)),
                    new Text(Requirement(node, executable), Styles.Hint));
                index++;
            }
            else if (node.Nodes is { Count: > 0 })
            {
                grid.AddRow(
                    new Text(string.Empty),
                    new Text(new string(' ', depth * 2) + node.Name),
                    new Text("容器", Styles.Hint),
                    new Text(string.Empty),
                    new Text(string.Empty),
                    new Text(ViewLabels.Of(node.Gate), Styles.Hint),
                    new Text(string.Empty));
            }
        }

        terminal.NewLine();
        terminal.Write(grid);
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

    private void SetDefault(string name)
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

        results.Apply(settings.SetText(DefaultFlowPath, flowName.Value.Value));
    }

    private static string Requirement(NodeSpec node, ExecutableSpec executable)
    {
        List<string> parts = [];
        if (executable.Branch is { } branch)
        {
            parts.Add($"分支 {branch}");
        }

        if (executable.Tools.Count > 0)
        {
            parts.Add($"工具 {string.Join("、", executable.Tools)}");
        }

        if (node.From.Count > 0)
        {
            parts.Add($"取自 {string.Join("、", node.From)}");
        }

        if (executable.Question is { Length: > 0 })
        {
            parts.Add($"提问 {executable.Question}");
        }

        if (node.Outputs.Count > 0)
        {
            parts.Add($"端口 {string.Join("、", node.Outputs.Select(port => port.Value))}");
        }

        if (node.SystemPrompt.Count > 0)
        {
            parts.Add($"系统指令 {node.SystemPrompt.Count} 块");
        }

        if (executable.Split is { } split)
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
