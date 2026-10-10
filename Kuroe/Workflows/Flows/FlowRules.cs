using ErrorOr;
using Kuroe.Shared.Workflows.Graph;
using Flow = Kuroe.Shared.Workflows.Flows;

namespace Kuroe.Workflows.Flows;

/// <summary>流程模板的校验规则。任务推进依赖这些不变量成立，加载与导入时逐条校验。
/// 一条流程内的错误一次给全。</summary>
static class FlowRules
{
    /// <summary>校验一条流程。</summary>
    public static ErrorOr<Success> Validate(Flow.FlowDefinition flow)
    {
        List<Error> errors = [];

        var modelNames = new HashSet<Flow.ModelRef>();
        foreach (Flow.ModelRef name in flow.Models.Select(model => model.Name))
        {
            if (string.IsNullOrWhiteSpace(name.Value))
            {
                errors.Add(FlowErrors.Model(flow.Name.Value, "(未命名)", "模型选择名不能为空。"));
            }
            else if (!modelNames.Add(name))
            {
                errors.Add(FlowErrors.Model(flow.Name.Value, name.Value, "模型选择名重复。"));
            }
        }

        foreach (Flow.ModelDefinition model in flow.Models)
        {
            if (model.Runtime && model.Model is not null)
            {
                errors.Add(FlowErrors.Model(flow.Name.Value, model.Name.Value, "运行时模型与固定模型不能同时声明。"));
            }
        }

        var containerNames = new HashSet<Flow.NodeName>();
        CollectContainers(flow.RootNode, containerNames);

        var executableNames = new HashSet<Flow.NodeName>();
        CollectExecutables(flow.RootNode, executableNames);

        var inputNames = new HashSet<Flow.NodeName>();
        CollectInputs(flow.RootNode, inputNames);

        var perItemNames = new HashSet<Flow.NodeName>();
        CollectPerItem(flow.RootNode, perItemNames);

        var names = new HashSet<Flow.NodeName>();
        var outputs = new Dictionary<Flow.NodeName, IReadOnlyList<Flow.PortName>>();
        CollectOutputs(flow.RootNode, outputs);

        var containerOutputs = new Dictionary<Flow.NodeName, IReadOnlyDictionary<Flow.PortName, Flow.PortRef>>();
        CollectContainerOutputs(flow.RootNode, containerOutputs);

        var parentOf = new Dictionary<Flow.NodeName, Flow.NodeName>();
        CollectParents(flow.RootNode, parentOf, parent: null);

        CheckTree(flow.RootNode, names, executableNames, inputNames, perItemNames, modelNames, containerNames, outputs, containerOutputs, parentOf, flow.Name.Value, errors);

        // 引用类错误不存在时才展平，避免编译时的模型选择查表落空
        if (errors.Count == 0)
        {
            CheckShape(FlowCompiler.Compile(flow), flow.Name.Value, errors);
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

    /// <summary>收集执行节点名，From 引用据此判定存在。</summary>
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

    /// <summary>收集输入节点名，上下文端口来源据此拦截。</summary>
    private static void CollectInputs(Flow.NodeSpec node, HashSet<Flow.NodeName> names)
    {
        if (node.Execution is { Output: Flow.NodeOutput.Input })
        {
            names.Add(node.Name);
        }
        else if (node.Nodes is { Count: > 0 } children)
        {
            foreach (Flow.NodeSpec child in children)
            {
                CollectInputs(child, names);
            }
        }
    }

    /// <summary>收集按条目展开的执行节点名，上下文输入端口来源据此拦截。</summary>
    private static void CollectPerItem(Flow.NodeSpec node, HashSet<Flow.NodeName> names)
    {
        if (node.Execution is { Mode: Flow.NodeMode.PerItem })
        {
            names.Add(node.Name);
        }
        else if (node.Nodes is { Count: > 0 } children)
        {
            foreach (Flow.NodeSpec child in children)
            {
                CollectPerItem(child, names);
            }
        }
    }

    /// <summary>收集执行节点的命名输出端口表，From 引用据此判定端口存在。</summary>
    private static void CollectOutputs(Flow.NodeSpec node, Dictionary<Flow.NodeName, IReadOnlyList<Flow.PortName>> outputs)
    {
        if (node.Execution is not null)
        {
            outputs[node.Name] = node.Outputs;
        }
        else if (node.Nodes is { Count: > 0 } children)
        {
            foreach (Flow.NodeSpec child in children)
            {
                CollectOutputs(child, outputs);
            }
        }
    }

    /// <summary>收集容器实例的输出端口表，装配层 From 引用容器端口据此判定端口存在。</summary>
    private static void CollectContainerOutputs(
        Flow.NodeSpec node,
        Dictionary<Flow.NodeName, IReadOnlyDictionary<Flow.PortName, Flow.PortRef>> containerOutputs)
    {
        if (node.Nodes is not { Count: > 0 } children)
        {
            return;
        }

        if (node.Out is { Count: > 0 } outs)
        {
            containerOutputs[node.Name] = outs;
        }

        foreach (Flow.NodeSpec child in children)
        {
            CollectContainerOutputs(child, containerOutputs);
        }
    }

    /// <summary>收集每个节点的直接所属容器名，容器封装据此判定引用作用域。</summary>
    private static void CollectParents(
        Flow.NodeSpec node,
        Dictionary<Flow.NodeName, Flow.NodeName> parentOf,
        Flow.NodeName? parent)
    {
        if (parent is { } container)
        {
            parentOf[node.Name] = container;
        }

        if (node.Nodes is { Count: > 0 } children)
        {
            foreach (Flow.NodeSpec child in children)
            {
                CollectParents(child, parentOf, node.Name);
            }
        }
    }

    /// <summary>引用来源是否落在当前节点可见的容器作用域内：同层成员或外层容器成员可见，嵌套更深容器的成员对容器外不可见。
    /// 引用者本身没有父容器时只允许引用同根下节点。</summary>
    private static bool IsInScope(
        Flow.NodeName source,
        Flow.NodeName referrer,
        IReadOnlyDictionary<Flow.NodeName, Flow.NodeName> parentOf)
    {
        if (!parentOf.TryGetValue(source, out Flow.NodeName sourceContainer))
        {
            return false;
        }

        if (!parentOf.TryGetValue(referrer, out Flow.NodeName referrerContainer))
        {
            return false;
        }

        // 沿引用者所在容器链上溯，遇到来源所在容器即可见
        Flow.NodeName? current = referrerContainer;
        while (current is { } container)
        {
            if (container == sourceContainer)
            {
                return true;
            }

            current = parentOf.TryGetValue(container, out Flow.NodeName parent) ? parent : null;
        }

        return false;
    }

    /// <summary>递归校验名字、From 引用、容器与模型选择引用。执行先后由拓扑排序保证，这里只检查引用落在执行节点或容器上。</summary>
    private static void CheckTree(
        Flow.NodeSpec node,
        HashSet<Flow.NodeName> names,
        HashSet<Flow.NodeName> executableNames,
        HashSet<Flow.NodeName> inputNames,
        HashSet<Flow.NodeName> perItemNames,
        HashSet<Flow.ModelRef> modelNames,
        HashSet<Flow.NodeName> containerNames,
        IReadOnlyDictionary<Flow.NodeName, IReadOnlyList<Flow.PortName>> outputs,
        IReadOnlyDictionary<Flow.NodeName, IReadOnlyDictionary<Flow.PortName, Flow.PortRef>> containerOutputs,
        IReadOnlyDictionary<Flow.NodeName, Flow.NodeName> parentOf,
        string flowName,
        List<Error> errors)
    {
        CheckSourceReferences(node.From, node, executableNames, inputNames, perItemNames, containerNames, outputs, containerOutputs, parentOf, flowName, errors);

        foreach (Flow.SourceRef duplicate in node.From
            .GroupBy(source => (source.Ref, source.Or, source.Signal, source.Context))
            .Where(group => group.Count() > 1)
            .Select(group => group.First()))
        {
            errors.Add(FlowErrors.Node(flowName, node.Name.Value, $"From 引用的节点 {duplicate.Ref.Display} 出现多次。"));
        }

        CheckValidation(node, flowName, errors);

        if (string.IsNullOrWhiteSpace(node.Name.Value))
        {
            errors.Add(FlowErrors.Node(flowName, "(未命名)", "节点名不能为空。"));
        }
        else if (node.Name.Value.Contains('@'))
        {
            errors.Add(FlowErrors.Node(flowName, node.Name.Value, "节点名不能含 @，@ 是端口引用的分隔符。"));
        }
        else if (!names.Add(node.Name))
        {
            errors.Add(FlowErrors.Node(flowName, node.Name.Value, "节点名重复。"));
        }

        if (node.Nodes is { Count: > 0 } children)
        {
            if (node.MaxRuns is { } containerLimit && containerLimit < 1)
            {
                errors.Add(FlowErrors.Node(flowName, node.Name.Value, "MaxRuns 必须是正整数。"));
            }

            if (node.Model is not null || node.Models is not null)
            {
                errors.Add(FlowErrors.Node(flowName, node.Name.Value, "容器节点不是执行节点，不声明模型。"));
            }

            if (!HasExecutable(children))
            {
                errors.Add(FlowErrors.Node(flowName, node.Name.Value, "容器里至少要有一个执行节点。"));
            }
            else
            {
                foreach (Flow.NodeSpec child in children)
                {
                    CheckTree(child, names, executableNames, inputNames, perItemNames, modelNames, containerNames, outputs, containerOutputs, parentOf, flowName, errors);
                }
            }

            if (node.Out is { } outs)
            {
                CheckContainerOut(node, outs, children, outputs, inputNames, flowName, errors);
            }

            if (node.From.Count > 0)
            {
                errors.Add(FlowErrors.Node(flowName, node.Name.Value, "容器节点不是执行节点，不支持 From。"));
            }
        }
        else if (node.Execution is { } execution)
        {
            // 输入节点不启动 run，不需要模型选择
            if (execution.Output != Flow.NodeOutput.Input)
            {
                if (node.Model is not { } model)
                {
                    errors.Add(FlowErrors.Node(flowName, node.Name.Value, "执行节点必须声明模型选择。"));
                }
                else if (!modelNames.Contains(model))
                {
                    errors.Add(FlowErrors.Node(flowName, node.Name.Value, $"引用的模型选择 {model.Value} 不存在。"));
                }
            }

            CheckExpansion(node, flowName, errors);
            ValidateSplit(node, flowName, errors);
            ValidateInput(node, flowName, errors);
            ValidateInterface(node, flowName, errors);
        }
        else if (node.Use is null)
        {
            errors.Add(FlowErrors.Node(flowName, node.Name.Value, "节点必须声明执行配置或子节点。"));
        }
    }

    /// <summary>校验上游接线条目：来源节点存在、端口存在、容器封装不泄漏。</summary>
    private static void CheckSourceReferences(
        IReadOnlyList<Flow.SourceRef> sources,
        Flow.NodeSpec node,
        HashSet<Flow.NodeName> executableNames,
        HashSet<Flow.NodeName> inputNames,
        HashSet<Flow.NodeName> perItemNames,
        HashSet<Flow.NodeName> containerNames,
        IReadOnlyDictionary<Flow.NodeName, IReadOnlyList<Flow.PortName>> outputs,
        IReadOnlyDictionary<Flow.NodeName, IReadOnlyDictionary<Flow.PortName, Flow.PortRef>> containerOutputs,
        IReadOnlyDictionary<Flow.NodeName, Flow.NodeName> parentOf,
        string flowName,
        List<Error> errors)
    {
        foreach (Flow.SourceRef entry in sources)
        {
            Flow.PortRef reference = entry.Ref;
            if (node.Nodes is { Count: > 0 })
            {
                break;
            }

            if (entry.Context)
            {
                if (reference.Source is null)
                {
                    errors.Add(FlowErrors.Node(flowName, node.Name.Value,
                        "Context 条目来源必须写节点名或 来源@端口，不能引用绑定端口。"));
                }
                else
                {
                    Flow.NodeName contextSource = reference.Source.Value;
                    if (!executableNames.Contains(contextSource))
                    {
                        errors.Add(FlowErrors.Node(flowName, node.Name.Value,
                            $"Context 条目来源 {contextSource} 必须是非输入执行节点。"));
                    }
                    else if (inputNames.Contains(contextSource))
                    {
                        errors.Add(FlowErrors.Node(flowName, node.Name.Value,
                            $"Context 条目来源 {contextSource} 不能是输入节点。"));
                    }
                    else if (perItemNames.Contains(contextSource))
                    {
                        errors.Add(FlowErrors.Node(flowName, node.Name.Value,
                            $"Context 条目来源 {contextSource} 不能是按条目展开的节点。"));
                    }
                }
            }

            if (reference.Source is null)
            {
                // 装配层执行节点没有传入端口可绑定，绑定端口引用在 From 里不合法
                errors.Add(FlowErrors.Node(flowName, node.Name.Value,
                    $"From 引用的节点 @{reference.Port.Value} 不在流程里。"));
                continue;
            }

            Flow.NodeName source = reference.Source.Value;
            if (containerNames.Contains(source))
            {
                // 容器对外只经 Out 声明端口，没有整份产出
                if (!containerOutputs.TryGetValue(source, out IReadOnlyDictionary<Flow.PortName, Flow.PortRef>? containerPorts)
                    || !containerPorts.ContainsKey(reference.Port))
                {
                    errors.Add(FlowErrors.Node(flowName, node.Name.Value,
                        $"容器 {source} 没有输出端口 {reference.Port.Value}。"));
                }

                continue;
            }

            if (!executableNames.Contains(source))
            {
                errors.Add(FlowErrors.Node(flowName, node.Name.Value, $"From 引用的节点 {source} 不在流程里。"));
                continue;
            }

            if (!IsInScope(source, node.Name, parentOf))
            {
                errors.Add(FlowErrors.Node(flowName, node.Name.Value,
                    $"不能从容器外直接引用容器内成员或端口 {source}，容器对外只暴露输出端口。"));
                continue;
            }

            if (reference.Port == Flow.PortNames.ContextOutput)
            {
                if (inputNames.Contains(source))
                {
                    errors.Add(FlowErrors.Node(flowName, node.Name.Value, $"输入节点不启动 run，不能作为上下文端口来源。"));
                }

                continue;
            }

            if (reference.Port == Flow.PortNames.Split)
            {
                // 拆分端口只用字形到按条目展开的来源，来源是否规划由 CheckShape 按边校验
                continue;
            }

            if (!outputs.TryGetValue(source, out IReadOnlyList<Flow.PortName>? declared) || !declared.Contains(reference.Port))
            {
                errors.Add(FlowErrors.Node(flowName, node.Name.Value, $"节点 {source} 没有输出端口 {reference.Port.Value}。"));
            }
        }
    }

    /// <summary>装配容器输出端口的绑定校验：绑定目标必须在子树内且是执行节点，端口必须是被引用成员声明的命名端口或 ContextOutput。
    /// 装配层容器对外只经 Out 端口被消费，绑定错误会在引用处落空。</summary>
    private static void CheckContainerOut(
        Flow.NodeSpec container,
        IReadOnlyDictionary<Flow.PortName, Flow.PortRef> outs,
        IReadOnlyList<Flow.NodeSpec> children,
        IReadOnlyDictionary<Flow.NodeName, IReadOnlyList<Flow.PortName>> outputs,
        HashSet<Flow.NodeName> inputNames,
        string flowName,
        List<Error> errors)
    {
        foreach ((Flow.PortName port, Flow.PortRef target) in outs)
        {
            if (target.Source is not { } sourceName)
            {
                errors.Add(FlowErrors.Node(flowName, container.Name.Value,
                    $"容器输出端口 {port.Value} 的绑定目标不能是对绑定端口的引用。"));
                continue;
            }

            if (!ContainsName(children, sourceName))
            {
                errors.Add(FlowErrors.Node(flowName, container.Name.Value,
                    $"容器输出端口 {port.Value} 的绑定目标 {sourceName} 不在容器 {container.Name} 的子节点里。"));
                continue;
            }

            if (!outputs.TryGetValue(sourceName, out IReadOnlyList<Flow.PortName>? memberPorts))
            {
                errors.Add(FlowErrors.Node(flowName, container.Name.Value,
                    $"容器输出端口 {port.Value} 的绑定目标节点 {sourceName} 不是执行节点，无法取产出。"));
                continue;
            }

            if (target.Port == Flow.PortNames.ContextOutput)
            {
                if (inputNames.Contains(sourceName))
                {
                    errors.Add(FlowErrors.Node(flowName, container.Name.Value,
                        $"容器输出端口 {port.Value} 的绑定目标节点 {sourceName} 是输入节点，不能作为上下文端口来源。"));
                }

                continue;
            }

            if (!memberPorts.Contains(target.Port))
            {
                errors.Add(FlowErrors.Node(flowName, container.Name.Value,
                    $"容器输出端口 {port.Value} 的绑定目标节点 {sourceName} 没有输出端口 {target.Port.Value}。"));
            }
        }
    }

    /// <summary>名字是否出现在节点表里，执行与容器都算。</summary>
    private static bool ContainsName(IReadOnlyList<Flow.NodeSpec> nodes, Flow.NodeName name)
    {
        foreach (Flow.NodeSpec node in nodes)
        {
            if (node.Name == name)
            {
                return true;
            }

            if (node.Nodes is { Count: > 0 } children && ContainsName(children, name))
            {
                return true;
            }
        }

        return false;
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

    /// <summary>输入节点的静态规则：只以用户输入为产出，不接受执行配置与门控。</summary>
    private static void ValidateInput(Flow.NodeSpec node, string flowName, List<Error> errors)
    {
        Flow.ExecutableSpec? executable = node.Execution;
        if (executable?.Output != Flow.NodeOutput.Input)
        {
            return;
        }

        void Reject(bool condition, string message)
        {
            if (condition)
            {
                errors.Add(FlowErrors.Node(flowName, node.Name.Value, message));
            }
        }

        Reject(node.Model is not null, "输入节点不启动 run，不能声明模型选择。");
        Reject(executable.Tools.Count > 0, "输入节点不启动 run，不能声明 Tools。");
        Reject(executable.Prompt is { Length: > 0 }, "输入节点不启动 run，不能声明 Prompt。");
        Reject(executable.Validate is not null, "输入节点不启动 run，不能声明 Validate。");
        Reject(node.From.Any(source => source.Or is not null), "输入节点不启动 run，不能声明可选启动组。");
        Reject(node.From.Any(source => source.Signal), "输入节点不启动 run，不能声明触发信号。");
        Reject(node.From.Any(source => source.Context), "输入节点不启动 run，不能声明上下文输入。");
        Reject(executable.Mode != Flow.NodeMode.Single, "输入节点只能整节点等待用户输入。");
        Reject(executable.Branch is not null, "输入节点不启动 run，不能声明 Branch。");
        Reject(node.Gate == Flow.NodeGate.Review, "输入节点回答即放行，不能声明 Review 门控。");
        Reject(node.Outputs.Count > 1, "输入节点最多声明一个输出端口，回答写入该端口。");
        Reject(node.SystemPrompt.Count > 0, "输入节点不启动 run，不能声明系统指令。");
    }

    /// <summary>节点接口的静态规则：输出端口声明在文本与 PerItem 执行节点上，端口名不与保留名冲突。</summary>
    private static void ValidateInterface(Flow.NodeSpec node, string flowName, List<Error> errors)
    {
        if (node.Execution is not { } executable || executable.Output == Flow.NodeOutput.Input)
        {
            return;
        }

        if (executable.Question is { Length: > 0 })
        {
            errors.Add(FlowErrors.Node(flowName, node.Name.Value, "Question 只属于输入节点。"));
        }

        if (node.Outputs.Count > 0 && executable.Output != Flow.NodeOutput.Text)
        {
            errors.Add(FlowErrors.Node(flowName, node.Name.Value, "输出端口只能声明在文本执行节点上。"));
        }

        foreach (Flow.PortName port in node.Outputs.Where(port => port == Flow.PortNames.Split || port == Flow.PortNames.ContextOutput || port == Flow.PortNames.ContextInput))
        {
            errors.Add(FlowErrors.Node(flowName, node.Name.Value, $"输出端口名 {port.Value} 是保留名，不可作命名输出端口声明。"));
        }

        foreach (Flow.PortName port in node.Outputs
            .GroupBy(port => port)
            .Where(group => group.Count() > 1)
            .Select(group => group.Key))
        {
            errors.Add(FlowErrors.Node(flowName, node.Name.Value, $"输出端口 {port.Value} 重复。"));
        }

        foreach (string block in node.SystemPrompt.Where(block => string.IsNullOrWhiteSpace(block)))
        {
            errors.Add(FlowErrors.Node(flowName, node.Name.Value, "系统指令块不能为空。"));
        }
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
            errors.Add(FlowErrors.Node(flowName, node.Name.Value, "Split 只能写在规划节点上。"));
        }

        if (split.Items is null && split.ExtrasMax is null)
        {
            errors.Add(FlowErrors.Node(flowName, node.Name.Value, "Split 至少要声明 Items 或 ExtrasMax。"));
        }

        if (split.Items is null && split.ExtrasMax is 0)
        {
            errors.Add(FlowErrors.Node(flowName, node.Name.Value, "Split.ExtrasMax 为 0 时要求声明至少一条 Items。"));
        }

        if (split.Items is not null && split.Items.Count is 0 or > 20)
        {
            errors.Add(FlowErrors.Node(flowName, node.Name.Value, "Split.Items 要有 1 到 20 条。"));
        }

        if (split.Items is { } items
            && items.Any(item => string.IsNullOrWhiteSpace(item.Title) || string.IsNullOrWhiteSpace(item.Instruction)))
        {
            errors.Add(FlowErrors.Node(flowName, node.Name.Value, "Split.Items 每条的 Title 与 Instruction 不能为空。"));
        }

        if (split.ExtrasMax is < 0 or > 20)
        {
            errors.Add(FlowErrors.Node(flowName, node.Name.Value, "Split.ExtrasMax 必须是 0 到 20 的整数。"));
        }

        if (executable.IsStaticSplit)
        {
            if (executable.Prompt is not null)
            {
                errors.Add(FlowErrors.Node(flowName, node.Name.Value, "纯静态拆分节点不支持 Prompt。"));
            }

            if (node.From.Count > 0)
            {
                errors.Add(FlowErrors.Node(flowName, node.Name.Value, "纯静态拆分节点不支持 From。"));
            }

            if (node.Gate == Flow.NodeGate.Review)
            {
                errors.Add(FlowErrors.Node(flowName, node.Name.Value, "纯静态拆分节点不支持待批准门控。"));
            }
        }
    }

    /// <summary>Validate 与 From 条目标记的组合约束：输出校验与可选/信号条目都要求整节点执行，不与纯静态拆分享。</summary>
    private static void CheckExpansion(Flow.NodeSpec node, string flowName, List<Error> errors)
    {
        Flow.ExecutableSpec execution = node.Execution!;
        if (execution.MaxRuns is { } limit && limit < 1)
        {
            errors.Add(FlowErrors.Node(flowName, node.Name.Value, "MaxRuns 必须是正整数。"));
        }

        if (execution.Validate is not null)
        {
            if (node.Outputs.Count == 0)
            {
                errors.Add(FlowErrors.Node(flowName, node.Name.Value, "声明 Validate 的节点必须声明输出端口，校验对象是端口产出。"));
            }

            if (execution.Mode == Flow.NodeMode.PerItem)
            {
                errors.Add(FlowErrors.Node(flowName, node.Name.Value, "声明 Validate 的执行节点不能按条目展开，必须是整节点执行。"));
            }

            if (execution.IsStaticSplit)
            {
                errors.Add(FlowErrors.Node(flowName, node.Name.Value, "纯静态拆分节点不支持 Validate。"));
            }
        }

        if (execution.Mode == Flow.NodeMode.PerItem && node.From.Any(source => source.Or is not null || source.Signal || source.Context))
        {
            errors.Add(FlowErrors.Node(flowName, node.Name.Value, "声明可选启动组、触发信号或上下文输入的执行节点不能按条目展开，必须是整节点执行。"));
        }
    }

    /// <summary>输出校验的取值边界：参数必要性、正则合法性。</summary>
    private static void CheckValidation(Flow.NodeSpec node, string flowName, List<Error> errors)
    {
        Flow.OutputValidation? validation = node.Execution?.Validate;
        if (validation is null)
        {
            return;
        }

        switch (validation.Predicate)
        {
            case Flow.ValidationPredicate.NonEmpty:
                if (validation.Argument is not null)
                {
                    errors.Add(FlowErrors.Node(flowName, node.Name.Value, "NonEmpty 校验不接受参数。"));
                }

                break;

            case Flow.ValidationPredicate.Pattern:
                if (string.IsNullOrEmpty(validation.Argument))
                {
                    errors.Add(FlowErrors.Node(flowName, node.Name.Value, "Pattern 校验需要正则参数。"));
                }
                else
                {
                    try
                    {
                        _ = new System.Text.RegularExpressions.Regex(
                            validation.Argument, System.Text.RegularExpressions.RegexOptions.None, TimeSpan.FromSeconds(1));
                    }
                    catch (System.Text.RegularExpressions.RegexParseException)
                    {
                        errors.Add(FlowErrors.Node(flowName, node.Name.Value, $"Pattern 校验的正则不合法：{validation.Argument}。"));
                    }
                }

                break;

            default:
                if (string.IsNullOrEmpty(validation.Argument))
                {
                    errors.Add(FlowErrors.Node(flowName, node.Name.Value, $"{validation.Predicate} 校验需要指定文本参数。"));
                }

                break;
        }
    }

    /// <summary>展平后的图规则：边组合、分支归属与拆分条目。执行直接按边推进。</summary>
    private static void CheckShape(NodeGraph graph, string flowName, List<Error> errors)
    {
        CheckLoops(graph, flowName, errors);
        CheckAlignment(graph, flowName, errors);

        foreach (FlowEdge edge in graph.Edges)
        {
            GraphNode source = graph[edge.From];
            if (graph[edge.To] is not ExecutableNode target)
            {
                continue;
            }

            if (target.Execution.Mode == Flow.NodeMode.Single
                && source is ExecutableNode { Execution.Mode: Flow.NodeMode.PerItem }
                && target.Execution.Output == Flow.NodeOutput.Plan)
            {
                errors.Add(FlowErrors.Node(flowName, target.Name.Value, "规划执行节点不能从按条目展开的执行节点取输入。"));
            }

            if (edge.Feed == EdgeFeed.Items && source is not ExecutableNode { Execution.Output: Flow.NodeOutput.Plan })
            {
                errors.Add(FlowErrors.Node(flowName, target.Name.Value, "按条目展开的执行节点只能从规划执行节点取拆分。"));
            }

            if (edge.Port == Flow.PortNames.Split && edge.Feed != EdgeFeed.Items)
            {
                errors.Add(FlowErrors.Node(flowName, target.Name.Value, "拆分端口只能被按条目展开的执行节点消费。"));
            }

            if (edge.Feed == EdgeFeed.Aligned
                && graph.ItemSpace(edge.From) is { } fromSpace
                && graph.ItemSpace(edge.To) is { } toSpace
                && fromSpace != toSpace)
            {
                errors.Add(FlowErrors.Node(flowName, target.Name.Value, "逐条对齐的两端必须来自同一个拆分。"));
            }

            if (target.Execution.Branch is { } branch
                && (target.Execution.Mode != Flow.NodeMode.PerItem || graph.ItemSource(target.Index) is null))
            {
                errors.Add(FlowErrors.Node(flowName, target.Name.Value, $"声明分支 {branch} 的执行节点必须按条目展开并从规划执行节点取拆分。"));
            }
        }

        // 按条目展开必须能确定实例集：从规划执行节点取拆分，或从其它展开执行节点取对齐
        foreach (ExecutableNode executable in graph.ExecutableNodes)
        {
            if (executable.Execution.Mode != Flow.NodeMode.PerItem)
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
                errors.Add(FlowErrors.Node(flowName, executable.Name.Value, reason));
            }
        }

        // 分支声明必须落在引用它的静态拆分条目里
        foreach (ExecutableNode executable in graph.ExecutableNodes)
        {
            if (!executable.IsStaticSplit || executable.Execution.Split?.Items is not { } fixedItems)
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
                    && target.Execution.Branch == branch);
                if (!owned)
                {
                    errors.Add(FlowErrors.Node(flowName, executable.Name.Value,
                        $"Split.Items 的分支“{branch}”没有对应的分支执行节点。"));
                }
            }
        }

    }

    /// <summary>引用环的启动不变量：环必须有环外来源或环内输入节点，否则环内节点永远等不到启动条件，任务无法推进。
    /// 输入节点每轮从外部接收回答，本身构成周期性外源。整节点依赖构成的迭代环若可启动则合法。</summary>
    private static void CheckLoops(NodeGraph graph, string flowName, List<Error> errors)
    {
        foreach (FlowEdge edge in graph.Edges)
        {
            // 成员从自身所在容器取输入会让容器永远等不到齐备，是启动条件永不满足的环
            if (graph[edge.From] is ContainerNode container
                && graph.ExecutablesIn(container.Index).Contains(edge.To))
            {
                errors.Add(FlowErrors.Node(flowName, graph[edge.To].Name.Value,
                    "执行节点不能从自身所在容器取输入，容器会永远等不到齐备。"));
            }
        }

        foreach (int[] members in graph.Loops())
        {
            // 环内输入节点是挂点也是周期外源，无需环外来源即可启动
            if (members.Any(member => graph[member] is ExecutableNode { Execution.Output: Flow.NodeOutput.Input }))
            {
                continue;
            }

            bool externalEntry = members.Any(member =>
                graph.Incoming(member).Any(edge =>
                    graph.ExecutablesIn(edge.From).Any(source => !members.Contains(source))));
            if (!externalEntry)
            {
                ExecutableNode representative = (ExecutableNode)graph[members[0]];
                errors.Add(FlowErrors.Node(flowName, representative.Name.Value,
                    "引用环没有环外来源且环内没有输入节点，任务无法启动。"));
            }
        }
    }

    /// <summary>逐条对齐的引用必须无环：按条目展开的实例空间沿对齐边递推，环里的对齐会让它递归不止。
    /// 整节点依赖构成的环由运行时自然迭代，不在此拒绝。</summary>
    private static void CheckAlignment(NodeGraph graph, string flowName, List<Error> errors)
    {
        var indegree = new Dictionary<int, int>();
        var outgoing = new Dictionary<int, List<int>>();
        foreach (int index in graph.ExecutableNodes.Select(node => node.Index))
        {
            indegree[index] = 0;
            outgoing[index] = [];
        }

        foreach (FlowEdge edge in graph.Edges.Where(edge => edge.Feed is EdgeFeed.Items or EdgeFeed.Aligned))
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

        if (visited < graph.ExecutableNodes.Count)
        {
            errors.Add(FlowErrors.Body(flowName, "逐条对齐的引用关系存在环，请检查 From。"));
        }
    }

    /// <summary>一组流程里重复的流程名，没有时为空。</summary>
    public static Flow.FlowName? Duplicated(IReadOnlyList<Flow.FlowDefinition> flows) =>
        flows.GroupBy(flow => flow.Name).FirstOrDefault(group => group.Count() > 1)?.Key;
}
