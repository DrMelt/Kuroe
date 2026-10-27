namespace Kuroe.Tool;

/// <summary>工具源：装配期提供函数定义，运行期按执行上下文创建本轮可用的载体。</summary>
public interface IToolSource
{
    /// <summary>本源的函数定义，按声明序。</summary>
    IReadOnlyList<ToolDefinition> Definitions { get; }

    /// <summary>按执行上下文创建本轮可用工具面内的载体。</summary>
    IReadOnlyList<ITool> Create(ToolContext context);
}