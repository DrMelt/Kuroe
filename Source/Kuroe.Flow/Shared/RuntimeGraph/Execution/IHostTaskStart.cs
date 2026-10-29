using ErrorOr;
using Kuroe.Flow.Shared.Model;
using Kuroe.Flow.Shared.RuntimeGraph.Ports;
using Kuroe.Flow.Shared.StaticGraph.Flow;

namespace Kuroe.Flow.Shared.RuntimeGraph.Execution;

/// <summary>任务启动时提供开始端口值的钩子：流程绑定到宿主输入名的端口由此解析并给值。</summary>
public interface IHostTaskStart
{
    /// <summary>准备资源并给出每个绑定端口的值。bindings 的键是流程声明的开始端口，值是宿主输入名。
    /// 返回 Conflict 表示任务号不可用，提交方换号重试。</summary>
    /// <param name="id">本次任务的任务号。</param>
    /// <param name="bindings">开始端口到宿主输入名的绑定。</param>
    /// <param name="flow">任务所属的流程名。</param>
    /// <param name="title">任务标题。</param>
    ErrorOr<IPortTexts> Prepare(
        TaskId id,
        IReadOnlyDictionary<PortName, string> bindings,
        FlowName flow,
        string title);
}