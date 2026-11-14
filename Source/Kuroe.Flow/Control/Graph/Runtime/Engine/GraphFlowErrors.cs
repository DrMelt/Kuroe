namespace Kuroe.Flow.Control.Graph.Runtime.Engine;

/// <summary>图流程存档恢复的错误码。</summary>
internal static class GraphFlowErrors
{
    /// <summary>存档执行目标不在流程图中。</summary>
    public const string RecordFlowMismatch = "Record.FlowMismatch";

    /// <summary>存档步骤状态与流程节点类型不一致。</summary>
    public const string RecordStepMismatch = "Record.StepMismatch";
}