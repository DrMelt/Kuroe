using ErrorOr;
using Kuroe.Kernel.Execution.Shared.Ports;
using Kuroe.Kernel.Tasks.Shared;
using Kuroe.Kernel.Execution.Shared.Identity;

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

    /// <summary>流程是否需要模型：任一步骤声明模型端口时需要。</summary>
    public bool RequiresModel => Steps.Any(step => step.ModelPort is not null);

    /// <summary>入口步骤：没有入边的步骤，按步骤声明序。</summary>
    public IReadOnlyList<MafStep> Entries() =>
        [.. Steps.Where(step => Edges.All(edge => edge.To != step.Id))];

    /// <summary>按步骤标识取步骤，不存在时为空。</summary>
    public MafStep? Find(MafStepId id) =>
        Steps.FirstOrDefault(step => step.Id == id);

    /// <summary>按步骤标识文本取步骤，不存在时为空。</summary>
    public MafStep? Find(string id) =>
        Steps.FirstOrDefault(step => step.Id.Value == id);

    /// <summary>校验流程结构：步骤与输出端口、连接端点、入口存在、全部步骤从任一入口可达、开始端口声明。</summary>
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

        IReadOnlyList<MafStep> entries = Entries();
        if (entries.Count == 0)
        {
            errors.Add(FlowErrors.Body(Name.Value, "流程没有入口步骤。"));
        }

        if (entries.Count > 0 && errors.Count == 0)
        {
            foreach (MafStepId unreachable in Unreachable(entries.Select(step => step.Id), ids))
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

        if (RequiresModel && StartPorts.All(port => port.Name != ModelPort))
        {
            errors.Add(FlowErrors.Body(Name.Value, $"没有开始端口「{ModelPort}」。"));
        }

        return errors;
    }

    /// <summary>步骤中从入口不可达的标识。</summary>
    private IEnumerable<MafStepId> Unreachable(IEnumerable<MafStepId> entries, HashSet<MafStepId> ids)
    {
        HashSet<MafStepId> reached = [.. entries];
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
public sealed record MafStartPort(PortName Name, string? Default = null)
{
    /// <summary>端口取值种类。</summary>
    public ExecutionPortKind Kind { get; init; } = ExecutionPortKind.Text;

    /// <summary>端口模式。</summary>
    public MafPortMode Mode { get; init; } = MafPortMode.Plain;

    /// <summary>提交时是否要求给出取值。不承载内容的开始端口自动放行。</summary>
    public bool RequiresInput { get; init; } = true;
}

/// <summary>MAF 流程的步骤：标识、标签、输出端口、执行装配工厂与输入装配。
/// 步骤标识同时是执行步骤标识；步骤的输入由输入装配按上游产出与流程开始输入给出。</summary>
public sealed record MafStep(
    MafStepId Id,
    string Label,
    IReadOnlyList<PortName> Outputs,
    IMafStepInvocationFactory InvocationFactory,
    IMafStepInputAssembler Inputs)
{
    /// <summary>步骤对应的执行步骤标识。</summary>
    public ExecutionStepId ExecutionStepId => Id.ToExecutionStepId();

    /// <summary>输出端口计划，未给出时由 <see cref="Outputs"/> 按文本端口补齐。</summary>
    public IReadOnlyList<MafOutputPort> OutputPlan { get; init; } = [];

    /// <summary>步骤使用的模型端口，类型不需要模型时为空。</summary>
    public PortName? ModelPort { get; init; }

    /// <summary>生效的输出端口计划：未显式给出时把 <see cref="Outputs"/> 按文本端口补齐。</summary>
    public IReadOnlyList<MafOutputPort> OutputPorts() =>
        OutputPlan.Count > 0
            ? OutputPlan
            : [.. Outputs.Select(port => new MafOutputPort(port, ExecutionPortKind.Text))];
}

/// <summary>步骤之间的连接。</summary>
public sealed record MafEdge(MafStepId From, MafStepId To);