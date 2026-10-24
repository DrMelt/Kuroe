using ErrorOr;
using Kuroe.Flow.Compiler.Shared;
using Kuroe.Flow.Shared.StaticGraph.Flow;
using Kuroe.Flow.Shared.StaticGraph.Graph;
using Kuroe.Flow.Shared.StaticGraph.Node;
using Kuroe.Presentation;
using Kuroe.Workflows.Flows;
using Spectre.Console;

namespace Kuroe.Console.Views;

/// <summary>流程菜单：列表、编译结果查看与文件导入。</summary>
internal sealed class FlowBrowser(
    FlowService flows,
    Terminal terminal,
    ResultView results)
{
    /// <summary>提问一行能看到的文本长度。</summary>
    private const int QuestionLimit = 40;

    /// <summary>一个菜单项。</summary>
    private sealed record Item(string Label, string Action);

    private static readonly Item Back = new("← 返回", "back");
    private static readonly Item Refresh = new("~ 刷新", "refresh");

    public void Browse()
    {
        using (terminal.Exclusive())
        {
            while (true)
            {
                IReadOnlyList<FlowDefinition> all = flows.All();

                terminal.Page("主菜单", "流程");
                Print(all);
                terminal.NewLine();

                List<Item> items = [];
                if (all.Count > 0)
                {
                    items.Add(new Item("≡ 查看流程详情", "show"));
                }

                items.Add(new Item("+ 导入流程", "import"));
                items.Add(Refresh);
                items.Add(Back);

                Item picked = terminal.Choose("流程操作", items, item => item.Label, Back);
                if (picked.Action == "back")
                {
                    return;
                }

                switch (picked.Action)
                {
                    case "show":
                        Show(all);
                        break;

                    case "import":
                        Import();
                        break;
                }
            }
        }
    }

    /// <summary>打印流程、端口与接线。</summary>
    public void PrintDetail(FlowName name)
    {
        ErrorOr<CompiledFlow> found = flows.FindCompiled(name);
        if (found.IsError)
        {
            results.Reject(found.ErrorsOrEmptyList);
            return;
        }

        CompiledFlow compiled = found.Value;
        terminal.Page("主菜单", "流程", compiled.Model.Name.Value);
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

    /// <summary>从文件导入流程与节点库，同名覆盖。</summary>
    public void Import(string file)
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

    private void Show(IReadOnlyList<FlowDefinition> all)
    {
        if (Choices.Pick(terminal, "选择流程", [.. all.Select(flow => flow.Name.Value)]) is { } index)
        {
            ViewPage.Browse(terminal, "流程详情", () => PrintDetail(all[index].Name));
        }
    }

    private void Import()
    {
        string? file = terminal.Ask("流程文件：");
        if (file is not null)
        {
            Import(file);
        }
    }

    private void Print(IReadOnlyList<FlowDefinition> all)
    {
        if (all.Count == 0)
        {
            terminal.Hint("流程库为空，选择「导入流程」导入流程文件。");
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