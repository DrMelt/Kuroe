using ErrorOr;
using Kuroe.Cli.Views;
using Kuroe.Shared;

namespace Kuroe.Cli.Commands;

/// <summary>provider 命令族：列出、新增、更换凭据与删除提供商。</summary>
internal sealed class ProviderCommands(CliSession session, CliOutput output)
{
    /// <summary>未给出凭据参数时读取的环境变量。</summary>
    internal const string CredentialEnvironmentVariable = "KUROE_PROVIDER_KEY";

    public int List()
    {
        PlainCatalogView.Providers(output, session.Catalog.Snapshot().Providers);

        return ExitCodes.Success;
    }

    public int Add(string name, string baseAddress, string? apiKey)
    {
        if (ResolveCredential(apiKey) is not { } credential)
        {
            output.Report([Error.Validation(ErrorCodes.CliCredentialMissing,
                $"缺少凭据：写入第三个参数或设置环境变量 {CredentialEnvironmentVariable}。")]);

            return ExitCodes.Failure;
        }

        ErrorOr<Success> added = session.Catalog.AddProvider(name, baseAddress, credential);
        if (added.IsError)
        {
            output.Report(added.ErrorsOrEmptyList);

            return ExitCodes.Failure;
        }

        output.Line($"已添加提供商 {name}。");

        return ExitCodes.Success;
    }

    public int Key(string name, string? apiKey)
    {
        if (ResolveCredential(apiKey) is not { } credential)
        {
            output.Report([Error.Validation(ErrorCodes.CliCredentialMissing,
                $"缺少凭据：写入第二个参数或设置环境变量 {CredentialEnvironmentVariable}。")]);

            return ExitCodes.Failure;
        }

        ErrorOr<Success> changed = session.Catalog.SetProviderKey(name, credential);
        if (changed.IsError)
        {
            output.Report(changed.ErrorsOrEmptyList);

            return ExitCodes.Failure;
        }

        output.Line($"已更换提供商 {name} 的凭据。");

        return ExitCodes.Success;
    }

    public int Remove(string name)
    {
        ErrorOr<Success> removed = session.Catalog.RemoveProvider(name);
        if (removed.IsError)
        {
            output.Report(removed.ErrorsOrEmptyList);

            return ExitCodes.Failure;
        }

        output.Line($"已删除提供商 {name}。");

        return ExitCodes.Success;
    }

    /// <summary>凭据参数为空时读环境变量，两者都为空时返回空。</summary>
    private static string? ResolveCredential(string? apiKey)
    {
        string? value = string.IsNullOrWhiteSpace(apiKey)
            ? Environment.GetEnvironmentVariable(CredentialEnvironmentVariable)
            : apiKey;

        return string.IsNullOrWhiteSpace(value) ? null : value;
    }
}