using ErrorOr;
using Kuroe.Flow.Shared.Execution.Runs;
using Kuroe.Flow.Shared.Execution.Tasks;
using Kuroe.Tool;
using Kuroe.Flow.Shared.StaticGraph;
using Kuroe.Flow.Execution.Tasks;

namespace Kuroe.Cli.Tests.Support;

/// <summary>不接模型的执行器：按节点产出要求交回条目与端口取值，并按声明收口。</summary>
public sealed class FakeExecutor : IRunExecutor
{
    public WorkItemSubmitter? Submitter { get; set; }

    /// <summary>声明输出端口节点的交回通道，端口节点由此提交命名段。</summary>
    public PortSubmitter? PortSubmitter { get; set; }

    /// <summary>本轮状态的声明通道，所有执行节点都有这个工具。</summary>
    public StatusSubmitter? StatusSubmitter { get; set; }

    /// <summary>单个 run 的人为耗时，用于取消断言。</summary>
    public int DelayMs { get; set; } = 10;

    /// <summary>声明 WorkItems 端口的执行节点交回的条目拆分。</summary>
    private const string ItemsJson = """
        [
          { "Title": "甲", "Instruction": "做甲", "Acceptance": "甲可见" },
          { "Title": "乙", "Instruction": "做乙", "Acceptance": "乙可见" }
        ]
        """;

    /// <summary>按声明的端口名逐个生成同名校验文本，提交与声明一一对应。</summary>
    private static string PortValuesFor(IReadOnlyList<Port> ports)
    {
        string body = string.Join(", ", ports.Select(port => $"\"{port.Name.Value}\": \"{port.Name.Value}产出\""));
        return "{ " + body + " }";
    }

    public async Task<ErrorOr<string>> ExecuteAsync(IRun run, CancellationToken cancellationToken)
    {
        await Task.Delay(DelayMs, cancellationToken);

        if (run.Context.OutputPorts.Any(port => port.Type == PortType.WorkItems))
        {
            _ = Submitter!.SubmitItems(run.Id, ItemsJson);
        }

        IReadOnlyList<Port> ports = run.Context.TextPorts;
        if (ports.Count > 0)
        {
            _ = PortSubmitter!.SubmitValues(run.Id, PortValuesFor(ports));
        }

        _ = StatusSubmitter!.SubmitVerdict(run.Id, "completed", null);

        return $"{run.Context.NodeName}产出";
    }
}
