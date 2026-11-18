using ErrorOr;
using Kuroe.Tasks.Shared;
using Kuroe.Execution.Shared.Identity;

namespace Kuroe.Maf.Definition;

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
    public ErrorOr<MafStep> Entry()
    {
        MafStep[] entries = [.. Steps.Where(step => Edges.All(edge => edge.To != step.Id))];

        return entries.Length == 1
            ? entries[0]
            : Error.Validation(FlowErrorCodes.FlowComposition, "MAF 流程必须恰好有一个入口步骤。");
    }

    /// <summary>按步骤标识取步骤，不存在时为空。</summary>
    public MafStep? Find(MafStepId id) =>
        Steps.FirstOrDefault(step => step.Id == id);

    /// <summary>按步骤标识文本取步骤，不存在时为空。</summary>
    public MafStep? Find(string id) =>
        Steps.FirstOrDefault(step => step.Id.Value == id);

    /// <summary>校验定义结构：步骤标识唯一且可作路径、连接两端存在、每个步骤至多一个上游、全部步骤从入口可达、开始端口声明完整。</summary>
    public IReadOnlyList<Error> Validate()
    {
        List<Error> errors = [];
        if (Steps.Count == 0)
        {
            errors.Add(FlowErrors.Body(Name.Value, "没有步骤。"));
        }

        HashSet<MafStepId> ids = [];
        foreach (MafStep step in Steps)
        {
            ErrorOr<MafStepId> id = MafStepId.Create(step.Id.Value);
            if (id.IsError)
            {
                errors.Add(id.FirstError);
                continue;
            }

            if (!ids.Add(step.Id))
            {
                errors.Add(FlowErrors.Body(Name.Value, $"步骤标识「{step.Id.Value}」重复。"));
            }

            if (step.Outputs.Any(port => string.IsNullOrWhiteSpace(port.Value)))
            {
                errors.Add(FlowErrors.Body(Name.Value, $"步骤「{step.Id.Value}」的输出端口名不合法。"));
            }

            if (step.Outputs.GroupBy(port => port).Any(group => group.Count() > 1))
            {
                errors.Add(FlowErrors.Body(Name.Value, $"步骤「{step.Id.Value}」的输出端口重复。"));
            }
        }

        foreach (MafEdge edge in Edges)
        {
            if (!ids.Contains(edge.From))
            {
                errors.Add(FlowErrors.Body(Name.Value, $"连接起点「{edge.From.Value}」不在流程中。"));
            }

            if (!ids.Contains(edge.To))
            {
                errors.Add(FlowErrors.Body(Name.Value, $"连接终点「{edge.To.Value}」不在流程中。"));
            }
        }

        foreach (IGrouping<MafStepId, MafEdge> incoming in Edges.GroupBy(edge => edge.To).Where(group => group.Count() > 1))
        {
            errors.Add(FlowErrors.Body(Name.Value,
                $"步骤「{incoming.Key.Value}」有多个上游，步骤只接受一个上游。"));
        }

        ErrorOr<MafStep> entry = Entry();
        if (entry.IsError)
        {
            errors.Add(entry.FirstError);
        }
        else if (errors.Count == 0)
        {
            foreach (MafStepId unreachable in Unreachable(entry.Value.Id, ids))
            {
                errors.Add(FlowErrors.Body(Name.Value, $"步骤「{unreachable.Value}」从入口不可达。"));
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
    private IEnumerable<MafStepId> Unreachable(MafStepId entry, HashSet<MafStepId> ids)
    {
        HashSet<MafStepId> reached = [entry];
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

/// <summary>MAF 流程的步骤：标识、标签、输出端口与执行装配工厂。
/// 步骤标识同时是执行步骤标识；步骤的输入是直接上游步骤的输出。</summary>
public sealed record MafStep(
    MafStepId Id,
    string Label,
    IReadOnlyList<PortName> Outputs,
    IMafStepInvocationFactory InvocationFactory)
{
    /// <summary>步骤对应的执行步骤标识。</summary>
    public ExecutionStepId ExecutionStepId => Id.ToExecutionStepId();
}

/// <summary>步骤之间的连接。</summary>
public sealed record MafEdge(MafStepId From, MafStepId To);