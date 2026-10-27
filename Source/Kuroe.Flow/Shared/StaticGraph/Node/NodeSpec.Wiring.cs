namespace Kuroe.Flow.Shared.StaticGraph.Node;

/// <summary>节点接线规则：From、In、Model、Models 与 Validate 的上下文限制。</summary>
public sealed partial record NodeSpec
{
    /// <summary>按作用域检查接线字段。container 为真时按容器判定，引用条目按被引用定义判定。</summary>
    public void CheckWiring(NodeRuleContext context, Action<string> fail, bool? container = null)
    {
        bool isContainer = container ?? IsContainer;
        if (context == NodeRuleContext.Reference)
        {
            if (isContainer)
            {
                if (From.Count > 0)
                {
                    fail("引用容器不能声明 From，容器自身不接线。");
                }

                if (Model is not null)
                {
                    fail("引用节点组不能声明模型，用 Models 绑定组内成员的模型。");
                }

                if (Validate is not null)
                {
                    fail("引用节点组不能声明 Validate，输出校验只属于执行节点。");
                }
            }
            else
            {
                if (Models is not null)
                {
                    fail("引用执行节点不能声明模型绑定，用 Model 指定模型选择。");
                }

                if (In is { Count: > 0 })
                {
                    fail("引用执行节点不写输入端口绑定，接线一律经 From。");
                }
            }

            return;
        }

        if (isContainer)
        {
            if (context == NodeRuleContext.LibraryDefinition)
            {
                if (From.Count > 0)
                {
                    fail("库容器定义不接线，不能声明 From。");
                }

                if (In is not null)
                {
                    fail("库容器定义不接线，不能声明输入端口绑定。");
                }

                if (Model is not null || Models is not null)
                {
                    fail("库容器定义不声明模型选择与模型绑定，绑定由引用处提供。");
                }

                if (Validate is not null)
                {
                    fail("库容器定义不声明 Validate，输出校验只属于执行节点。");
                }
            }
            else
            {
                if (From.Count > 0)
                {
                    fail("容器节点不是执行节点，不支持 From。");
                }

                if (Model is not null || Models is not null)
                {
                    fail("容器节点不是执行节点，不支持模型选择。");
                }

                if (ModelSlots is not null)
                {
                    fail("内联容器不声明模型槽位与模型绑定，槽位声明只属于节点库容器定义。");
                }

                if (Validate is not null)
                {
                    fail("容器节点不是执行节点，不支持 Validate。");
                }
            }

            return;
        }

        if (context == NodeRuleContext.LibraryDefinition)
        {
            if (From.Count > 0)
            {
                fail("执行节点库定义的接线由引用处提供，不能写 From。");
            }

            if (In is not null)
            {
                fail("执行节点库定义不绑定输入端口，接线从引用处提供。");
            }

            if (ContainerOutputs is { Count: > 0 })
            {
                fail("库执行定义不声明容器输出端口，执行节点输出端口条目写 Name 与 Type。");
            }
        }
        else
        {
            if (In is { Count: > 0 })
            {
                fail("执行节点不写输入端口绑定，接线一律经 From。");
            }

            if (Models is not null || ModelSlots is not null)
            {
                fail("执行节点不声明模型槽位与模型绑定，槽位声明只属于容器定义。");
            }
        }
    }
}