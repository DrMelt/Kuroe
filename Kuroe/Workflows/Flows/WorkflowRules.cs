using ErrorOr;
using Kuroe.Shared.Workflows.Graph;
using Flow = Kuroe.Shared.Workflows.Flows;

namespace Kuroe.Workflows.Flows;

/// <summary>流程模板的校验规则。任务推进依赖这些不变量成立，加载与导入时逐条校验。
/// 一条流程内的错误一次给全。</summary>
static class WorkflowRules
{
    /// <summary>校验一条流程。</summary>
    public static ErrorOr<Success> Validate(Flow.Workflow flow)
    {
        List<Error> errors = [];

        var modelNames = new HashSet<Flow.ModelRef>();
        foreach (Flow.ModelRef name in flow.Models.Select(model => model.Name))
        {
            if (string.IsNullOrWhiteSpace(name.Value))
            {
                errors.Add(WorkflowErrors.Model(flow.Name, "(未命名)", "模型配置名不能为空。"));
            }
            else if (!modelNames.Add(name))
            {
                errors.Add(WorkflowErrors.Model(flow.Name, name.Value, "模型配置名重复。"));
            }
        }

        var containerNames = new HashSet<Flow.NodeName>();
        CollectContainers(flow.RootNode, containerNames);

        var executableNames = new HashSet<Flow.NodeName>();
        CollectExecutables(flow.RootNode, executableNames);

        var names = new HashSet<Flow.NodeName>();
        CheckTree(flow.RootNode, names, executableNames, modelNames, containerNames, flow.Name, errors);

        // 引用类错误不存在时才展平，避免编译时的模型配置查表落空
        if (errors.Count == 0)
        {
            CheckShape(FlowCompiler.Compile(flow), flow.Name, errors);
        }

        return errors.Count > 0 ? errors : Result.Success;
    }

    /// <summary>收集全部容器名，From 引用允许指向容器。</summary>
    private static void CollectContainers(Flow.NodeSpec node, HashSet<Flow.NodeName> names)
    {
        if (node.Nodes is not { Count: > 0 } children)
        {
            return;
        }

        names.Add(node.Name);
        foreach (Flow.NodeSpec child in children)
        {
            CollectContainers(child, names);
        }
    }

    /// <summary>收集全部执行节点名，From 引用据此判定存在。</summary>
    private static void CollectExecutables(Flow.NodeSpec node, HashSet<Flow.NodeName> names)
    {
        if (node.Execution is not null)
        {
            names.Add(node.Name);
        }
        else if (node.Nodes is { Count: > 0 } children)
        {
            foreach (Flow.NodeSpec child in children)
            {
                CollectExecutables(child, names);
            }
        }
    }

    /// <summary>递归校验名字、From 引用、容器与模型配置引用。执行先后由拓扑排序保证，这里只检查引用落在执行节点或容器上。</summary>
    private static void CheckTree(
        Flow.NodeSpec node,
        HashSet<Flow.NodeName> names,
        HashSet<Flow.NodeName> executableNames,
        HashSet<Flow.ModelRef> modelNames,
        HashSet<Flow.NodeName> containerNames,
        string flowName,
        List<Error> errors)
    {
        foreach (Flow.NodeName from in node.From)
        {
            if (node.Nodes is { Count: > 0 })
            {
                break;
            }

            if (!executableNames.Contains(from) && !containerNames.Contains(from))
            {
                errors.Add(WorkflowErrors.Node(flowName, node.Name.Value, $"From 引用的节点 {from} 不在流程里。"));
            }
        }

        if (string.IsNullOrWhiteSpace(node.Name.Value))
        {
            errors.Add(WorkflowErrors.Node(flowName, "(未命名)", "节点名不能为空。"));
        }
        else if (!names.Add(node.Name))
        {
            errors.Add(WorkflowErrors.Node(flowName, node.Name.Value, "节点名重复。"));
        }

        if (node.Nodes is { Count: > 0 } children)
        {
            if (!HasExecutable(children))
            {
                errors.Add(WorkflowErrors.Node(flowName, node.Name.Value, "容器里至少要有一个执行节点。"));
            }
            else
            {
                foreach (Flow.NodeSpec child in children)
                {
                    CheckTree(child, names, executableNames, modelNames, containerNames, flowName, errors);
                }
            }

            if (node.From.Count > 0)
            {
                errors.Add(WorkflowErrors.Node(flowName, node.Name.Value, "容器节点不是执行节点，不支持 From。"));
            }
        }
        else if (node.Execution is { } execution)
        {
            if (string.IsNullOrWhiteSpace(execution.Model.Value) || !modelNames.Contains(execution.Model))
            {
                errors.Add(WorkflowErrors.Node(flowName, node.Name.Value, $"引用的模型配置 {execution.Model.Value} 不存在。"));
            }

            ValidateSplit(node, flowName, errors);
        }
        else if (node.Use is null)
        {
            errors.Add(WorkflowErrors.Node(flowName, node.Name.Value, "节点必须声明执行配置或子节点。"));
        }
    }

