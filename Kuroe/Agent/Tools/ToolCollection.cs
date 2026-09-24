using System.Text;
using System.Text.Encodings.Web;
using System.Text.Json;
using Kuroe.Agent.Turns;
using Kuroe.Shared.Agent;
using Kuroe.Shared.Agent.Tools;
using Kuroe.Shared.Agent.Turns;
using Microsoft.Extensions.AI;

namespace Kuroe.Agent.Tools;

/// <summary>工具载体声明的函数构成的模型可调用集合，外加记录调用的包装。
/// 每轮请求按回合归属重新取载体，使调用记录落到该回合的记录上。</summary>
public sealed class ToolCollection
{
    private readonly IAgentTool[] _carriers;

    /// <summary>收集各载体的函数声明。</summary>
    public ToolCollection(IEnumerable<IAgentTool> carriers)
    {
        _carriers = [.. carriers];
        Names = [.. _carriers.SelectMany(carrier => carrier.Functions).Select(function => function.Name)];
    }

    /// <summary>工具名，供宿主展示可用性。</summary>
    public IReadOnlyList<string> Names { get; }

    /// <summary>模型可调用的工具，请求选项由会话取用。没有声明时为空。</summary>
    internal IReadOnlyList<AITool> Build(TurnScope scope, ITurnSink sink)
    {
        List<AITool> tools = [];
        foreach (IAgentTool carrier in _carriers)
        {
            IAgentTool bound = carrier is IScopedAgentTool scoped ? scoped.ForTurn(scope) : carrier;
            foreach (ToolFunction function in bound.Functions)
            {
                tools.Add(new DeclaredFunction(function, sink));
            }
        }

        return tools;
    }

    /// <summary>按声明执行的函数，调用参数与结果写进过程记录。</summary>
    private sealed class DeclaredFunction(ToolFunction declaration, ITurnSink sink) : AIFunction
    {
        private readonly JsonElement _schema = SchemaOf(declaration.Parameters);

        /// <summary>调用参数的呈现方式：非 ASCII 字符不转义。</summary>
        private static readonly JsonWriterOptions Rendering = new()
        {
            Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
        };

        public override string Name => declaration.Name;

        public override string Description => declaration.Description;

        public override JsonElement JsonSchema => _schema;

        protected override ValueTask<object?> InvokeCoreAsync(AIFunctionArguments arguments, CancellationToken cancellationToken)
        {
            string parameters = Describe(arguments);

            string result;
            try
            {
                result = declaration.Invoke(new ToolArguments(arguments));
            }
            catch (Exception ex)
            {
                sink.OnToolCall(new ToolCallRecord(Name, parameters, $"{ex.GetType().Name}: {ex.Message}", true));
                throw;
            }

            sink.OnToolCall(new ToolCallRecord(Name, parameters, result, false));

            return new ValueTask<object?>(result);
        }

        /// <summary>实参写成 JSON 文本。</summary>
        private static string Describe(AIFunctionArguments arguments)
        {
            using MemoryStream buffer = new();
            using (Utf8JsonWriter writer = new(buffer, Rendering))
            {
                writer.WriteStartObject();
                foreach ((string name, object? value) in arguments)
                {
                    writer.WritePropertyName(name);
                    WriteValue(writer, value);
                }

                writer.WriteEndObject();
            }

            return Encoding.UTF8.GetString(buffer.ToArray());
        }

        /// <summary>JSON 值原样写出，其余值按文本写出。</summary>
        private static void WriteValue(Utf8JsonWriter writer, object? value)
        {
            switch (value)
            {
                case null:
                    writer.WriteNullValue();
                    break;

                case JsonElement element:
                    element.WriteTo(writer);
                    break;

                case string text:
                    writer.WriteStringValue(text);
                    break;

                case bool flag:
                    writer.WriteBooleanValue(flag);
                    break;

                default:
                    writer.WriteStringValue(value.ToString());
                    break;
            }
        }
    }

    /// <summary>参数的 JSON Schema，随请求交给模型。</summary>
    private static JsonElement SchemaOf(IReadOnlyList<ToolParameter> parameters)
    {
        using MemoryStream buffer = new();
        using (Utf8JsonWriter writer = new(buffer))
        {
            writer.WriteStartObject();
            writer.WriteString("type", "object");
            writer.WriteStartObject("properties");
            foreach (ToolParameter parameter in parameters)
            {
                writer.WriteStartObject(parameter.Name);
                writer.WriteString("type", parameter.Flag ? "boolean" : "string");
                writer.WriteString("description", parameter.Description);
                writer.WriteEndObject();
            }

            writer.WriteEndObject();
            if (parameters.Any(parameter => parameter.Required))
            {
                writer.WriteStartArray("required");
                foreach (ToolParameter parameter in parameters.Where(parameter => parameter.Required))
                {
                    writer.WriteStringValue(parameter.Name);
                }

                writer.WriteEndArray();
            }

            writer.WriteEndObject();
        }

        return JsonDocument.Parse(buffer.ToArray()).RootElement.Clone();
    }
}
