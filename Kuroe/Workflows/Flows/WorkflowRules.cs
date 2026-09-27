using ErrorOr;
using Kuroe.Shared.Agent;
using Kuroe.Shared.Workflows.Flows;

namespace Kuroe.Workflows.Flows;

/// <summary>流程模板的校验规则。任务推进依赖这些不变量成立，加载与导入时逐条校验。
/// 一条流程内的错误一次给全。</summary>
static class WorkflowRules
{
    /// <summary>校验一条流程。</summary>
    public static ErrorOr<Success> Validate(Workflow flow, int attemptLimit)
    {
        List<Error> errors = [];
        if (flow.Nodes.Count == 0)
        {
            errors.Add(WorkflowErrors.Body(flow.Name, "至少要有一个节点。"));
        }

        var agentNames = new HashSet<string>();
        foreach (string name in flow.Agents.Select(agent => agent.Name))
        {
            if (string.IsNullOrWhiteSpace(name))
            {
                errors.Add(WorkflowErrors.Agent(flow.Name, "(未命名)", "agent 名不能为空。"));
            }
            else if (!agentNames.Add(name))
            {
                errors.Add(WorkflowErrors.Agent(flow.Name, name, "agent 名重复。"));
            }
        }

        var containerNames = new HashSet<string>();
        CollectContainers(flow.Nodes, containerNames);

        var executableNames = new HashSet<string>();
        CollectExecutables(flow.Nodes, executableNames);

        var names = new HashSet<string>();
        CheckTree(flow.Nodes, names, executableNames, agentNames, containerNames, flow.Name, errors);

        // 引用类错误不存在时才展平，避免编译时的 agent 查表落空
        if (errors.Count == 0)
        {
            CheckShape(FlowCompiler.Compile(flow), flow.Name, attemptLimit, errors);
        }

        return errors.Count > 0 ? errors : Result.Success;
    }

    /// <summary>收集全部容器名，From 不允许引用容器。</summary>
    private static void CollectContainers(IReadOnlyList<NodeSpec> nodes, HashSet<string> names)
    {
        foreach (NodeSpec node in nodes)
        {
            if (node is FlowNode flow)
            {
                names.Add(node.Name);
                CollectContainers(flow.Nodes, names);
            }
        }
    }

    /// <summary>收集全部执行节点名，From 引用据此判定存在。</summary>
    private static void CollectExecutables(IReadOnlyList<NodeSpec> nodes, HashSet<string> names)
    {
        foreach (NodeSpec node in nodes)
        {
            if (node is AgentNode executable)
            {
                names.Add(executable.Name);
            }
            else if (node is FlowNode flow)
            {
                CollectExecutables(flow.Nodes, names);
            }
        }
    }

    /// <summary>递归校验名字、From 引用、容器与 agent 引用。执行先后由拓扑排序保证，这里只检查引用是否落在执行节点或容器上。</summary>
    private static void CheckTree(
        IReadOnlyList<NodeSpec> siblings,
        HashSet<string> names,
        HashSet<string> executableNames,
        HashSet<string> agentNames,
        HashSet<string> containerNames,
        string flowName,
        List<Error> errors)
    {
        foreach (NodeSpec node in siblings)
        {
            foreach (string from in node.From)
            {
                if (node is FlowNode)
                {
                    break;
                }

                if (containerNames.Contains(from))
                {
                    errors.Add(WorkflowErrors.Node(flowName, node.Name, $"From 引用的 {from} 是容器节点，请直接引用其中的执行节点。"));
                }
                else if (!executableNames.Contains(from))
                {
                    errors.Add(WorkflowErrors.Node(flowName, node.Name, $"From 引用的节点 {from} 不在流程里。"));
                }
            }

            if (string.IsNullOrWhiteSpace(node.Name))
            {
                errors.Add(WorkflowErrors.Node(flowName, "(未命名)", "节点名不能为空。"));
            }
            else if (!names.Add(node.Name))
            {
                errors.Add(WorkflowErrors.Node(flowName, node.Name, "节点名重复。"));
            }

            switch (node)
            {
                case FlowNode flow:
                    if (flow.Nodes.Count == 0)
                    {
                        errors.Add(WorkflowErrors.Node(flowName, node.Name, "容器至少要有一个子节点。"));
                    }
                    else
                    {
                        CheckTree(flow.Nodes, names, executableNames, agentNames, containerNames, flowName, errors);
                    }

                    if (flow.From.Count > 0)
                    {
                        errors.Add(WorkflowErrors.Node(flowName, node.Name, "容器节点不是执行节点，不支持 From。"));
                    }

                    if (flow.Prompt is not null)
                    {
                        errors.Add(WorkflowErrors.Node(flowName, node.Name, "容器节点不是执行节点，不支持 Prompt。"));
                    }

                    break;

                case AgentNode executable:
                    if (string.IsNullOrWhiteSpace(executable.Agent) || !agentNames.Contains(executable.Agent))
                    {
                        errors.Add(WorkflowErrors.Node(flowName, node.Name, $"引用的 agent {executable.Agent} 不存在。"));
                    }

                    ValidateSplit(executable, flowName, errors);
                    break;
            }
        }
    }

