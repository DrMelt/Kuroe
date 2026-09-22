using System.ComponentModel;
using System.Reflection;
using Microsoft.Extensions.AI;

namespace Kuroe.Agent;

/// <summary>工具载体上标注 DescriptionAttribute 的公开方法构成的模型可调用工具集合。实例方法需要载体实例，静态方法不需要。</summary>
public sealed class ToolCollection
{
    private const BindingFlags Scan = BindingFlags.Public | BindingFlags.Static | BindingFlags.Instance | BindingFlags.DeclaredOnly;

    private readonly AIFunction[] _functions;

    public ToolCollection(IEnumerable<IAgentTool> providers)
    {
        _functions = [.. providers.SelectMany(provider => provider.GetType()
            .GetMethods(Scan)
            .Where(method => method.GetCustomAttribute<DescriptionAttribute>() is not null)
            .Select(method => AIFunctionFactory.Create(method, method.IsStatic ? null : provider)))];

        Names = [.. _functions.Select(function => function.Name)];
    }

    /// <summary>模型可调用的工具，请求选项由会话取用。没有符合条件的载体方法时为空。</summary>
    internal IReadOnlyList<AITool> Tools => _functions;

    /// <summary>工具名，供宿主展示可用性。</summary>
    public IReadOnlyList<string> Names { get; }
}