    /// <summary>子树里是否至少有一个执行节点。</summary>
    private static bool HasExecutable(IReadOnlyList<Flow.NodeSpec> nodes)
    {
        foreach (Flow.NodeSpec node in nodes)
        {
            if (node.Execution is not null)
            {
                return true;
            }

            if (node.Nodes is { Count: > 0 } children && HasExecutable(children))
            {
                return true;
            }
        }

        return false;
    }

    /// <summary>拆分配置的静态规则：只能写在规划执行节点上，条目与补充上限的取值边界。</summary>
    private static void ValidateSplit(Flow.NodeSpec node, string flowName, List<Error> errors)
    {
        Flow.ExecutableSpec? executable = node.Execution;
        if (executable?.Split is not { } split)
        {
            return;
        }

        if (executable.Output != Flow.NodeOutput.Plan)
        {
            errors.Add(WorkflowErrors.Node(flowName, node.Name.Value, "Split 只能写在规划节点上。"));
        }

        if (split.Items is null && split.ExtrasMax is null)
        {
            errors.Add(WorkflowErrors.Node(flowName, node.Name.Value, "Split 至少要声明 Items 或 ExtrasMax。"));
        }

        if (split.Items is null && split.ExtrasMax is 0)
        {
            errors.Add(WorkflowErrors.Node(flowName, node.Name.Value, "Split.ExtrasMax 为 0 时要求声明至少一条 Items。"));
        }

        if (split.Items is not null && split.Items.Count is 0 or > 20)
        {
            errors.Add(WorkflowErrors.Node(flowName, node.Name.Value, "Split.Items 要有 1 到 20 条。"));
        }

        if (split.Items is { } items
            && items.Any(item => string.IsNullOrWhiteSpace(item.Title) || string.IsNullOrWhiteSpace(item.Instruction)))
        {
            errors.Add(WorkflowErrors.Node(flowName, node.Name.Value, "Split.Items 每条的 Title 与 Instruction 不能为空。"));
        }

        if (split.ExtrasMax is < 0 or > 20)
        {
            errors.Add(WorkflowErrors.Node(flowName, node.Name.Value, "Split.ExtrasMax 必须是 0 到 20 的整数。"));
        }

        if (executable.IsStaticSplit)
        {
            if (executable.Prompt is not null)
            {
                errors.Add(WorkflowErrors.Node(flowName, node.Name.Value, "纯静态拆分节点不支持 Prompt。"));
            }

            if (node.From.Count > 0)
            {
                errors.Add(WorkflowErrors.Node(flowName, node.Name.Value, "纯静态拆分节点不支持 From。"));
            }

            if (node.Gate == Flow.NodeGate.Review)
            {
                errors.Add(WorkflowErrors.Node(flowName, node.Name.Value, "纯静态拆分节点不支持待批准门控。"));
            }
        }
    }

