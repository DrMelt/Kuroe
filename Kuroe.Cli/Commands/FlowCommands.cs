using ErrorOr;
using Kuroe.Agent;
using Kuroe.Agent.Runs;
using Kuroe.Cli.Views;
using Kuroe.Configuration;
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
        ("/flow show <流程>", "打印流程的步骤"),
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
            new Text("步骤", Styles.Hint),
            new Text("说明", Styles.Hint));

        foreach (Workflow flow in flows.All())
        {
            bool standard = flow.Name == flows.DefaultName;
            grid.AddRow(
                new Text(standard ? $">{flow.Name}" : flow.Name, standard ? Styles.Success : Styles.Key),
                new Text(string.Join(" → ", flow.Steps.Select(step => step.Name))),
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

        Grid grid = Terminal.Columns(6, wrapColumns: 5);
        grid.AddRow(
            new Text("序", Styles.Hint),
            new Text("步骤", Styles.Hint),
            new Text("角色", Styles.Hint),
            new Text("展开", Styles.Hint),
            new Text("放行", Styles.Hint),
            new Text("上游与要求", Styles.Hint));

        foreach ((StepSpec step, int index) in found.Value.Steps.Select((step, index) => (step, index)))
        {
            grid.AddRow(
                new Text($"{index + 1}", Styles.Key),
                new Text(step.Name),
                new Text(step.Role.Label()),
                new Text(Labels.Of(step.Scope)),
                new Text(Labels.Of(step.Gate)),
                new Text(Requirement(step), Styles.Hint));
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

    private static string Requirement(StepSpec step)
    {
        List<string> parts = [];
        if (step.From.Count > 0)
        {
            parts.Add($"取自 {string.Join("、", step.From)}");
        }

        if (step.Role == RunRole.Check)
        {
            parts.Add($"不通过则 {Labels.Of(step.RejectAction)}，至多 {step.AttemptLimit} 轮");
        }

        if (step.Model is { Length: > 0 } model)
        {
            parts.Add($"模型 {model}");
        }

        return parts.Count == 0 ? "—" : string.Join("；", parts);
    }
}
