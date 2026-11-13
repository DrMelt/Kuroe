using ErrorOr;
using Kuroe.Flow.Shared.Model;
using Kuroe.Flow.Node.StaticGraph.Flow;

namespace Kuroe.Flow.Node.RuntimeGraph.Execution;

/// <summary>宿主开始输入表：按流程名给出开始端口到宿主输入名的绑定。表由宿主维护，不进入流程定义与流程编译。
/// 表项里的开始端口由宿主在任务启动时给值，不接受提交输入；表项之外承载内容的开始端口按用户输入处理，不承载内容的开始端口不进入提交，没有表项的流程不获得宿主输入。</summary>
public interface IHostStartInputs
{
    /// <summary>该流程的表项，没有表项时为空表。展示用它读取配置原样，不做与开始入口的一致性判断。</summary>
    /// <param name="flow">流程名。</param>
    IReadOnlyDictionary<PortName, string> Bindings(FlowName flow);

    /// <summary>按流程的开始入口校验表项并给出可用的绑定，表项与开始入口不一致时一次给出全部错误。
    /// 绑定端口必须已在开始入口声明、不带固定值且承载 Text，带默认值的端口允许绑定。</summary>
    /// <param name="flow">要绑定宿主输入的流程。</param>
    ErrorOr<IReadOnlyDictionary<PortName, string>> Resolve(FlowDefinition flow);
}