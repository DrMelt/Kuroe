using System.ComponentModel;
using System.Reflection;
using System.Text.Encodings.Web;
using System.Text.Json;
using Microsoft.Extensions.AI;

namespace Kuroe.Agent;

/// <summary>工具载体上标注 DescriptionAttribute 的公开方法构成的模型可调用工具集合。实例方法需要载体实例，静态方法不需要。
/// 每轮请求按回合归属重新构造载体与包装，使调用记录落到该回合的记录上。</summary>
public sealed class ToolCollection
{
    private const BindingFlags Scan = BindingFlags.Public | BindingFlags.Static | BindingFlags.Instance | BindingFlags.DeclaredOnly;

    private readonly Provider[] _providers;

    public ToolCollection(IEnumerable<IAgentTool> providers)
    {
        _providers = [.. providers.Select(provider => new Provider(provider, [.. provider.GetType()
            .GetMethods(Scan)
            .Where(method => method.GetCustomAttribute<DescriptionAttribute>() is not null)]))];

        Names = [.. _providers.SelectMany(provider => provider.Methods.Select(method => method.Name))];
    }

    /// <summary>工具名，供宿主展示可用性。</summary>
    public IReadOnlyList<string> Names { get; }

    /// <summary>模型可调用的工具，请求选项由会话取用。没有符合条件的载体方法时为空。</summary>
    internal IReadOnlyList<AITool> Build(TurnScope scope, ITurnSink sink)
    {
        List<AITool> tools = [];
        foreach (Provider provider in _providers)
        {
            IAgentTool carrier = provider.Instance is IScopedAgentTool scoped ? scoped.ForTurn(scope) : provider.Instance;
            foreach (MethodInfo method in provider.Methods)
            {
                tools.Add(new RecordingAIFunction(
                    AIFunctionFactory.Create(method, method.IsStatic ? null : carrier), sink));
            }
        }

        return tools;
    }

    private sealed record Provider(IAgentTool Instance, MethodInfo[] Methods);

    /// <summary>记录一次调用的包装，执行原样转发给被包装的函数。</summary>
    private sealed class RecordingAIFunction(AIFunction inner, ITurnSink sink) : DelegatingAIFunction(inner)
    {
        /// <summary>参数与结果写成文本时保留非 ASCII 字符。</summary>
        private static readonly JsonSerializerOptions Display = new()
        {
            Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
        };

        protected override async ValueTask<object?> InvokeCoreAsync(AIFunctionArguments arguments, CancellationToken cancellationToken)
        {
            string parameters = JsonSerializer.Serialize(new Dictionary<string, object?>(arguments), Display);
            try
            {
                object? result = await base.InvokeCoreAsync(arguments, cancellationToken);
                sink.OnToolCall(new ToolCallRecord(Name, parameters, Describe(result), false));
                return result;
            }
            catch (Exception ex)
            {
                sink.OnToolCall(new ToolCallRecord(Name, parameters, $"{ex.GetType().Name}: {ex.Message}", true));
                throw;
            }
        }

        /// <summary>返回值转成文本，字符串形式的返回值去掉 JSON 引号。</summary>
        private static string Describe(object? result) => result switch
        {
            JsonElement element when element.ValueKind is JsonValueKind.String => element.GetString() ?? string.Empty,
            JsonElement element => JsonSerializer.Serialize(element, Display),
            _ => JsonSerializer.Serialize(result, Display),
        };
    }
}
