using System.ComponentModel;
using System.Reflection;
using ErrorOr;
using Microsoft.Extensions.AI;

namespace Kuroe.Agent;

/// <summary>把工具载体上带 DescriptionAttribute 的方法批量转换为模型可调用工具。</summary>
public static class ToolCollection
{
    public static ErrorOr<IReadOnlyList<AITool>> Create(IEnumerable<IAgentTool> providers)
    {
        List<AITool> tools =
        [
            .. providers.SelectMany(provider => provider.GetType()
                .GetMethods(BindingFlags.Public | BindingFlags.Instance | BindingFlags.DeclaredOnly)
                .Where(method => method.GetCustomAttribute<DescriptionAttribute>() is not null)
                .Select(method => (AITool)AIFunctionFactory.Create(method, provider))),
        ];

        if (tools.Count == 0)
        {
            return Error.Failure(
                "Tools.None",
                "没有发现任何可调用工具，检查工具载体上的方法是否为公开实例方法并标注 DescriptionAttribute。");
        }

        return tools;
    }
}
