using ErrorOr;
using Kuroe.Flow.Tasks.Records.Shared;
using Kuroe.Flow.Tasks.Shared;

namespace Kuroe.Control.Shared;

/// <summary>流程实现的提交与恢复：校验提交输入并给出初始状态，从存档重建流程状态。</summary>
public interface IFlowSubmission
{
    /// <summary>校验提交输入并给出初始状态、标题与流程定义记录。</summary>
    ErrorOr<FlowSubmission> Prepare(TaskId id, FlowName name, StartInputs inputs, string? title);

    /// <summary>从存档恢复流程状态与执行。</summary>
    ErrorOr<FlowRestore> Restore(TaskRecord record);
}