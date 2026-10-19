using Kuroe.Flow.Shared.Execution;
using Kuroe.Tool;

namespace Kuroe.Node.Llm.Tools;

/// <summary>需要知道所属执行的工具载体。每轮请求换一个带身份的载体，避免共享实例上串台。
/// 不需要归属的工具仍按普通 <see cref="ITool"/> 单例注册。</summary>
public interface IScopedTool : ITool
{
    /// <summary>该执行使用的载体。</summary>
    ITool ForExecution(ExecutionId execution);
}
