using Kuroe.Executor.Shared.Parameters;

namespace Kuroe.Executor.Shared.Nodes;

/// <summary>报出一个条目的违规文本，作用域由调用方补上。</summary>
/// <param name="message">违规文本。</param>
public delegate void ExecutorMessageHandler(string message);

/// <summary>执行器条目的描述契约：种类名、静态形状、参数段字段与参数到条目实例的转换。
/// 描述与承载格式无关，格式层只负责把文本转成描述值。</summary>
public interface IExecutorDescription
{
    /// <summary>形状所属的种类名。</summary>
    ExecutorKind Kind { get; }

    /// <summary>该条目的静态形状。</summary>
    ExecutorShape Shape { get; }

    /// <summary>参数段声明的字段与取值形态。</summary>
    IReadOnlyList<ParameterField> Fields { get; }

    /// <summary>由本轮取值建出条目实例，声明内容不合法经 fail 报出。</summary>
    IExecutor Read(IParameterReader values, ExecutorMessageHandler fail);
}