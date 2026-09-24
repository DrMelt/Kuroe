using Kuroe.Agent.Turns;
using Kuroe.Shared.Agent.Tools;

namespace Kuroe.Agent.Tools;

/// <summary>需要知道本轮归属的工具载体。每轮请求换一个带身份的载体，避免共享实例上串台。
/// 不需要归属的工具仍按普通 <see cref="IAgentTool"/> 单例注册。</summary>
public interface IScopedAgentTool : IAgentTool
{
    /// <summary>本轮使用的载体。</summary>
    IAgentTool ForTurn(TurnScope scope);
}
