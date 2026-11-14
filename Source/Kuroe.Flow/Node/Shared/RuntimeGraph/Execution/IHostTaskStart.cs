using ErrorOr;
using Kuroe.Tasks.Shared;

namespace Kuroe.Flow.Node.Shared.RuntimeGraph.Execution;

/// <summary>任务启动时提供开始端口值的钩子：宿主开始输入表里的端口由此解析并给值。</summary>
public interface IHostTaskStart
{
    /// <summary>准备资源并给出每个绑定端口的值。bindings 是已通过校验的开始端口到宿主输入名的绑定。
    /// 返回 Conflict 表示任务号对应的宿主资源已被占用。</summary>
    /// <param name="id">本次任务的任务号。</param>
    /// <param name="bindings">开始端口到宿主输入名的绑定。</param>
    ErrorOr<IPortTexts> Prepare(
        TaskId id,
        IReadOnlyDictionary<PortName, string> bindings);
}