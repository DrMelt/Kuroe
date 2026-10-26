using System.Text.Json;
using ErrorOr;
using Kuroe.Node.Llm.Executions;
using Kuroe.Flow.Shared.Execution;
using Kuroe.Tool;
using Microsoft.Extensions.AI;

namespace Kuroe.Node.Llm.Tools;

/// <summary>工具载体声明的函数构成的模型可调用集合，外加记录调用的包装。
/// 每轮请求按所属执行重新取载体，使调用记录落到该执行的记录上。
/// 工具面按白名单给出：只给路径落在名单里的函数及其子树。JSON 承载由 <see cref="IToolJson"/> 给出。</summary>
public sealed class ToolCollection
{
    private readonly ITool[] _carriers;
    private readonly IToolJson _json;

    /// <summary>收集各载体的函数声明。</summary>
    public ToolCollection(IEnumerable<ITool> carriers, IToolJson json)
    {
        _carriers = [.. carriers];
        _json = json;
        Definitions = [.. _carriers.SelectMany(carrier => carrier.Functions).Select(function => function.Definition)];
        Names = [.. Definitions.Select(definition => definition.Path.Name)];
    }

    /// <summary>全部函数的定义，按载体与声明顺序排列，供宿主展示工具面。</summary>
    public IReadOnlyList<ToolDefinition> Definitions { get; }

    /// <summary>函数名，取工具路径的末段，供宿主展示可用性。</summary>
    public IReadOnlyList<ToolName> Names { get; }

    /// <summary>模型可调用的工具，请求选项由会话取用。没有声明时为空。
    /// 工具面按本轮的能力路径给出：只给路径落在名单里的函数及其子树。</summary>
    internal IReadOnlyList<AITool> Build(ExecutionId execution, ProcessJournal journal, IReadOnlyList<ToolPath> tools)
    {
        List<AITool> result = [];
        foreach (ITool carrier in _carriers)
        {
            ITool bound = carrier is IScopedTool scoped ? scoped.ForExecution(execution) : carrier;
            result.AddRange(bound.Functions
                .Where(function => tools.Any(entry => entry.Allows(function.Definition.Path.Value)))
                .Select(function => new DeclaredFunction(function, journal, _json)));
        }

        return result;
    }

    /// <summary>按声明执行的函数，调用参数与结果写进过程记录，过程条目随后播发。
    /// 模型侧的名字取路径的调用名，过程记录与展示仍用路径。</summary>
    private sealed class DeclaredFunction(ToolFunction declaration, ProcessJournal journal, IToolJson json) : AIFunction
    {
        private readonly JsonElement _schema = json.Schema(declaration.Definition.Parameters);

        public override string Name => declaration.Definition.Path.CallName.Value;

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
                Record(new ToolCallRecord(declaration.Definition.Path.Value, parameters, $"{ex.GetType().Name}: {ex.Message}", true));
                throw;
            }

            string result = ToolResult.Render(outcome);
            Record(new ToolCallRecord(declaration.Definition.Path.Value, parameters, result, outcome.IsError));

            return new ValueTask<object?>(result);
        }

        /// <summary>一次调用落进过程记录，过程条目由执行器播发。</summary>
        private void Record(ToolCallRecord call) =>
            journal.AppendToolCall(call);
    }
}
