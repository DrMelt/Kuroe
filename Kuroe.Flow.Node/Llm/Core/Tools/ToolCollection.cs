using System.Text.Json;
using ErrorOr;
using Kuroe.Flow.Shared.Execution;
using Kuroe.Shared.Executions.Tools;
using Kuroe.Flow.Shared.Execution.Turns;
using Kuroe.Flow.Shared.StaticGraph;
using Microsoft.Extensions.AI;

namespace Kuroe.Flow.Node.Llm.Tools;

/// <summary>工具载体声明的函数构成的模型可调用集合，外加记录调用的包装。
/// 每轮请求按回合归属重新取载体，使调用记录落到该回合的记录上。
/// 工具面从回合的归属取用：null 表示全部，名单表示只给这些函数。JSON 承载由 <see cref="IToolJson"/> 给出。</summary>
public sealed class ToolCollection
{
    private readonly ITool[] _carriers;
    private readonly IToolJson _json;

    /// <summary>收集各载体的函数声明。</summary>
    public ToolCollection(IEnumerable<ITool> carriers, IToolJson json)
    {
        _carriers = [.. carriers];
        _json = json;
        Names = [.. _carriers.SelectMany(carrier => carrier.Functions).Select(function => function.Definition.Name)];
    }

    /// <summary>工具名，供宿主展示可用性。</summary>
    public IReadOnlyList<ToolName> Names { get; }

    /// <summary>模型可调用的工具，请求选项由会话取用。没有声明时为空。
    /// 工具面从回合的归属取用：null 表示全部，路径名单表示只给这些函数及其子树。</summary>
    internal IReadOnlyList<AITool> Build(TurnScope scope, ITurnSink sink, IReadOnlyList<CapabilityPath>? tools)
    {
        List<AITool> result = [];
        foreach (ITool carrier in _carriers)
        {
            ITool bound = carrier is IScopedTool scoped ? scoped.ForTurn(scope) : carrier;
            IEnumerable<ToolFunction> allowed = tools is null
                ? bound.Functions
                : bound.Functions.Where(function => tools.Any(entry => entry.Allows(function.Definition.Path.Value)));
            result.AddRange(allowed.Select(function => new DeclaredFunction(function, sink, _json)));
        }

        return result;
    }

    /// <summary>按声明执行的函数，调用参数与结果写进过程记录。</summary>
    private sealed class DeclaredFunction(ToolFunction declaration, ITurnSink sink, IToolJson json) : AIFunction
    {
        private readonly JsonElement _schema = json.Schema(declaration.Definition.Parameters);

        public override string Name => declaration.Definition.Name.Value;

        public override string Description => declaration.Definition.Description;

        public override JsonElement JsonSchema => _schema;

        protected override ValueTask<object?> InvokeCoreAsync(AIFunctionArguments arguments, CancellationToken cancellationToken)
        {
            string parameters = json.Describe(arguments);

            ErrorOr<string> outcome;
            try
            {
                outcome = declaration.Invoke(new ToolArguments(arguments));
            }
            catch (Exception ex)
            {
                sink.OnToolCall(new ToolCallRecord(declaration.Definition.Name.Value, parameters, $"{ex.GetType().Name}: {ex.Message}", true));
                throw;
            }

            string result = ToolResult.Render(outcome);
            sink.OnToolCall(new ToolCallRecord(declaration.Definition.Name.Value, parameters, result, outcome.IsError));

            return new ValueTask<object?>(result);
        }
    }
}
