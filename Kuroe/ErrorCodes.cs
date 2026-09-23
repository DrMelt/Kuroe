namespace Kuroe;

/// <summary>宿主按错误码补充操作指引，这些码由库侧的错误构造与宿主的指引共用。</summary>
public static class ErrorCodes
{
    public const string SettingsModelPath = "Settings.ModelPath";

    public const string SettingsNullValue = "Settings.NullValue";

    public const string TaskNotFound = "Task.NotFound";

    public const string RunNotFound = "Run.NotFound";

    public const string WorkflowNotFound = "Workflow.NotFound";

    public const string WorkflowFormat = "Workflow.Format";

    public const string WorkflowName = "Workflow.Name";

    public const string WorkflowStep = "Workflow.Step";

    public const string WorkflowBody = "Workflow.Body";

    public const string WorkflowInvalidPath = "Workflow.InvalidPath";
}