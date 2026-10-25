namespace Kuroe.Flow.Shared.Execution;

/// <summary>任务启动时的宿主路径：工作区根目录与任务目录。任务域没有工作目录时任务目录为空。</summary>
/// <param name="WorkspaceDirectory">工作区根目录的绝对路径。</param>
/// <param name="TaskDirectory">任务目录的绝对路径，任务域没有工作目录时为空。</param>
public sealed record StartPaths(string WorkspaceDirectory, string? TaskDirectory);