    /// <summary>拆分配置的静态规则：只能写在规划执行节点上，条目与补充上限的取值边界。</summary>
    private static void ValidateSplit(AgentNode executable, string flowName, List<Error> errors)
    {
        if (executable.Split is not { } split)
        {
            return;
        }

        if (executable.Output != NodeOutput.Plan)
        {
            errors.Add(WorkflowErrors.Node(flowName, executable.Name, "Split 只能写在规划节点上。"));
        }

        if (split.Items is null && split.ExtrasMax is null)
        {
            errors.Add(WorkflowErrors.Node(flowName, executable.Name, "Split 至少要声明 Items 或 ExtrasMax。"));
        }

        if (split.Items is null && split.ExtrasMax is 0)
        {
            errors.Add(WorkflowErrors.Node(flowName, executable.Name, "Split.ExtrasMax 为 0 时要求声明至少一条 Items。"));
        }

        if (split.Items is not null && split.Items.Count is 0 or > 20)
        {
            errors.Add(WorkflowErrors.Node(flowName, executable.Name, "Split.Items 要有 1 到 20 条。"));
        }

        if (split.Items is { } items
            && items.Any(item => string.IsNullOrWhiteSpace(item.Title) || string.IsNullOrWhiteSpace(item.Instruction)))
        {
            errors.Add(WorkflowErrors.Node(flowName, executable.Name, "Split.Items 每条的 Title 与 Instruction 不能为空。"));
        }

        if (split.ExtrasMax is < 0 or > 20)
        {
            errors.Add(WorkflowErrors.Node(flowName, executable.Name, "Split.ExtrasMax 必须是 0 到 20 的整数。"));
        }

        if (executable.IsStaticSplit)
        {
            if (executable.Prompt is not null)
            {
                errors.Add(WorkflowErrors.Node(flowName, executable.Name, "纯静态拆分节点不支持 Prompt。"));
            }

            if (executable.From.Count > 0)
            {
                errors.Add(WorkflowErrors.Node(flowName, executable.Name, "纯静态拆分节点不支持 From。"));
            }

            if (executable.Gate == NodeGate.Review)
            {
                errors.Add(WorkflowErrors.Node(flowName, executable.Name, "纯静态拆分节点不支持待批准门控。"));
            }
        }
    }

