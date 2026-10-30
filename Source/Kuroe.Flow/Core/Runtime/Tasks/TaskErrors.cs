using ErrorOr;
using Kuroe.Flow.Shared;
using Kuroe.Flow.Shared.Model;
using Kuroe.Flow.Shared.RuntimeGraph.Execution;

namespace Kuroe.Flow.Runtime.Tasks;

/// <summary>任务域的错误构造。</summary>
static class TaskErrors
{
    public static Error NotFound(string reference) =>
        Error.NotFound(FlowErrorCodes.TaskNotFound, $"没有任务 {reference}。");

    /// <summary>一个引用匹配到多个任务。</summary>
    public static Error Ambiguous(string reference) =>
        Error.Validation(FlowErrorCodes.TaskNotFound, $"任务引用 {reference} 匹配多个任务，请给出完整键。");

    /// <summary>开始端口没有给出输入。</summary>
    public static Error MissingInput(PortName port) =>
        Error.Validation(FlowErrorCodes.TaskInput, $"开始端口 {port} 没有输入。");

    /// <summary>开始端口给出的输入为空。</summary>
    public static Error BlankInput(PortName port) =>
        Error.Validation(FlowErrorCodes.TaskInput, $"开始端口 {port} 的输入不能为空。");

    /// <summary>输入落在流程没有声明的开始端口上。</summary>
    public static Error UnknownInput(PortName port) =>
        Error.Validation(FlowErrorCodes.TaskInput, $"流程没有开始端口 {port}。");

    /// <summary>输入落在带固定值的开始端口上。</summary>
    public static Error ConstantInput(PortName port) =>
        Error.Validation(FlowErrorCodes.TaskInput, $"开始端口 {port} 带固定值，不接受提交输入。");

    /// <summary>输入落在绑定到宿主输入的开始端口上。</summary>
    public static Error HostInput(PortName port) =>
        Error.Validation(FlowErrorCodes.TaskInput, $"开始端口 {port} 由宿主提供，不接受提交输入。");

    /// <summary>宿主没有为绑定端口给出值。</summary>
    public static Error MissingHostValue(PortName port) =>
        Error.Validation(FlowErrorCodes.TaskInput, $"开始端口 {port} 的宿主输入没有给出值。");

    /// <summary>宿主为绑定端口给出的值为空。</summary>
    public static Error BlankHostValue(PortName port) =>
        Error.Validation(FlowErrorCodes.TaskInput, $"开始端口 {port} 的宿主输入不能为空。");

    /// <summary>同一开始端口给出了多项输入。</summary>
    public static Error DuplicateInput(PortName port) =>
        Error.Validation(FlowErrorCodes.TaskInput, $"开始端口 {port} 的输入重复。");

    /// <summary>该任务没有被阻塞的执行节点。</summary>
    public static Error NotBlocked(TaskId task) =>
        Error.Validation("Task.Rework", $"{task} 没有被阻塞的执行节点。");

    /// <summary>任务已暂停，不能推进或交付输入。</summary>
    public static Error Paused(TaskId task) =>
        Error.Conflict(FlowErrorCodes.TaskPaused, $"{task} 已暂停。");

    /// <summary>任务已是暂停状态。</summary>
    public static Error AlreadyPaused(TaskId task) =>
        Error.Conflict(FlowErrorCodes.TaskAlreadyPaused, $"{task} 已暂停。");

    /// <summary>任务不在暂停状态。</summary>
    public static Error NotPaused(TaskId task) =>
        Error.Conflict(FlowErrorCodes.TaskNotPaused, $"{task} 未暂停。");

    /// <summary>任务已终止。</summary>
    public static Error Terminated(TaskId task) =>
        Error.Conflict(FlowErrorCodes.TaskTerminated, $"{task} 已终止。");

    /// <summary>已完成的任务不能暂停。</summary>
    public static Error DoneCannotPause(TaskId task) =>
        Error.Conflict(FlowErrorCodes.TaskDoneCannotPause, $"{task} 已完成，不能暂停。");

    /// <summary>已完成的任务不能终止。</summary>
    public static Error DoneCannotTerminate(TaskId task) =>
        Error.Conflict(FlowErrorCodes.TaskDoneCannotTerminate, $"{task} 已完成，不能终止。");
}
