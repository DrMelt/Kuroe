using ErrorOr;
using Kuroe.Cli.Views;
using Kuroe.Shared.Catalogs;

namespace Kuroe.Cli.Commands;

/// <summary>catalog 命令族：列出、导出与合并导入目录。</summary>
internal sealed class CatalogCommands(CliSession session, CliOutput output)
{
    public int List()
    {
        var contents = session.Catalog.Snapshot();
        PlainCatalogView.Providers(output, contents.Providers);
        output.Line();
        PlainCatalogView.Models(output, contents.Models, session.Models.Current);

        return ExitCodes.Success;
    }

    public int Export(string file)
    {
        ErrorOr<string> exported = session.Catalog.Export(file);
        if (exported.IsError)
        {
            output.Report(exported.ErrorsOrEmptyList);

            return ExitCodes.Failure;
        }

        output.Line($"已导出到 {exported.Value}，凭据已替换为占位符。");

        return ExitCodes.Success;
    }

    public int Import(string file)
    {
        ErrorOr<CatalogMerge> imported = session.Catalog.Import(file);
        if (imported.IsError)
        {
            output.Report(imported.ErrorsOrEmptyList);

            return ExitCodes.Failure;
        }

        output.Line($"已从 {imported.Value.Source} 合并导入。");
        foreach (string note in imported.Value.Notes)
        {
            output.Hint(note);
        }

        if (imported.Value.PlaceholderProviders.Count > 0)
        {
            output.Hint($"用 provider key <提供商> <凭据> 替换占位符凭据：{string.Join('、', imported.Value.PlaceholderProviders)}");
        }

        return ExitCodes.Success;
    }
}