    /// <summary>展平后的图规则：边组合、分支归属与拆分条目。执行直接按边推进。</summary>
    private static void CheckShape(NodeGraph graph, string flowName, List<Error> errors)
    {
        if (!IsAcyclic(graph))
        {
            errors.Add(WorkflowErrors.Body(flowName, "流程引用关系存在环，请检查 From。"));
            return;
        }

        foreach (FlowEdge edge in graph.Edges)
        {
            GraphNode source = graph[edge.From];
            if (graph[edge.To] is not ExecutableNode target)
            {
                continue;
            }

            if (target.Mode == Flow.NodeMode.Single
                && source is ExecutableNode { Mode: Flow.NodeMode.PerItem }
                && target.Output == Flow.NodeOutput.Plan)
            {
                errors.Add(WorkflowErrors.Node(flowName, target.Name.Value, "规划执行节点不能从按条目展开的执行节点取输入。"));
            }

            if (edge.Feed == EdgeFeed.Items && source is not ExecutableNode { Output: Flow.NodeOutput.Plan })
            {
                errors.Add(WorkflowErrors.Node(flowName, target.Name.Value, "按条目展开的执行节点只能从规划执行节点取拆分。"));
            }

            if (edge.Feed == EdgeFeed.Aligned
                && graph.ItemSpace(edge.From) is { } fromSpace
                && graph.ItemSpace(edge.To) is { } toSpace
                && fromSpace != toSpace)
            {
                errors.Add(WorkflowErrors.Node(flowName, target.Name.Value, "逐条对齐的两端必须来自同一个拆分。"));
            }

            if (target.Branch is { } branch
                && (target.Mode != Flow.NodeMode.PerItem || graph.ItemSource(target.Index) is null))
            {
                errors.Add(WorkflowErrors.Node(flowName, target.Name.Value, $"声明分支 {branch} 的执行节点必须按条目展开并从规划执行节点取拆分。"));
            }
        }

        // 按条目展开必须能确定实例集：从规划执行节点取拆分，或从其它展开执行节点取对齐
        foreach (ExecutableNode executable in graph.ExecutableNodes)
        {
            if (executable.Mode != Flow.NodeMode.PerItem)
            {
                continue;
            }

            bool expandable = graph.Incoming(executable.Index).Any(edge =>
                edge.Feed is EdgeFeed.Items or EdgeFeed.Aligned);
            if (!expandable)
            {
                // 容器来源只供整份上下文，不提供实例集，单独出现时节点永远无法展开
                string reason = graph.Incoming(executable.Index).Any(edge => graph[edge.From] is ContainerNode)
                    ? "按条目展开的执行节点不能从容器取实例集，请引用规划执行节点或其它展开执行节点。"
                    : "按条目展开的执行节点必须从规划执行节点或其它展开执行节点取输入。";
                errors.Add(WorkflowErrors.Node(flowName, executable.Name.Value, reason));
            }
        }

        // 分支声明必须落在引用它的静态拆分条目里
        foreach (ExecutableNode executable in graph.ExecutableNodes)
        {
            if (!executable.IsStaticSplit || executable.Split?.Items is not { } fixedItems)
            {
                continue;
            }

            foreach (Flow.SplitItem item in fixedItems)
            {
                if (item.Branch is not { } branch)
                {
                    continue;
                }

                bool owned = graph.Outgoing(executable.Index).Any(edge =>
                    edge.Feed == EdgeFeed.Items
                    && graph[edge.To] is ExecutableNode target
                    && target.Branch == branch);
                if (!owned)
                {
                    errors.Add(WorkflowErrors.Node(flowName, executable.Name.Value,
                        $"Split.Items 的分支“{branch}”没有对应的分支执行节点。"));
                }
            }
        }
    }

    /// <summary>拓扑排序判定无环。容器来源边按成员展开成执行依赖，执行按边推进，环会让激活永远等不到齐备。
    /// 成员直接或间接依赖自身所在容器链，展开后就成为自环或容器间交叉环，一并在此拒绝。</summary>
    private static bool IsAcyclic(NodeGraph graph)
    {
        var indegree = new Dictionary<int, int>();
        var outgoing = new Dictionary<int, List<int>>();
        foreach (int index in graph.ExecutableNodes.Select(node => node.Index))
        {
            indegree[index] = 0;
            outgoing[index] = [];
        }

        foreach (FlowEdge edge in graph.Edges)
        {
            foreach (int source in graph.ExecutablesIn(edge.From))
            {
                indegree[edge.To]++;
                outgoing[source].Add(edge.To);
            }
        }

        var ready = new Queue<int>(indegree.Where(entry => entry.Value == 0).Select(entry => entry.Key));
        int visited = 0;
        while (ready.TryDequeue(out int current))
        {
            visited++;
            foreach (int to in outgoing[current])
            {
                if (--indegree[to] == 0)
                {
                    ready.Enqueue(to);
                }
            }
        }

        return visited == graph.ExecutableNodes.Count;
    }

    /// <summary>一组流程里重复的流程名，没有时为空。</summary>
    public static string? Duplicated(IReadOnlyList<Flow.Workflow> flows) =>
        flows.GroupBy(flow => flow.Name).FirstOrDefault(group => group.Count() > 1)?.Key;
}