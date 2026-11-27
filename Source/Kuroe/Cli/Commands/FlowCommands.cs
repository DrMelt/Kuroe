using ErrorOr;
using Kuroe.Cli.Views;
using Kuroe.Kernel.Presentation.Shared;
using Kuroe.Kernel.Tasks.Shared;
using Kuroe.Flow.Library;

namespace Kuroe.Cli.Commands;

/// <summary>flow 命令族：列出、查看与导入流程。</summary>
internal sealed class FlowCommands(CliSession session, CliOutput output)
{
    public int List()
    {
        IReadOnlyList<FlowTemplateView> all = session.Templates.All();
        HashSet<string> available = [.. all.Select(flow => flow.Name)];
        FlowFailure[] failures =
        [
            .. session.Library.Failures.Where(failure => !available.Contains(failure.Name.Value)),
        ];
        PlainFlowView.List(output, all, failures);

        return ExitCodes.Success;
    }

    public int Show(string name, string? engine)
    {
        ErrorOr<FlowName> parsed = FlowName.Create(name);
        if (parsed.IsError)
        {
            output.Report(parsed.ErrorsOrEmptyList);

            return ExitCodes.Failure;
        }

        if (engine is { Length: > 0 } implementation)
        {
            ErrorOr<FlowTemplateView> found =
                session.Templates.Find(parsed.Value, new FlowImplementation(implementation.Trim()));
            if (found.IsError)
            {
                output.Report(found.ErrorsOrEmptyList);

                return ExitCodes.Failure;
            }

            PlainFlowView.Show(output, found.Value);

            return ExitCodes.Success;
        }

        IReadOnlyList<FlowTemplateView> all = session.Templates.FindAll(parsed.Value);
        if (all.Count == 0)
        {
            output.Report([Error.Validation(FlowErrorCodes.FlowName, $"流程 {parsed.Value} 不存在。")]);

            return ExitCodes.Failure;
        }

        foreach (FlowTemplateView flow in all)
        {
            PlainFlowView.Show(output, flow);
        }

        return ExitCodes.Success;
    }

    public int Add(string file)
    {
        ErrorOr<FlowImport> imported = session.Library.Import(file);
        if (imported.IsError)
        {
            output.Report(imported.ErrorsOrEmptyList);

            return ExitCodes.Failure;
        }

        output.Line($"已从 {imported.Value.Source} 导入 {Summary(imported.Value)}。");
        foreach (string note in imported.Value.Notes)
        {
            output.Hint(note);
        }

        return ExitCodes.Success;
    }

    /// <summary>导入结果的条数说明，节点库与流程分开计数。</summary>
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
}