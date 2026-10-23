using ErrorOr;
using Kuroe.Cli.Views;
using Kuroe.Configuration;
using Kuroe.Shared.Configuration;

namespace Kuroe.Cli.Commands;

/// <summary>config 命令族：打印生效配置、写入与删除用户层设置。</summary>
internal sealed class ConfigCommands(CliSession session, CliOutput output)
{
    public int List()
    {
        PlainSettingsView.Print(output, session.Settings);

        return ExitCodes.Success;
    }

    public int Set(string path, string value)
    {
        ErrorOr<SettingsEffect> applied = session.Settings.Set(path, value);
        if (applied.IsError)
        {
            output.Report(applied.ErrorsOrEmptyList);

            return ExitCodes.Failure;
        }

        if (applied.Value.RequiresRestart)
        {
            output.Line("已保存，下次运行生效。");
        }
        else if (applied.Value.RequiresDialogueRebuild)
        {
            output.Line("已保存；该设置只作用于 Kuroe.Console，下次启动生效。");
        }
        else
        {
            output.Line("已保存并生效。");
        }

        return ExitCodes.Success;
    }

    public int Unset(string path)
    {
        ErrorOr<string> resolved = SettingsProvider.ResolvePath(path);
        if (resolved.IsError)
        {
            output.Report(resolved.ErrorsOrEmptyList);

            return ExitCodes.Failure;
        }

        if (session.Settings.TryGetUserValue(resolved.Value) is null)
        {
            output.Line($"{resolved.Value} 未在用户层设置。");

            return ExitCodes.Success;
        }

        ErrorOr<SettingsEffect> cleared = session.Settings.Clear(resolved.Value);
        if (cleared.IsError)
        {
            output.Report(cleared.ErrorsOrEmptyList);

            return ExitCodes.Failure;
        }

        output.Line("已删除该项。");

        return ExitCodes.Success;
    }
}