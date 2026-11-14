using ErrorOr;
using Kuroe.Control.Shared;
using Kuroe.Flow.Presentation.Shared;
using Kuroe.Flow.Tasks.Shared;
using Kuroe.Workflows.Flows;
using Spectre.Console;

namespace Kuroe.Console.Views;

/// <summary>流程菜单：列表、模板查看与文件导入。</summary>
internal sealed class FlowBrowser(
    IFlowLibrary library,
    IFlowTemplateCatalog templates,
    Terminal terminal,
    ResultView results)
{
    private sealed record Item(string Label, string Action);

    private static readonly Item Back = new("← 返回", "back");
    private static readonly Item Refresh = new("~ 刷新", "refresh");

    public void Browse()
    {
        using (terminal.Exclusive())
        {
            while (true)
            {
                IReadOnlyList<FlowTemplateView> all = templates.All();

                terminal.Page("主菜单", "流程");
                Print(all);
                terminal.NewLine();

                List<Item> items = [Refresh];
                if (all.Count > 0)
                {
                    items.Add(new Item("≡ 查看流程详情", "show"));
                }

                items.Add(new Item("+ 导入流程", "import"));
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

    /// <summary>打印流程、开始端口与节点树。</summary>
    public void PrintDetail(FlowName name)
    {
        ErrorOr<FlowTemplateView> found = templates.Find(name);
        if (found.IsError)
        {
            results.Reject(found.ErrorsOrEmptyList);
            return;
        }

        FlowTemplateView flow = found.Value;
        terminal.Page("主菜单", "流程", flow.Name);
        terminal.Write(Blocks.Section(
            BoxKind.View,
            $"流程 {flow.Name}",
            Blocks.Fields(
                ("执行节点", new Text(flow.Steps.Count.ToString())),
                ("容器", new Text(flow.Groups.Count.ToString())),
                ("说明", new Text(
                    flow.Description is { Length: > 0 } description ? description : "—",
                    Styles.Hint)))));

        terminal.NewLine();
        ViewLine start = new ViewLine().Add("开始端口", Styles.Key);
        if (flow.StartPorts.Count > 0)
        {
            start.Add(string.Join('、', flow.StartPorts.Select(port => port.Label)), Styles.Hint);
        }

        terminal.Write(start.Build());
        terminal.Write(NodeTree.Build(
            flow,
            new ViewLine().Add("节点", Styles.Hint).Build(),
            group => GroupLine(group),
            step => StepLine(step)));
    }

    /// <summary>从文件导入节点库或流程，同名覆盖。</summary>
    public void Import(string file)
    {
        ErrorOr<FlowImport> imported = library.Import(file);
        if (imported.IsError)
        {
            results.Reject(imported.ErrorsOrEmptyList);
            return;
        }

        terminal.Ok($"已从 {imported.Value.Source} 导入 {Summary(imported.Value)}。");
        foreach (string note in imported.Value.Notes)
        {
            terminal.Hint($"  {note}");
        }
    }

    private static string Summary(FlowImport imported)
    {
        if (imported.Nodes.Count == 0)
        {
            return $"{imported.Flows.Count} 条流程";
        }

        return imported.Flows.Count == 0
            ? $"{imported.Nodes.Count} 个节点定义"
            : $"{imported.Flows.Count} 条流程、{imported.Nodes.Count} 个节点定义";
    }

    private void Show(IReadOnlyList<FlowTemplateView> all)
    {
        if (Choices.Pick(terminal, "选择流程", [.. all.Select(flow => flow.Name)]) is { } index)
        {
            ViewPage.Browse(terminal, "流程详情", () => PrintDetail(new FlowName(all[index].Name)));
        }
    }

    private void Import()
    {
        InputForm form = new(terminal, "导入流程", [new FormField("文件")]);
        if (form.Run() is { } values)
        {
            Import(values[0]);
        }
    }
    private void Print(IReadOnlyList<FlowTemplateView> all)
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
            ("说明", true));

        foreach (FlowTemplateView flow in all)
        {
            table.AddRow(
                new Text(flow.Name, Styles.Key),
                new Text(flow.Steps.Count.ToString()),
                new Text(flow.Groups.Count.ToString()),
                new Text(flow.Description ?? string.Empty, Styles.Hint));
        }

        terminal.Write(Blocks.Section(BoxKind.View, $"流程 {all.Count} 条", table));
    }

    private static Paragraph GroupLine(FlowGroupView group)
    {
        ViewLine line = new ViewLine().Add(group.Label, Styles.Key).Add(group.Kind ?? "容器", Styles.Hint);
        if (group.Outputs.Count > 0)
        {
            line.Add($"输出 {string.Join('、', group.Outputs.Select(port => port.Name))}", Styles.Hint);
        }

        if (group.MaxExecutions != int.MaxValue)
        {
            line.Add($"上限 {group.MaxExecutions}", Styles.Hint);
        }

        return line.Build();
    }

    private static Paragraph StepLine(FlowStepView step)
    {
        ViewLine line = new ViewLine().Add(step.Label, Styles.Key).Add(step.Kind, Styles.Hint);
        if (step.Model is { Length: > 0 } model)
        {
            line.Add($"模型 {model}", Styles.Hint);
        }

        IReadOnlyList<FlowPortView> inputs = [.. step.Inputs.Where(port => !port.Static)];
        if (inputs.Count > 0)
        {
            line.Add($"输入 {string.Join('、', inputs.Select(port => port.Name))}", Styles.Hint);
        }

        if (step.Outputs.Count > 0)
        {
            line.Add($"输出 {string.Join('、', step.Outputs.Select(port => port.Name))}", Styles.Hint);
        }

        if (step.MaxExecutions != FlowStepView.DefaultMaxExecutions)
        {
            line.Add($"上限 {step.MaxExecutions}", Styles.Hint);
        }

        if (step.Upstream.Count > 0)
        {
            line.Add($"上游 {string.Join(" 或 ", step.Upstream)}", Styles.Hint);
        }

        return line.Build();
    }
}