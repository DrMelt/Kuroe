namespace Kuroe.Workflows.Tasks;

/// <summary>Kuroe 提供的宿主开始输入名。流程在 HostStartInputs 的值里引用这些名字。</summary>
public static class StartSources
{
    /// <summary>工作区根目录。</summary>
    public const string Workspace = "WORKSPACE_PATH";

    /// <summary>本次任务的工作目录。</summary>
    public const string Task = "TASK_WORK_PATH";
}