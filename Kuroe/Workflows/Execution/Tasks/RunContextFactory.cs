using ApiHub.Shared.Models;
using Kuroe.Executions.Runs;
using Kuroe.Shared.Executions;
using Kuroe.Shared.Executions.Runs;
using Kuroe.Shared.Executions.Tools;
using Kuroe.Shared.Workflows.Flows;
using Kuroe.Shared.Workflows.Graph;
using Kuroe.Shared.Workflows.Tasks;
using ExecutableNode = Kuroe.Shared.Workflows.Graph.ExecutableNode;

namespace Kuroe.Workflows.Execution.Tasks;

/// <summary>为执行节点装配上下文。启动的执行只用这里给出的内容，装配规则集中在一处。
/// 每条取值边按它的取值域取值：整份产出、来源全部实例、或与目标同序号的实例产出。</summary>
internal static class RunContextFactory
{
    /// <summary>单条种子文本的上限。</summary>
    private const int TextLimit = 2000;

    /// <summary>为执行节点的实例装配这一轮的上下文。itemIndex 为空表示整节点实例。</summary>
    internal static RunContext Create(WorkTask task, RuntimeExecutable node, int? itemIndex, ModelName model)
    {
        List<ContextMessage> seed = [];
        List<ContextMessage> contextInput = [];
        List<string> inputs = [];
        foreach (FlowEdge edge in task.Graph.Incoming(node.Index))
        {
            inputs.Add(InputLine(task, edge));
            if (edge.Type == PortType.Signal)
            {
                // 信号输入不取数，来源产出不进上下文，只作启动信号
                continue;
            }

            List<ContextMessage> target = edge.Type == PortType.Context ? contextInput : seed;
            AppendUpstreamOutput(task, edge, itemIndex, target);
        }

        // 上下文输入端口的内容与系统指令一起构成请求前缀段，置于种子最前
        seed.InsertRange(0, contextInput);

        PlanItem? item = ItemOf(task, node, itemIndex);
        if (item is not null)
        {
            string branch = item.Branch is { } name ? $"\n实施分支：{name}" : string.Empty;
            seed.Add(new ContextMessage(MessageRole.User,
                Truncate($"本条目：{item.Title}\n要做：{item.Instruction}\n验收标准：{item.Acceptance}{branch}"),
                new ItemSource(OriginOf(task, node), item.Index, item.Title)));
        }

        int count = node.ExecutionCount(itemIndex) + 1;

        return new RunContext
        {
            Task = task.Id,
            NodeIndex = node.Index,
            NodeName = node.Name,
            Instruction = BuildInstruction(node, item, count),
            Model = model,
            ItemIndex = itemIndex,
            ExecutionCount = count,
            Tools = ToolsFor(node.Executable),
            Seed = seed,
            SystemPrompt = Combine(node.Executable.SystemPrompt, BuildBriefing(task, node, inputs)),
            OutputPorts = [.. node.Executable.Outputs.Where(port => port.Type == PortType.Data)],
        };
    }

    /// <summary>按一条取值边追加上游产出：同序号实例的取值按目标实例取来源该实例的整份，其余按边上的端口取来源产出消息集。</summary>
    private static void AppendUpstreamOutput(WorkTask task, FlowEdge edge, int? itemIndex, List<ContextMessage> seed)
    {
        if (edge.Scope == FeedScope.Item)
        {
            if (itemIndex is { } index)
            {
                AppendPortMessages(task, edge.From, edge.Port, index, seed);
            }

            return;
        }

        AppendPortMessages(task, edge.From, edge.Port, null, seed);
    }

    /// <summary>取来源节点某端口的注入消息集并入种子：整份直接取放行产出，命名端口与上下文端口返回节点已构造的消息。</summary>
    private static void AppendPortMessages(WorkTask task, int fromIndex, PortName port, int? item, List<ContextMessage> seed)
    {
        foreach (ContextMessage message in task.Runtime[fromIndex].OutputMessages(task, port, item))
        {
            seed.Add(new ContextMessage(message.Role, Truncate(message.Text), message.Source));
        }
    }
    /// <summary>来源节点上某实例最近一次成功收口且产出非空的 run。</summary>
    private static Run? LatestSucceededRun(WorkTask task, int node, int? item) =>
        task.Runs.LastOrDefault(run =>
            run.Context.NodeIndex == node
            && run.Context.ItemIndex == item
            && run.State == RunState.Succeeded
            && run.Result is { Length: > 0 });

    /// <summary>按条目展开时本实例的条目内容，整节点实例为空。条目身份只在归属空间内有效。</summary>
    private static PlanItem? ItemOf(WorkTask task, RuntimeExecutable node, int? itemIndex)
    {
        if (itemIndex is not { } index || node.Mode != NodeMode.PerItem)
        {
            return null;
        }

        return task.Graph.ItemSpace(node.Index) is { } space && task.SplitFor(space) is { } split
            ? split.Items.FirstOrDefault(item => item.Index == index)
            : null;
    }

    /// <summary>条目内容的出处 run：优先用拆分来源的规划 run，找不到按条目归属空间取拆分的交回者。</summary>
    private static RunId OriginOf(WorkTask task, RuntimeExecutable node)
    {
        if (task.Graph.PlanOf(node.Index)?.SplitSource is { } source
            && LatestSucceededRun(task, source, null) is { } planRun)
        {
            return planRun.Id;
        }

        return task.Graph.ItemSpace(node.Index) is { } space && task.SplitFor(space) is { } split
            ? split.Origin
            : new RunId(0);
    }

