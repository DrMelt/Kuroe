using ErrorOr;
using Kuroe.Cli.Views;
using Kuroe.Flow.Compiler.Shared;
using Kuroe.Flow.Shared.StaticGraph.Flow;
using Kuroe.Flow.Shared.StaticGraph.Graph;
using Kuroe.Flow.Shared.StaticGraph.Node;
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
    /// <summary>提问一行能看到的文本长度。</summary>
    private const int QuestionLimit = 40;

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
        IReadOnlyList<FlowDefinition> all = flows.All();
        if (all.Count == 0)
        {
            terminal.Hint("流程库为空，用 /flow add <文件> 导入流程。");
            return;
        }

        Table table = Blocks.Table(
            ("流程", false),
            ("执行节点", false),
            ("容器", false),
            ("模型选择", false),
            ("说明", true));

        foreach (FlowDefinition flow in all)
        {
            (int executables, int containers) = Count(flow.RootNode);
            table.AddRow(
                new Text(flow.Name.Value, Styles.Key),
                new Text(executables.ToString()),
                new Text(containers.ToString()),
                new Text(flow.Models.Count.ToString()),
                new Text(flow.Description ?? string.Empty, Styles.Hint));
        }

        terminal.Write(Blocks.Section(BoxKind.View, $"流程 {all.Count} 条", table));
    }

    private void Show(string name)
    {
        ErrorOr<FlowName> flowName = FlowName.Create(name);
        if (flowName.IsError)
        {
            results.Reject(flowName.ErrorsOrEmptyList);
            return;
        }

        ErrorOr<CompiledFlow> found = flows.FindCompiled(flowName.Value);
        if (found.IsError)
        {
            results.Reject(found.ErrorsOrEmptyList);
            return;
        }

        CompiledFlow compiled = found.Value;
        terminal.Write(Blocks.Section(
            BoxKind.View,
            $"流程 {compiled.Model.Name.Value}",
            Blocks.Fields(
                ("执行节点", new Text(compiled.Graph.TotalExecutables.ToString())),
                ("容器", new Text(compiled.Graph.Containers.Count.ToString())),
                ("模型选择", new Text(compiled.Model.Models.Count.ToString())),
                ("说明", new Text(
                    compiled.Model.Description is { Length: > 0 } description ? description : "—",
                    Styles.Hint)))));

        terminal.NewLine();
        terminal.Write(NodeTree.Build(compiled.Graph, new ViewLine().Add("节点", Styles.Hint).Build(), Line));
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

    private static Paragraph Line(GraphNode node) => node switch
    {
        StartNode start => StartLine(start),
        ContainerNode container => ContainerLine(container),
        ExecutableNode executable => ExecutableLine(executable),
        _ => new ViewLine().Add(node.Label.Value, Styles.Key).Build(),
    };

    private static Paragraph StartLine(StartNode start)
    {
        ViewLine line = new ViewLine().Add("Start", Styles.Key).Add("开始", Styles.Hint);
        if (start.Outputs.Count > 0)
        {
            line.Add($"端口 {string.Join('、', start.Outputs.Select(port => port.StartLabel()))}", Styles.Hint);
        }

        return line.Build();
    }

    private static Paragraph ContainerLine(ContainerNode container)
    {
        ViewLine line = new ViewLine().Add(container.Label.Value, Styles.Key).Add("容器", Styles.Hint);
        if (container.PortBindings is { Count: > 0 } bindings)
        {
            line.Add($"端口 {string.Join('、', bindings.Keys.Select(port => port.Value))}", Styles.Hint);
        }

        return line.Build();
    }

    private static Paragraph ExecutableLine(ExecutableNode executable)
    {
        ViewLine line = new ViewLine().Add(executable.Label.Value, Styles.Key).Add(executable.Kind.Value, Styles.Hint);
        if (executable.Model is { } model)
        {
            line.Add($"模型 {ViewLabels.ModelLabel(model)}", Styles.Hint);
        }

        if (executable.Inputs.Count > 0)
        {
            line.Add($"输入 {string.Join('、', executable.Inputs.Select(port => port.Label()))}", Styles.Hint);
        }

        if (executable.Outputs.Count > 0)
        {
            line.Add($"输出 {string.Join('、', executable.Outputs.Select(port => port.Label()))}", Styles.Hint);
        }

        if (NodeParameterView.Llm(executable.Type) is { } parameters)
        {
            if (parameters.Tools is { Count: > 0 } tools)
            {
                line.Add($"工具 {string.Join('、', tools.Select(tool => tool.Value))}", Styles.Hint);
            }

            if (parameters.SystemPrompt is { Count: > 0 } blocks)
            {
                line.Add($"系统指令 {blocks.Count} 块", Styles.Hint);
            }
        }

        if (NodeParameterView.Question(executable.Type) is { Length: > 0 } question)
        {
            line.Add($"提问 {ViewLabels.OneLine(question, QuestionLimit)}", Styles.Hint);
        }

        if (executable.MaxExecutions != ExecutableNode.DefaultMaxExecutions)
        {
            line.Add($"上限 {executable.MaxExecutions}", Styles.Hint);
        }

        string upstream = Upstream(executable.Incoming);
        if (upstream.Length > 0)
        {
            line.Add($"上游 {upstream}", Styles.Hint);
        }

        return line.Build();
    }

    /// <summary>入边按启动组展开，组内以顿号连接，组间表示任一齐备。</summary>
    private static string Upstream(IReadOnlyList<FlowEdge> edges) =>
        string.Join(" 或 ", edges
            .GroupBy(edge => edge.Group)
            .OrderBy(group => group.Key)
            .Select(group => string.Join('、', group.Select(edge =>
                $"{edge.TargetInput}←{edge.Source}@{edge.SourcePort}"))));

    /// <summary>流程里的执行节点与容器数量。</summary>
    private static (int Executables, int Containers) Count(NodeSpec root)
    {
        int executables = 0;
        int containers = 0;
        foreach (NodeSpec node in Walk(root))
        {
            if (node.IsContainer)
            {
                containers++;
            }
            else if (node.Spec is not null)
            {
                executables++;
            }
        }

        return (executables, containers);
    }

    /// <summary>先根序遍历节点树。</summary>
    private static IEnumerable<NodeSpec> Walk(NodeSpec node)
    {
        yield return node;
        if (node.Nodes is { Count: > 0 } children)
        {
            foreach (NodeSpec child in children)
            {
                foreach (NodeSpec descendant in Walk(child))
                {
                    yield return descendant;
                }
            }
        }
    }
}
