using ErrorOr;
using Kuroe.Execution.Shared;
using Kuroe.Tasks.Shared;

namespace Kuroe.Maf.Control;

/// <summary>MAF 流程定义：流程名、开始端口、步骤与连接。定义由宿主的代码给出。</summary>
public sealed record MafFlowDefinition(
    FlowName Name,
    IReadOnlyList<MafStartPort> StartPorts,
    IReadOnlyList<MafStep> Steps,
    IReadOnlyList<MafEdge> Edges)
{
    /// <summary>执行使用的模型开始端口名。</summary>
    public static PortName ModelPort { get; } = new("MODEL_NAME");

    /// <summary>流程说明。</summary>
    public string? Description { get; init; }

    /// <summary>入口步骤：没有入边的步骤。定义必须恰好有一个入口。</summary>
    internal ErrorOr<string> Entry()
    {
        string[] entries = [.. Steps.Select(step => step.Id).Where(id => Edges.All(edge => edge.To != id))];

        return entries.Length == 1
            ? entries[0]
            : Error.Validation(FlowErrorCodes.FlowComposition, "MAF 流程必须恰好有一个入口步骤。");
    }

    /// <summary>校验定义结构：步骤标识唯一且可作路径、连接两端存在、每个步骤至多一个上游、全部步骤从入口可达、开始端口声明完整。</summary>
    internal IReadOnlyList<Error> Validate()
    {
        List<Error> errors = [];
        if (Steps.Count == 0)
        {
            errors.Add(FlowErrors.Body(Name.Value, "没有步骤。"));
        }

        HashSet<string> ids = [];
        foreach (MafStep step in Steps)
        {
            if (string.IsNullOrWhiteSpace(step.Id) || step.Id.Contains('/'))
            {
                errors.Add(FlowErrors.Body(Name.Value, $"步骤标识「{step.Id}」不合法。"));
                continue;
            }

            if (!ids.Add(step.Id))
            {
                errors.Add(FlowErrors.Body(Name.Value, $"步骤标识「{step.Id}」重复。"));
            }

            if (step.Outputs.Any(port => string.IsNullOrWhiteSpace(port.Value)))
            {
                errors.Add(FlowErrors.Body(Name.Value, $"步骤「{step.Id}」的输出端口名不合法。"));
            }

            if (step.Outputs.GroupBy(port => port).Any(group => group.Count() > 1))
            {
                errors.Add(FlowErrors.Body(Name.Value, $"步骤「{step.Id}」的输出端口重复。"));
            }
        }

        foreach (MafEdge edge in Edges)
        {
            if (!ids.Contains(edge.From))
            {
                errors.Add(FlowErrors.Body(Name.Value, $"连接起点「{edge.From}」不在流程中。"));
            }

            if (!ids.Contains(edge.To))
            {
                errors.Add(FlowErrors.Body(Name.Value, $"连接终点「{edge.To}」不在流程中。"));
            }
        }

        foreach (IGrouping<string, MafEdge> incoming in Edges.GroupBy(edge => edge.To).Where(group => group.Count() > 1))
        {
            errors.Add(FlowErrors.Body(Name.Value,
                $"步骤「{incoming.Key}」有多个上游，步骤只接受一个上游。"));
        }

        ErrorOr<string> entry = Entry();
        if (entry.IsError)
        {
            errors.Add(entry.FirstError);
        }
        else if (errors.Count == 0)
        {
            foreach (string unreachable in Unreachable(entry.Value, ids))
            {
                errors.Add(FlowErrors.Body(Name.Value, $"步骤「{unreachable}」从入口不可达。"));
            }
        }

        if (StartPorts.Any(port => string.IsNullOrWhiteSpace(port.Name.Value)))
        {
            errors.Add(FlowErrors.Body(Name.Value, "开始端口名不合法。"));
        }

        if (StartPorts.GroupBy(port => port.Name).Any(group => group.Count() > 1))
        {
            errors.Add(FlowErrors.Body(Name.Value, "开始端口重复。"));
        }

        if (StartPorts.All(port => port.Name != ModelPort))
        {
            errors.Add(FlowErrors.Body(Name.Value, $"没有开始端口「{ModelPort}」。"));
        }

        return errors;
    }

    /// <summary>步骤中从入口不可达的标识。</summary>
    private IEnumerable<string> Unreachable(string entry, HashSet<string> ids)
    {
        HashSet<string> reached = [entry];
        bool grew = true;
        while (grew)
        {
            grew = false;
            foreach (MafEdge edge in Edges.Where(edge => reached.Contains(edge.From)))
            {
                grew |= reached.Add(edge.To);
            }
        }

        return ids.Where(id => !reached.Contains(id));
    }
}

/// <summary>MAF 流程的开始端口。</summary>
public sealed record MafStartPort(PortName Name, string? Default = null);

/// <summary>MAF 流程的步骤：标识、标签、输出端口与按模型建立执行目标的工厂。
/// 步骤标识同时是执行目标标识；步骤的输入是直接上游步骤的输出。</summary>
public sealed record MafStep(
    string Id,
    string Label,
    IReadOnlyList<PortName> Outputs,
    Func<string, IExecutionTarget> Target);

/// <summary>步骤之间的连接。</summary>
public sealed record MafEdge(string From, string To);

/// <summary>MAF 流程的一条上游产出，随消息在步骤之间传递。</summary>
internal sealed record MafStepOutput(string Step, string Port, string Text);

/// <summary>MAF 流程步骤之间传递的消息：上游步骤与它交回的端口文本。</summary>
internal sealed record MafStepMessage(string Step, IReadOnlyList<MafStepOutput> Outputs);