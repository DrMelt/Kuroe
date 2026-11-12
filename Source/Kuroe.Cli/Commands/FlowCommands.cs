using ErrorOr;
using Kuroe.Cli.Views;
using Kuroe.Flow.Presentation;
using Kuroe.Flow.Tasks;
using Kuroe.Workflows.Flows;

namespace Kuroe.Cli.Commands;

/// <summary>flow 命令族：列出、查看与导入流程。</summary>
internal sealed class FlowCommands(CliSession session, CliOutput output)
{
    public int List()
    {
        PlainFlowView.List(output, session.Templates.All(), session.Library.Failures);

        return ExitCodes.Success;
    }

    public int Show(string name)
    {
        ErrorOr<FlowName> parsed = FlowName.Create(name);
        if (parsed.IsError)
        {
            output.Report(parsed.ErrorsOrEmptyList);

            return ExitCodes.Failure;
        }

        ErrorOr<FlowTemplateView> found = session.Templates.Find(parsed.Value);
        if (found.IsError)
        {
            output.Report(found.ErrorsOrEmptyList);

            return ExitCodes.Failure;
        }

        PlainFlowView.Show(output, found.Value);

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