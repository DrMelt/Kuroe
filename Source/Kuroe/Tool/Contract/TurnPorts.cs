using Kuroe.Flow.Execution.Tasks;
using Kuroe.Flow.Shared.Execution.Turns;
using Kuroe.Flow.Shared.StaticGraph;

namespace Kuroe.Tool.Contract;

/// <summary>回合所属 run 声明的命名端口。契约工具按回合换载体时经它取交回清单。</summary>
internal static class TurnPorts
{
    /// <summary>该回合所属 run 声明的 Text 输出端口，没有声明时为空。</summary>
    internal static IReadOnlyList<Port>? Text(TaskRegistry registry, TurnScope scope) =>
        registry.FindRun(scope.Run) is { IsError: false } found && found.Value.Context.TextPorts.Count > 0
            ? found.Value.Context.TextPorts
            : null;
}