    /// <summary>指令正文：节点要求、本次负责的条目与第几轮。目标经依赖边进入上下文，端口要求进系统指令，都不在此拼接。</summary>
    private static string BuildInstruction(RuntimeExecutable node, PlanItem? item, int count)
    {
        List<string> lines = [];
        if (node.Executable.Execution.Prompt is { Length: > 0 } prompt)
        {
            lines.Add(prompt);
        }

        if (item is { } entry)
        {
            string branch = entry.Branch is { } name ? $"（分支 {name}）" : string.Empty;
            lines.Add($"本次只负责条目 {entry.Index + 1}：{entry.Title}{branch}");
        }

        if (count > 1)
        {
            lines.Add($"这是第 {count} 轮实施。");
        }

        return string.Join('\n', lines);
    }

    /// <summary>本轮接口说明：本节点的输入端口与需要交回的产出，随系统指令发给模型。</summary>
    private static string BuildBriefing(WorkTask task, RuntimeExecutable node, List<string> inputs)
    {
        List<string> lines = ["本轮接口"];
        if (inputs.Count > 0)
        {
            lines.Add("输入端口：");
            lines.AddRange(inputs);
        }

        List<string> outputs = OutputLines(task, node);
        if (outputs.Count > 0)
        {
            lines.Add("需要交回：");
            lines.AddRange(outputs);
        }

        lines.Add($"结束时用 {ContractTools.Status.Name} 提交本轮状态，status 取 completed 完成或 blocked 无法完成，blocked 时必须给出 reason。");

        return string.Join('\n', lines);
    }

    /// <summary>一条入边的输入端口说明：来源、端口与取用方式，信号输入标明不进上下文。</summary>
    private static string InputLine(WorkTask task, FlowEdge edge)
    {
        string source = task.Graph[edge.From] is StartNode ? "流程起点" : $"节点「{task.Graph[edge.From].Name}」";
        string port = $"端口「{edge.Port}」";
        if (edge.Type == PortType.Signal)
        {
            return $"- 输入「{edge.Input}」取自 {source}{port}：仅作启动信号，不进上下文。";
        }

        string consumption = edge.Kind == EdgeKind.Split
            ? "该来源交回的条目拆分，本实例对应其中一个条目"
            : edge.Scope switch
            {
                FeedScope.AllInstances => "该来源全部实例的产出",
                FeedScope.Item => "该来源同序号实例的产出",
                _ => "该来源最近一份产出",
            };

        string placement = edge.Type == PortType.Context ? "，置于上下文最前" : string.Empty;

        return $"- 输入「{edge.Input}」取自 {source}{port}：{consumption}{placement}。";
    }

    /// <summary>本节点需要交回的产出：Data 端口与条目拆分端口各一条，未声明时为空。</summary>
    private static List<string> OutputLines(WorkTask task, RuntimeExecutable node)
    {
        ExecutableNode executable = node.Executable;
        List<string> lines = [];
        if (executable.HasDataPorts)
        {
            string ports = ContractText.List(executable.Outputs.Where(port => port.Type == PortType.Data));
            lines.Add($"- 输出端口 {ports}。用 {ContractTools.Port.Name} 提交端口名到文本的 JSON 对象，端口必须与声明一一对应。");
        }

        if (executable.HasItemsPort)
        {
            PortName port = executable.Outputs.First(item => item.Type == PortType.Items).Name;
            List<string> branches = Branches(task, node);
            string guide = branches.Count == 0 ? string.Empty : $"可选分支：{string.Join('、', branches)}，条目须标明其一。";
            lines.Add($"- 条目拆分端口「{port}」。用 {ContractTools.Plan.Name} 提交对象数组，每项含 Title、Instruction、Acceptance。{guide}");
        }

        return lines;
    }

    /// <summary>拆分出口所驱动的分支执行节点声明的分支名，供条目标明归属。</summary>
    private static List<string> Branches(WorkTask task, RuntimeExecutable node)
    {
        List<string> branches = [];
        foreach (FlowEdge edge in task.Graph.Outgoing(node.Index))
        {
            if (edge.Kind == EdgeKind.Split && task.Graph[edge.To] is ExecutableNode { Execution.Branch: { } name })
            {
                branches.Add(name.Value);
            }
        }

        return branches;
    }

    /// <summary>系统指令：节点声明的恒定块在前，本轮接口说明在后。</summary>
    private static string Combine(IReadOnlyList<string> declared, string briefing) =>
        declared.Count == 0 ? briefing : $"{string.Join('\n', declared)}\n\n{briefing}";

    /// <summary>本轮工具面：节点声明的能力工具加按输出端口附上的契约工具，执行状态工具所有节点都有。</summary>
    private static List<ToolPath> ToolsFor(ExecutableNode node)
    {
        List<ToolPath> names = [.. node.Execution.Tools];
        if (node.HasItemsPort)
        {
            names.Add(ContractTools.Plan.Path);
        }

        if (node.HasDataPorts)
        {
            names.Add(ContractTools.Port.Path);
        }

        names.Add(ContractTools.Status.Path);

        return names;
    }

    private static string Truncate(string text) =>
        text.Length <= TextLimit ? text : string.Concat(text.AsSpan(0, TextLimit), "…");
}
