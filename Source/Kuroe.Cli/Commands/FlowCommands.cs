using ErrorOr;
using Kuroe.Cli.Views;
using Kuroe.Flow.Shared.StaticGraph.Flow;
using Kuroe.Workflows.Flows;

namespace Kuroe.Cli.Commands;

/// <summary>flow 命令族：列出、查看与导入流程。</summary>
internal sealed class FlowCommands(CliSession session, CliOutput output)
{
    public int List()
    {
        PlainFlowView.List(output, session.Flows.All(), session.Flows.Failures);

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

        ErrorOr<CompiledFlow> found = session.Flows.FindCompiled(parsed.Value);
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
        ErrorOr<FlowImport> imported = session.Flows.Import(file);
        if (imported.IsError)
        {
            output.Report(imported.ErrorsOrEmptyList);

            return ExitCodes.Failure;
        }

        output.Line($"已从 {imported.Value.Source} 导入 {imported.Value.Names.Count} 条流程。");
        foreach (string note in imported.Value.Notes)
        {
            output.Hint(note);
        }

        return ExitCodes.Success;
    }
}