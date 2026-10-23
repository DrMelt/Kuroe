using ApiHub.Shared.Models;
using ErrorOr;
using Kuroe.Cli.Views;
using Kuroe.Shared.Catalogs;
using Kuroe.Shared.Configuration;

namespace Kuroe.Cli.Commands;

/// <summary>model 命令族：查看当前模型、切换、注册与注销。</summary>
internal sealed class ModelCommands(CliSession session, CliOutput output)
{
    public int Current()
    {
        output.Line(session.Models.Current is { } current
            ? $"当前模型 {current.Value}"
            : "当前未选择模型。");

        return ExitCodes.Success;
    }

    public int List()
    {
        PlainCatalogView.Models(output, session.Catalog.Snapshot().Models, session.Models.Current);

        return ExitCodes.Success;
    }

    public int Use(string name)
    {
        ErrorOr<ModelName> parsed = ModelName.Create(name.Trim());
        if (parsed.IsError)
        {
            output.Report(parsed.ErrorsOrEmptyList);

            return ExitCodes.Failure;
        }

        ErrorOr<ModelSelection> selected = session.Models.Select(parsed.Value);
        if (selected.IsError)
        {
            output.Report(selected.ErrorsOrEmptyList);
            output.Hint("用 model list 查看已注册模型，用 model add <模型> <提供商> 注册。");

            return ExitCodes.Failure;
        }

        output.Line($"已选择模型 {parsed.Value.Value}。");
        Report(selected.Value.Effect);

        return ExitCodes.Success;
    }

    public int None()
    {
        ErrorOr<ModelSelection> unselected = session.Models.Unselect();
        if (unselected.IsError)
        {
            output.Report(unselected.ErrorsOrEmptyList);

            return ExitCodes.Failure;
        }

        output.Line(unselected.Value.Changed ? "已取消模型选择。" : "当前未选择模型。");

        return ExitCodes.Success;
    }

    public int Add(string model, string provider)
    {
        ErrorOr<ModelName> modelName = ModelName.Create(model.Trim());
        ErrorOr<ProviderName> providerName = ProviderName.Create(provider.Trim());
        if (modelName.IsError || providerName.IsError)
        {
            output.Report([.. modelName.ErrorsOrEmptyList, .. providerName.ErrorsOrEmptyList]);

            return ExitCodes.Failure;
        }

        ErrorOr<Success> added = session.Catalog.AddModel(modelName.Value, providerName.Value);
        if (added.IsError)
        {
            output.Report(added.ErrorsOrEmptyList);

            return ExitCodes.Failure;
        }

        output.Line($"已注册模型 {modelName.Value.Value}。");

        return ExitCodes.Success;
    }

    public int Remove(string model)
    {
        ErrorOr<ModelName> parsed = ModelName.Create(model.Trim());
        if (parsed.IsError)
        {
            output.Report(parsed.ErrorsOrEmptyList);

            return ExitCodes.Failure;
        }

        ErrorOr<ModelRemoval> removed = session.Models.Remove(parsed.Value);
        if (removed.IsError)
        {
            output.Report(removed.ErrorsOrEmptyList);

            return ExitCodes.Failure;
        }

        output.Line(removed.Value.Failures.Count > 0
            ? $"已注销模型 {parsed.Value.Value}，取消选择未生效。"
            : $"已注销模型 {parsed.Value.Value}。");

        if (removed.Value.Failures.Count > 0)
        {
            output.Report(removed.Value.Failures);

            return ExitCodes.Failure;
        }

        return ExitCodes.Success;
    }

    /// <summary>模型选择改动不需要重启，后续调用会重新读取用户层。</summary>
    private void Report(SettingsEffect effect) =>
        output.Hint(effect.RequiresRestart ? "下次运行生效。" : "已生效。");
}