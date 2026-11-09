namespace Kuroe.Workflows.Tasks;

/// <summary>Kuroe 提供的宿主开始输入名。宿主开始输入表的值引用这些名字。</summary>
public static class StartSources
{
    /// <summary>工作区根目录。</summary>
    public const string Workspace = "WORKSPACE_PATH";

    /// <summary>本次任务的工作目录。</summary>
    public const string Task = "TASK_WORK_PATH";

    /// <summary>任务启动时宿主当前选中的模型名。</summary>
    public const string Model = "MODEL";

    /// <summary>全部可用的宿主输入名，开始输入表的值只能取其中之一。</summary>
    public static IReadOnlySet<string> Supported { get; } =
        new HashSet<string> { Workspace, Task, Model };
}