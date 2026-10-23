namespace Kuroe.Cli;

/// <summary>一次 CLI 调用的退出码。</summary>
internal static class ExitCodes
{
    /// <summary>命令完成。</summary>
    public const int Success = 0;

    /// <summary>参数、装配或操作失败。</summary>
    public const int Failure = 1;

    /// <summary>任务阻塞。</summary>
    public const int Blocked = 3;
}