using System.Text.Json;
using ErrorOr;
using Kuroe.Node.Llm.Executions;
using Kuroe.Flow.Shared.Execution;
using Kuroe.Tool;
using Kuroe.Flow.Shared.Execution.Process;
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
        Names = [.. _carriers.SelectMany(carrier => carrier.Functions).Select(function => function.Definition.Name)];
    }

    /// <summary>工具名，供宿主展示可用性。</summary>
    public IReadOnlyList<ToolName> Names { get; }

    /// <summary>模型可调用的工具，请求选项由会话取用。没有声明时为空。
    /// 工具面按本轮的能力路径给出：只给路径落在名单里的函数及其子树。</summary>
    internal IReadOnlyList<AITool> Build(ExecutionId execution, IProcessSink sink, ProcessJournal journal, IReadOnlyList<ToolPath> tools)
    {
        List<AITool> result = [];
        foreach (ITool carrier in _carriers)
        {
            ITool bound = carrier is IScopedTool scoped ? scoped.ForExecution(execution) : carrier;
            result.AddRange(bound.Functions
                .Where(function => tools.Any(entry => entry.Allows(function.Definition.Path.Value)))
                .Select(function => new DeclaredFunction(function, sink, journal, _json)));
        }

        return result;
    }

    /// <summary>按声明执行的函数，调用参数与结果写进过程记录，同时播发给观察者。</summary>
    private sealed class DeclaredFunction(ToolFunction declaration, IProcessSink sink, ProcessJournal journal, IToolJson json) : AIFunction
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
                Record(new ToolCallRecord(declaration.Definition.Name.Value, parameters, $"{ex.GetType().Name}: {ex.Message}", true));
                throw;
            }

            string result = ToolResult.Render(outcome);
            Record(new ToolCallRecord(declaration.Definition.Name.Value, parameters, result, outcome.IsError));

            return new ValueTask<object?>(result);
        }

        /// <summary>一次调用同时落记录与播发。</summary>
        private void Record(ToolCallRecord call)
        {
            journal.Append(new ToolCallEntry(call));
            sink.OnToolCall(call);
        }
    }
}
