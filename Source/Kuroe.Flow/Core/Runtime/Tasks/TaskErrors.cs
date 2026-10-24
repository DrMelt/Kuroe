using ErrorOr;
using Kuroe.Flow.Shared;
using Kuroe.Flow.Shared.Execution;
using Kuroe.Flow.Shared.StaticGraph.PortModel;

namespace Kuroe.Flow.Runtime.Tasks;

/// <summary>任务域的错误构造。</summary>
static class TaskErrors
{
    public static Error NotFound(int value) => Error.NotFound(FlowErrorCodes.TaskNotFound, $"没有任务 #{value}。");

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

    /// <summary>同一开始端口给出了多项输入。</summary>
    public static Error DuplicateInput(PortName port) =>
        Error.Validation(FlowErrorCodes.TaskInput, $"开始端口 {port} 的输入重复。");

    /// <summary>该任务没有被阻塞的执行节点。</summary>
    public static Error NotBlocked(TaskId task) =>
        Error.Validation("Task.Rework", $"{task} 没有被阻塞的执行节点。");
}