    /// <summary>展平后的图规则：边组合、分支归属、检查与拆分条目。执行直接按边推进。</summary>
    private static void CheckShape(NodeGraph graph, string flowName, int attemptLimit, List<Error> errors)
    {
        if (!IsAcyclic(graph))
        {
            errors.Add(WorkflowErrors.Body(flowName, "流程引用关系存在环，请检查 From。"));
            return;
        }

        foreach (FlowEdge edge in graph.Edges)
        {
            ExecutableNode source = graph[edge.From];
            ExecutableNode target = graph[edge.To];

            if (target.Mode == NodeMode.Single && source.Mode == NodeMode.PerItem && target.Output == NodeOutput.Plan)
            {
                errors.Add(WorkflowErrors.Node(flowName, target.Name, "规划执行节点不能从按条目展开的执行节点取输入。"));
            }

            if (edge.Feed == EdgeFeed.Items && source.Output != NodeOutput.Plan)
            {
                errors.Add(WorkflowErrors.Node(flowName, target.Name, "按条目展开的执行节点只能从规划执行节点取拆分。"));
            }

            if (edge.Feed == EdgeFeed.Aligned
                && graph.ItemSpace(edge.From) is { } fromSpace
                && graph.ItemSpace(edge.To) is { } toSpace
                && fromSpace != toSpace)
            {
                errors.Add(WorkflowErrors.Node(flowName, target.Name, "逐条对齐的两端必须来自同一个拆分。"));
            }

            if (target.Branch is { } branch && (target.Mode != NodeMode.PerItem || graph.ItemSource(target.Index) is null))
            {
                errors.Add(WorkflowErrors.Node(flowName, target.Name, $"声明分支 {branch} 的执行节点必须按条目展开并从规划执行节点取拆分。"));
            }
        }

        // 按条目展开必须能确定实例集：从规划执行节点取拆分，或从其它展开执行节点取对齐
        for (int index = 0; index < graph.Count; index++)
        {
            ExecutableNode executable = graph[index];
            if (executable.Mode != NodeMode.PerItem)
            {
                continue;
            }

            bool expandable = graph.Incoming(index).Any(edge =>
                edge.Feed is EdgeFeed.Items or EdgeFeed.Aligned);
            if (!expandable)
            {
                errors.Add(WorkflowErrors.Node(flowName, executable.Name, "按条目展开的执行节点必须从规划执行节点或其它展开执行节点取输入。"));
            }
        }

        // 分支声明必须落在引用它的静态拆分条目里
        for (int index = 0; index < graph.Count; index++)
        {
            ExecutableNode executable = graph[index];
            if (!executable.IsStaticSplit || executable.Split?.Items is not { } fixedItems)
            {
                continue;
            }

            foreach (SplitItem item in fixedItems)
            {
                if (item.Branch is not { } branch)
                {
                    continue;
                }

                bool owned = graph.Outgoing(index).Any(edge =>
                    edge.Feed == EdgeFeed.Items && graph[edge.To].Branch == branch);
                if (!owned)
                {
                    errors.Add(WorkflowErrors.Node(flowName, executable.Name,
                        $"Split.Items 的分支“{branch}”没有对应的分支执行节点。"));
                }
            }
        }

        foreach (ExecutableNode executable in graph.ExecutableNodes)
        {
            if (executable.Output != NodeOutput.Review && (executable.OnReject is not null || executable.MaxAttempts is not null))
            {
                errors.Add(WorkflowErrors.Node(flowName, executable.Name, "OnReject 与 MaxAttempts 只适用于检查节点。"));
            }

            if (executable.Output != NodeOutput.Review)
            {
                continue;
            }

            if (graph.CheckedSources(executable.Index).Count == 0)
            {
                errors.Add(WorkflowErrors.Node(flowName, executable.Name, "检查节点必须用 From 引用被检查的实施产出。"));
            }

            if (executable.MaxAttempts is < 1)
            {
                errors.Add(WorkflowErrors.Node(flowName, executable.Name, "MaxAttempts 必须为正整数。"));
            }

            if (executable.MaxAttempts > attemptLimit)
            {
                errors.Add(WorkflowErrors.Node(flowName, executable.Name, $"MaxAttempts 超过 Agent:MaxAttempts={attemptLimit}。"));
            }
        }

        if (!graph.ExecutableNodes.Any(executable => executable.Output == NodeOutput.Review))
        {
            errors.Add(WorkflowErrors.Body(flowName, "流程需要有检查节点。"));
        }

        // 按条目展开的实施必须被某个检查节点引用
        HashSet<int> checkedSources = [.. graph.ExecutableNodes
            .Where(executable => executable.Output == NodeOutput.Review)
            .SelectMany(executable => graph.CheckedSources(executable.Index))];
        foreach (ExecutableNode executable in graph.ExecutableNodes)
        {
            if (executable.Mode == NodeMode.PerItem && executable.Output != NodeOutput.Review && !checkedSources.Contains(executable.Index))
            {
                errors.Add(WorkflowErrors.Node(flowName, executable.Name, "按条目展开的实施必须被某个检查节点引用。"));
            }
        }
    }

    /// <summary>拓扑排序判定无环。执行按边推进，环会让激活永远等不到齐备。</summary>
    private static bool IsAcyclic(NodeGraph graph)
    {
        var indegree = new int[graph.Count];
        foreach (FlowEdge edge in graph.Edges)
        {
            indegree[edge.To]++;
        }

        var ready = new Queue<int>(Enumerable.Range(0, graph.Count).Where(index => indegree[index] == 0));
        int visited = 0;
        while (ready.TryDequeue(out int current))
        {
            visited++;
            foreach (int to in graph.Outgoing(current).Select(edge => edge.To))
            {
                if (--indegree[to] == 0)
                {
                    ready.Enqueue(to);
                }
            }
        }

        return visited == graph.Count;
    }

    /// <summary>一组流程里重复的流程名，没有时为空。</summary>
    public static string? Duplicated(IReadOnlyList<Workflow> flows) =>
        flows.GroupBy(flow => flow.Name).FirstOrDefault(group => group.Count() > 1)?.Key;
}